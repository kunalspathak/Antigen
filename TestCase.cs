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

        private const string MainMethodName = "Method0";

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

            SyntaxTree syntaxTree = testCase.SyntaxTree;
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
                Console.WriteLine("Got compiler errors:");
                Console.WriteLine(string.Join(Environment.NewLine, compileResult.CompileErrors));
                Console.ReadLine();
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
