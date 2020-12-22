using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Emit;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Exprgen
{
    internal static class CSharpCompiler
    {

        private static readonly MetadataReference[] s_references =
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(Assembly.GetExecutingAssembly().Location),
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
            // These two are needed to properly pick up System.Object when using methods on System.Console.
            // See here: https://github.com/dotnet/corefx/issues/11601
            MetadataReference.CreateFromFile(Assembly.Load(new AssemblyName("System.Runtime")).Location),
            MetadataReference.CreateFromFile(Assembly.Load(new AssemblyName("System.Linq")).Location),
            MetadataReference.CreateFromFile(Assembly.Load(new AssemblyName("mscorlib")).Location),
        };

        private static readonly CSharpCompilationOptions DebugOptions =
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, concurrentBuild: false, optimizationLevel: OptimizationLevel.Debug);

        private static readonly CSharpCompilationOptions ReleaseOptions =
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, concurrentBuild: false, optimizationLevel: OptimizationLevel.Release);

        internal static void CompileAndExecute(string fileName)
        {
            string contents = File.ReadAllText(fileName);
            CompilationUnitSyntax comp = SyntaxFactory.ParseCompilationUnit(contents, options: new CSharpParseOptions(LanguageVersion.Latest));
            //Console.WriteLine(comp.NormalizeWhitespace().ToFullString());

            CompileResult compResult = Compile(comp);
            if (compResult.Assembly == null)
            {
                Console.WriteLine("Got compiler errors:");
                Console.WriteLine(string.Join(Environment.NewLine, compResult.CompileErrors));
            }
            else
            {
                Assembly asm = Assembly.Load(compResult.Assembly);
                MethodInfo mainMethodInfo = asm.GetType("RyuJITTest").GetMethod("Main");
                Action<string[]> entryPoint = (Action<string[]>)Delegate.CreateDelegate(typeof(Action<string[]>), mainMethodInfo);

                Exception ex = null;
                //TextWriter origOut = Console.Out;

                MemoryStream ms = new MemoryStream();
                //StreamWriter sw = new StreamWriter(Console.s, Encoding.UTF8);

                try
                {
                    //Console.SetOut(sw);
                    entryPoint(null);
                }
                catch (Exception caughtEx)
                {
                    ex = caughtEx;
                }
                finally
                {
                    //Console.SetOut(origOut);
                    //sw.Close();
                }
            }
        }

        private static CompileResult Compile(CompilationUnitSyntax program)
        {
            SyntaxTree[] trees = { SyntaxFactory.SyntaxTree(program, new CSharpParseOptions(LanguageVersion.Latest)) };
            CSharpCompilation comp = CSharpCompilation.Create("RyuJITTest", trees, s_references, ReleaseOptions);

            using (var ms = new MemoryStream())
            {
                EmitResult result;
                try
                {
                    result = comp.Emit(ms);
                }
                catch (Exception ex)
                {
                    return new CompileResult(ex, ImmutableArray<Diagnostic>.Empty, null);
                }

                if (!result.Success)
                    return new CompileResult(null, result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToImmutableArray(), null);

                return new CompileResult(null, ImmutableArray<Diagnostic>.Empty, ms.ToArray());
            }
        }
    }

    internal class CompileResult
    {
        public CompileResult(Exception roslynException, ImmutableArray<Diagnostic> compileErrors, byte[] assembly)
        {
            RoslynException = roslynException;
            CompileErrors = compileErrors;
            Assembly = assembly;
        }

        public Exception RoslynException { get; }
        public ImmutableArray<Diagnostic> CompileErrors { get; }
        public byte[] Assembly { get; }
    }

}
