using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Emit;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

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


        private SyntaxGenerator synGen = Rsln.synGen;
        private SyntaxNode testClass;

        private List<SyntaxNode> classesList;
        private List<SyntaxNode> methodsList;
        private List<SyntaxNode> propertiesList;
        private List<SyntaxNode> fieldsList;
        

        public string Name { get; private set; }

        public TestCase(int testId)
        {
            synGen = SyntaxGenerator.GetGenerator(new AdhocWorkspace(), LanguageNames.CSharp);
            Name = string.Format("Test{0:0000}", testId);

            //var x = SyntaxFactory.BinaryExpression(SyntaxKind.AddExpression, SyntaxFactory.IdentifierName("a"), SyntaxFactory.IdentifierName("b"));
            var x = SyntaxFactory.PrefixUnaryExpression(SyntaxKind.UnaryPlusExpression, SyntaxFactory.ParseExpression("a"));
                //(SyntaxKind.AddExpression, SyntaxFactory.IdentifierName("a"), SyntaxFactory.IdentifierName("b"));
            Console.WriteLine(x.ToFullString());
            var ifs = SyntaxFactory.IfStatement(SyntaxFactory.IdentifierName("a"), SyntaxFactory.ParseStatement("1"));
            Console.WriteLine(ifs.ToFullString());
            SyntaxList<StatementSyntax> stmts;
            for (int i = 0; i < 10; i++)
            {
                stmts = stmts.Add(SyntaxFactory.ParseStatement("x = " + i));
            }
            //SyntaxFactory.expre
            var block = SyntaxFactory.Block(stmts);
            ifs = ifs.WithStatement(block);
            Console.WriteLine(ifs.ToFullString());
            //var y = x.WithLeft(SyntaxFactory.IdentifierName("x")).WithRight(SyntaxFactory.IdentifierName("y"));
            //Console.WriteLine(y.ToFullString());
        }

        public void Generate()
        {
            var usingDirectives = synGen.NamespaceImportDeclaration("System");

            List<SyntaxNode> methods = new List<SyntaxNode>();
            for (int i = 0; i < 5; i++)
            {
                methods.Add(synGen.MethodDeclaration("Method" + i));
            }

            //synGen.AddExpression()

            // TODO: Figure out why we cannot have string[] in parameter. The Emit() function fails in its presence.
            var mainArgs = new SyntaxNode[] {
                synGen.ParameterDeclaration("args", /*synGen.ArrayTypeExpression(synGen.TypeExpression(SpecialType.System_String))*/synGen.TypeExpression(SpecialType.System_String)) };

            methods.Add(synGen.MethodDeclaration("Main", parameters: mainArgs, accessibility: Accessibility.Public, modifiers: DeclarationModifiers.Static ));

            var classDefinition = synGen.ClassDeclaration(Name, null, Accessibility.Public, members: methods);

            var namespaceDeclaration = synGen.NamespaceDeclaration("MyTypes", classDefinition);

            
            testClass = synGen.CompilationUnit(usingDirectives, namespaceDeclaration).NormalizeWhitespace();
        }

        public void CompileAndExecute()
        {
            CompileResult compileResult = Compile(CompilationType.Release);
            Execute(compileResult);
        }

        private CompileResult Compile(CompilationType compilationType)
        {
            Console.WriteLine(testClass.ToFullString());

            MetadataReference mscorlib = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
            MetadataReference codeAnalysis = MetadataReference.CreateFromFile(typeof(SyntaxTree).Assembly.Location);
            MetadataReference csharpCodeAnalysis = MetadataReference.CreateFromFile(typeof(CSharpSyntaxTree).Assembly.Location);

            MetadataReference[] references = { mscorlib, codeAnalysis, csharpCodeAnalysis };

            var cc = CSharpCompilation.Create("test000", new SyntaxTree[] { testClass.SyntaxTree }, references, compilationType == CompilationType.Debug ? Rsln.DebugOptions : Rsln.ReleaseOptions);

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
                MethodInfo mainMethodInfo = asm.GetType("MyTypes.Test000").GetMethod("Main");
                Action<string> entryPoint = (Action<string>)Delegate.CreateDelegate(typeof(Action<string>), mainMethodInfo);

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

    public class TestClass
    {

    }

    public class TestMainClass : TestClass
    {

    }

    public class TestMethod
    {

    }

    public class TestMainMethod : TestMethod
    {

    }
}
