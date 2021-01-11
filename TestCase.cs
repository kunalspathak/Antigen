using Antigen.Config;
using Antigen.Tree;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Emit;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Text;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Antigen
{
    public enum TestResult
    {
        CompileError,
        OutputMismatch,
        Fail,
        Pass
    }

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

        private List<string> knownDiffs = new List<string>()
        {
            "System.OverflowException: Value was either too large or too small for a Decimal.",
            "System.DivideByZeroException: Attempted to divide by zero.",
        };

        private SyntaxNode testCaseRoot;

        //private List<SyntaxNode> classesList;
        //private List<SyntaxNode> methodsList;
        //private List<SyntaxNode> propertiesList;
        //private List<SyntaxNode> fieldsList;

        private static TestRunner TestRunner;
        private static RunOptions RunOptions;
        public string Name { get; private set; }
        public AstUtils AstUtils { get; private set; }

        public TestCase(int testId, RunOptions runOptions)
        {
            RunOptions = runOptions;
            AstUtils = new AstUtils(this, new ConfigOptions(), null);
            Name = "TestClass" + testId;
            TestRunner = TestRunner.GetInstance(RunOptions);
        }

        public void Generate()
        {
            List<UsingDirectiveSyntax> usingDirective =
                new List<UsingDirectiveSyntax>()
                {
                    UsingDirective(IdentifierName("System"))
                    .WithUsingKeyword(Token(TriviaList(new[]{
                    Comment("// Licensed to the .NET Foundation under one or more agreements."),
                    Comment("// The .NET Foundation licenses this file to you under the MIT license."),
                    Comment("// See the LICENSE file in the project root for more information."),
                    Comment("//"),
                    Comment("// This file is auto-generated."),
                    Comment("// Seed: " + PRNG.GetSeed()),
                    Comment("//"),
                    }), SyntaxKind.UsingKeyword, TriviaList())),
                    UsingDirective(
                        QualifiedName(
                            QualifiedName(
                                IdentifierName("System"),
                                IdentifierName("Runtime")),
                            IdentifierName("CompilerServices")))
                };

            ClassDeclarationSyntax klass = new TestClass(this, Name).Generate();

            testCaseRoot = CompilationUnit()
                            .WithUsings(new SyntaxList<UsingDirectiveSyntax>(usingDirective))
                            .WithMembers(new SyntaxList<MemberDeclarationSyntax>(klass)).NormalizeWhitespace();
        }

        public TestResult Verify()
        {
#if DEBUG
            SyntaxTree syntaxTree = testCaseRoot.SyntaxTree;
            SyntaxTree expectedTree = CSharpSyntaxTree.ParseText(testCaseRoot.ToFullString());
            FindTreeDiff(expectedTree.GetRoot(), syntaxTree.GetRoot());
#else
            // In release, make sure that we didn't end up generating wrong syntax tree,
            // hence parse the text to reconstruct the tree.

            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(testCaseRoot.ToFullString());
#endif

            StringBuilder fileContents = new StringBuilder();
            CompileResult compileResult = TestRunner.Compile(syntaxTree, Name);
            if (compileResult.AssemblyFullPath == null)
            {
                fileContents.AppendLine(testCaseRoot.ToFullString());
                fileContents.AppendLine("/*");
                fileContents.AppendLine($"Got {compileResult.CompileErrors.Length} compiler error(s):");
                foreach (var error in compileResult.CompileErrors)
                {
                    fileContents.AppendLine(error.ToString());
                }
                fileContents.AppendLine("*/");

                string errorFile = Path.Combine(RunOptions.OutputDirectory, $"{Name}-compile-error.g.cs");
                File.WriteAllText(errorFile, fileContents.ToString());

                return TestResult.CompileError;
            }
            //else
            //{
            //    string workingFile = Path.Combine(RunOptions.OutputDirectory, $"{Name}-working.g.cs");
            //    File.WriteAllText(workingFile, testCaseRoot.ToFullString());
            //}

            string baseline = TestRunner.Execute(compileResult, Rsln.BaselineEnvVars);

            var selectedVars = Rsln.TestEnvVars[PRNG.Next(Rsln.TestEnvVars.Count)].Vars;
            var testEnvVariables = new Dictionary<string, string>();
            foreach (var commonVars in Rsln.CommonTestEnvVars)
            {
                testEnvVariables.Add(commonVars.Key, commonVars.Value);
            }
            foreach (var selectedVar in selectedVars)
            {
                // override the COMPlus_TieredCompilation variable
                testEnvVariables[selectedVar.Key] = selectedVar.Value;
            }

            string test = TestRunner.Execute(compileResult, testEnvVariables);

            if (baseline == test)
            {
                try
                {
                    File.Delete(compileResult.AssemblyFullPath);
                }
                catch (Exception)
                {
                    // ignore errors 
                }
                return TestResult.Pass;
            }

            bool isKnownError = false;
            foreach (string knownError in knownDiffs)
            {
                if (baseline.Contains(knownError) && test.Contains(knownError))
                {
                    isKnownError = true;
                    break;
                }
            }

            fileContents.AppendLine(testCaseRoot.ToFullString());
            fileContents.AppendLine("/*");

            if (isKnownError)
            {
                fileContents.AppendLine($"Got known error mismatch:");
            }
            else
            {
                fileContents.AppendLine($"Got output diff:");
            }
            fileContents.AppendLine("--------- Baseline ---------  ");
            fileContents.AppendLine();
            fileContents.AppendLine("Environment:");
            fileContents.AppendLine();
            foreach (var envVars in Rsln.BaselineEnvVars)
            {
                fileContents.AppendLine($"{envVars.Key}={envVars.Value}");
            }
            fileContents.AppendLine();
            fileContents.AppendLine(baseline);

            fileContents.AppendLine("--------- Test ---------  ");
            fileContents.AppendLine();
            fileContents.AppendLine("Environment:");
            fileContents.AppendLine();
            foreach (var envVars in testEnvVariables)
            {
                fileContents.AppendLine($"{envVars.Key}={envVars.Value}");
            }
            fileContents.AppendLine();
            fileContents.AppendLine(test);
            fileContents.AppendLine("*/");

            //TODO- for now, delete known error files
            if (!isKnownError)
            {
                string failedFileName = $"{Name}-{(isKnownError ? "known-error" : "fail")}";
                string failFile = Path.Combine(RunOptions.OutputDirectory, $"{failedFileName}.g.cs");
                File.WriteAllText(failFile, fileContents.ToString());

                File.Move(compileResult.AssemblyFullPath, Path.Combine(RunOptions.OutputDirectory, $"{failedFileName}.exe"), overwrite: true);
                return TestResult.Fail;
            }
            else
            {
                try
                {
                    File.Delete(compileResult.AssemblyFullPath);
                }
                catch (Exception)
                {
                    // ignore errors 
                }
                return TestResult.OutputMismatch;
            }
        }

        /// <summary>
        ///     Method to find diff of generated tree vs. roslyn generated tree by parsing the
        ///     generated code.
        /// </summary>
        /// <param name="expected"></param>
        /// <param name="actual"></param>
        private void FindTreeDiff(SyntaxNode expected, SyntaxNode actual)
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
                    FindTreeDiff(expectedChildNodes[ch], actualChildNodes[ch]);
                }
                return;
            }
        }
    }
}
