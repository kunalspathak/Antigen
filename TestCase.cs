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
using System.Runtime.CompilerServices;
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
                            .WithUsings(usingDirective.ToSyntaxList())
                            .WithMembers(new SyntaxList<MemberDeclarationSyntax>(klass)).NormalizeWhitespace();
        }

        public TestResult Verify()
        {
            SyntaxTree syntaxTree = RslnUtilities.GetValidSyntaxTree(testCaseRoot);

            CompileResult compileResult = TestRunner.Compile(syntaxTree, Name);
            if (compileResult.AssemblyFullPath == null)
            {
                StringBuilder fileContents = new StringBuilder();

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

            var baselineVariables = Switches.BaseLineVars();
            var testVariables = Switches.TestVars();

            string baseline = TestRunner.Execute(compileResult, baselineVariables);
            string test = TestRunner.Execute(compileResult, testVariables);

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

            //TODO- for now, delete known error files
            if (!isKnownError)
            {
                StringBuilder fileContents = new StringBuilder();
                fileContents.AppendLine($"// BaselineVars: {string.Join("|", baselineVariables.ToList().Select(x => $"{x.Key}={x.Value}"))}");
                fileContents.AppendLine($"// TestVars: {string.Join("|", testVariables.ToList().Select(x => $"{x.Key}={x.Value}"))}");
                fileContents.AppendLine("//");
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
                fileContents.AppendLine("--------- Baseline ---------");
                fileContents.AppendLine();
                fileContents.AppendLine("Environment:");
                fileContents.AppendLine();
                foreach (var envVars in baselineVariables)
                {
                    fileContents.AppendLine($"{envVars.Key}={envVars.Value}");
                }
                fileContents.AppendLine();
                fileContents.AppendLine(baseline);

                fileContents.AppendLine("--------- Test ---------");
                fileContents.AppendLine();
                fileContents.AppendLine("Environment:");
                fileContents.AppendLine();
                foreach (var envVars in testVariables)
                {
                    fileContents.AppendLine($"{envVars.Key}={envVars.Value}");
                }
                fileContents.AppendLine();
                fileContents.AppendLine(test);
                fileContents.AppendLine("*/");

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


    }
}
