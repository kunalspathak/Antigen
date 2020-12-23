using Antigen.Tree;
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
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Antigen
{
    public class TestCase
    {
        #region Compiler options

        public enum CompilationType
        {
            Debug, Release
        };

        #endregion

        #region PreComputed roslyn syntax tress
        #endregion

        private const string MainMethodName = "MainMethod";

        private SyntaxNode testClass;

        private List<SyntaxNode> classesList;
        private List<SyntaxNode> methodsList;
        private List<SyntaxNode> propertiesList;
        private List<SyntaxNode> fieldsList;

        public string Name { get; private set; }
        public AstUtils AstUtils { get; private set; }

        public TestCase(int testId)
        {
            AstUtils = new AstUtils(this, new ConfigOptions(), null);
            Name = "TestClass" + testId;
        }

        public void Generate()
        {
            BaseMethod mainMethod = new BaseMethod(this, MainMethodName);
            mainMethod.Generate();

            IList<BaseMethod> methods = new List<BaseMethod>() { mainMethod };

            for (int i = 1; i < 5; i++)
            {
                var testMethod = new BaseMethod(this, "Method" + i);
                methods.Add(testMethod);
                testMethod.Generate();
            }

            ClassDeclarationSyntax klass = ClassDeclaration(Name).WithMembers(new SyntaxList<MemberDeclarationSyntax>(methods.Select(m => m.GeneratedMethod)));
            testClass = CompilationUnit()
                            .WithUsings(
                                SingletonList<UsingDirectiveSyntax>(
                                    UsingDirective(
                                        IdentifierName("System"))))
                            .WithMembers(
                                SingletonList<MemberDeclarationSyntax>(klass)).NormalizeWhitespace();
        }

        public void CompileAndExecute()
        {
            CompileResult compileResult = Compile(CompilationType.Release);
            Execute(compileResult);
        }

        private CompileResult Compile(CompilationType compilationType)
        {
            Console.WriteLine(testClass.ToString());

            string corelibPath = typeof(object).Assembly.Location;
            string otherAssembliesPath = Path.GetDirectoryName(corelibPath);
            MetadataReference systemPrivateCorelib = MetadataReference.CreateFromFile(corelibPath);
            MetadataReference systemConsole = MetadataReference.CreateFromFile(Path.Combine(otherAssembliesPath, "System.Console.dll"));
            MetadataReference systemRuntime = MetadataReference.CreateFromFile(Path.Combine(otherAssembliesPath, "System.Runtime.dll"));
            MetadataReference codeAnalysis = MetadataReference.CreateFromFile(typeof(SyntaxTree).Assembly.Location);
            MetadataReference csharpCodeAnalysis = MetadataReference.CreateFromFile(typeof(CSharpSyntaxTree).Assembly.Location);

            MetadataReference[] references = { systemPrivateCorelib, systemConsole, systemRuntime, codeAnalysis, csharpCodeAnalysis };

            var cc = CSharpCompilation.Create(Name, new SyntaxTree[] { testClass.SyntaxTree }, references, compilationType == CompilationType.Debug ? Rsln.DebugOptions : Rsln.ReleaseOptions);

            using (var ms = new MemoryStream())
            {
                EmitResult result;
                try
                {
                    result = cc.Emit(ms);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    return new CompileResult(ex, ImmutableArray<Diagnostic>.Empty, null);
                }

                if (!result.Success)
                    return new CompileResult(null, result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToImmutableArray(), null);

                return new CompileResult(null, ImmutableArray<Diagnostic>.Empty, ms.ToArray());
            }
        }

        private delegate void MainMethodInvoke();
        private void Execute(CompileResult compileResult)
        {
            if (compileResult.Assembly == null)
            {
                Console.WriteLine("Got compiler errors:");
                Console.WriteLine(string.Join(Environment.NewLine, compileResult.CompileErrors));
            }
            else
            {
                Assembly asm = Assembly.Load(compileResult.Assembly);
                Type testClassType = asm.GetType(Name);
                MethodInfo mainMethodInfo = testClassType.GetMethod(MainMethodName);
                MainMethodInvoke entryPoint = (MainMethodInvoke)Delegate.CreateDelegate(typeof(MainMethodInvoke), Activator.CreateInstance(testClassType), mainMethodInfo);

                Exception ex = null;
                //TextWriter origOut = Console.Out;

                MemoryStream ms = new MemoryStream();
                //StreamWriter sw = new StreamWriter(Console.s, Encoding.UTF8);

                try
                {
                    //Console.SetOut(sw);
                    entryPoint();
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

    //public class TestClass
    //{

    //}

    //public class TestMainClass : TestClass
    //{

    //}

    //public class TestMethod
    //{

    //}

    //public class TestMainMethod : TestMethod
    //{

    //}
}
