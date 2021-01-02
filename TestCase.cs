using Antigen.Tree;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Emit;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
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

        private const string MainMethodName = "Main";

        private SyntaxNode testCase;

        //private List<SyntaxNode> classesList;
        //private List<SyntaxNode> methodsList;
        //private List<SyntaxNode> propertiesList;
        //private List<SyntaxNode> fieldsList;

        public string Name { get; private set; }
        public AstUtils AstUtils { get; private set; }

        public TestCase(int testId)
        {
            AstUtils = new AstUtils(this, new ConfigOptions(), null);
            Name = "TestClass" + testId;
        }

        public void Generate()
        {
            UsingDirectiveSyntax usingDirective =
                UsingDirective(IdentifierName("System"))
                .WithUsingKeyword(Token(TriviaList(new[]{
                    Comment("// Licensed to the .NET Foundation under one or more agreements."),
                    Comment("// The .NET Foundation licenses this file to you under the MIT license."),
                    Comment("// See the LICENSE file in the project root for more information."),
                    Comment("//"),
                    Comment("// This file is auto-generated."),
                    Comment("// Seed: " + PRNG.GetSeed()),
                    Comment("//"),
                    }), SyntaxKind.UsingKeyword, TriviaList()));

            ClassDeclarationSyntax klass = new TestClass(this, Name).Generate();

            testCase = CompilationUnit()
                            .WithUsings(SingletonList(usingDirective))
                            .WithMembers(new SyntaxList<MemberDeclarationSyntax>(klass)).NormalizeWhitespace();
        }

        public void CompileAndExecute()
        {
            CompileResult compileResult = Compile(CompilationType.Release);
            Execute(compileResult);
        }

        /// <summary>
        ///     Method to find diff of generated tree vs. roslyn generated tree by parsing the
        ///     generated code.
        /// </summary>
        /// <param name="expected"></param>
        /// <param name="actual"></param>
        private void FindDiff(SyntaxNode expected, SyntaxNode actual)
        {
            if ((expected is LiteralExpressionSyntax) || (actual is LiteralExpressionSyntax))
            {
                // ignore
                return;
            }

            if (!expected.IsEquivalentTo(actual))
            {
                var expectedChildNodes = expected.ChildNodes().ToArray();
                var actualChildNodes = actual.ChildNodes().ToArray();

                int expectedCount = expectedChildNodes.Length;
                int actualCount = actualChildNodes.Length;
                if (expectedCount != actualCount)
                {
                    Debug.Assert(false, $"Child nodes mismatch. Expected= {expected}, Actual= {actual}");
                    return;
                }
                for (int ch = 0; ch < expectedCount; ch++)
                {
                    FindDiff(expectedChildNodes[ch], actualChildNodes[ch]);
                }
                return;
            }
        }

        private CompileResult Compile(CompilationType compilationType)
        {
            string testCaseContents = testCase.ToFullString();
            File.WriteAllText(@$"E:\git\Antigen\{Name}.g.cs", testCaseContents);

            string[] testCaseCode = testCaseContents.Split(Environment.NewLine);
            int lineNum = 1;
            foreach (string code in testCaseCode)
            {
                Console.WriteLine("[{0,4:D4}]{1}", lineNum++, code);
            }

#if DEBUG
            SyntaxTree syntaxTree = testCase.SyntaxTree;
            SyntaxTree expectedTree = CSharpSyntaxTree.ParseText(testCase.ToFullString());
            FindDiff(expectedTree.GetRoot(), syntaxTree.GetRoot());
#else
            // In release, make sure that we didn't end up generating wrong syntax tree,
            // hence parse the text to reconstruct the tree.

            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(testCase.ToFullString());
#endif

            string corelibPath = typeof(object).Assembly.Location;
            string otherAssembliesPath = Path.GetDirectoryName(corelibPath);
            MetadataReference systemPrivateCorelib = MetadataReference.CreateFromFile(corelibPath);
            MetadataReference systemConsole = MetadataReference.CreateFromFile(Path.Combine(otherAssembliesPath, "System.Console.dll"));
            MetadataReference systemRuntime = MetadataReference.CreateFromFile(Path.Combine(otherAssembliesPath, "System.Runtime.dll"));
            MetadataReference codeAnalysis = MetadataReference.CreateFromFile(typeof(SyntaxTree).Assembly.Location);
            MetadataReference csharpCodeAnalysis = MetadataReference.CreateFromFile(typeof(CSharpSyntaxTree).Assembly.Location);

            MetadataReference[] references = { systemPrivateCorelib, systemConsole, systemRuntime, codeAnalysis, csharpCodeAnalysis };

            var cc = CSharpCompilation.Create(Name, new SyntaxTree[] { syntaxTree }, references, compilationType == CompilationType.Debug ? Rsln.DebugOptions : Rsln.ReleaseOptions);

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
                Console.WriteLine($"Got {compileResult.CompileErrors.Length} compiler error(s):");
                Console.WriteLine(string.Join(Environment.NewLine, compileResult.CompileErrors));
                Console.ReadLine();
            }
            else
            {
                Assembly asm = Assembly.Load(compileResult.Assembly);
                Type testClassType = asm.GetType(Name);
                MethodInfo mainMethodInfo = testClassType.GetMethod(MainMethodName);
                Action<string[]> entryPoint = (Action<string[]>)Delegate.CreateDelegate(typeof(Action<string[]>), mainMethodInfo);

                Exception ex = null;
                TextWriter origOut = Console.Out;

                MemoryStream ms = new MemoryStream();
                StreamWriter sw = new StreamWriter(ms, Encoding.UTF8);

                try
                {
                    Console.SetOut(sw);
                    entryPoint(null);
                }
                catch (Exception caughtEx)
                {
                    ex = caughtEx;
                    Console.WriteLine(caughtEx);
                }
                finally
                {
                    Console.SetOut(origOut);
                    sw.Close();
                }

                string stdout = Encoding.UTF8.GetString(ms.ToArray());
                Console.WriteLine(stdout);
                Console.ReadLine();
            }
        }
    }

    internal class CompileResult
    {
        public CompileResult(Exception roslynException, ImmutableArray<Diagnostic> diagnostics, byte[] assembly)
        {
            RoslynException = roslynException;
            List<Diagnostic> errors = new List<Diagnostic>();
            List<Diagnostic> warnings = new List<Diagnostic>();
            foreach (var diag in diagnostics)
            {
                if (diag.Severity == DiagnosticSeverity.Error)
                {
                    errors.Add(diag);
                }
                else if (diag.Severity == DiagnosticSeverity.Warning)
                {
                    errors.Add(diag);
                }
            }
            CompileErrors = errors.ToImmutableArray();
            CompileWarnings = warnings.ToImmutableArray();
            Assembly = assembly;
        }

        public Exception RoslynException { get; }
        public ImmutableArray<Diagnostic> CompileErrors { get; }
        public ImmutableArray<Diagnostic> CompileWarnings { get; }
        public byte[] Assembly { get; }
    }
}
