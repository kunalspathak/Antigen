﻿using Antigen.Config;
using Antigen.Tree;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Utils;

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

        private readonly List<string> _knownDiffs = new List<string>()
        {
            "System.OverflowException: Value was either too large or too small for a Decimal.",
            "System.DivideByZeroException: Attempted to divide by zero.",
        };

        private SyntaxNode testCaseRoot;
        private static readonly Dictionary<int, int> s_uniqueIssues = new();

        internal IList<Weights<int>> _numerals = new List<Weights<int>>()
        {
            new Weights<int>(int.MinValue, (double) PRNG.Next(1, 10) / 10000 ),
            new Weights<int>(int.MinValue + 1, (double)PRNG.Next(1, 10) / 10000 ),
            new Weights<int>(PRNG.Next(-100, -6), (double) PRNG.Next(1, 10) / 1000 ),
            new Weights<int>(-5, (double) PRNG.Next(1, 10) / 1000 ),
            new Weights<int>(-2, (double) PRNG.Next(1, 10) / 1000 ),
            new Weights<int>(-1, (double) PRNG.Next(1, 10) / 1000 ),
            new Weights<int>(0, (double) PRNG.Next(1, 10) / 1000 ),
            new Weights<int>(1, (double) PRNG.Next(1, 10) / 1000 ),
            new Weights<int>(2, (double) PRNG.Next(1, 10) / 1000 ),
            new Weights<int>(5, (double) PRNG.Next(1, 10) / 1000 ),
            new Weights<int>(PRNG.Next(6, 100), (double) PRNG.Next(1, 10) / 1000 ),
            new Weights<int>(int.MaxValue - 1, (double) PRNG.Next(1, 10) / 10000 ),
            new Weights<int>(int.MaxValue, (double) PRNG.Next(1, 10) / 10000 ),
        };

        //private List<SyntaxNode> classesList;
        //private List<SyntaxNode> methodsList;
        //private List<SyntaxNode> propertiesList;
        //private List<SyntaxNode> fieldsList;

        private static TestRunner s_testRunner;
        private static RunOptions s_runOptions;

        internal ConfigOptions Config { get; private set; }
        public string Name { get; private set; }
        public AstUtils AstUtils { get; private set; }

        public TestCase(int testId, RunOptions runOptions)
        {
            s_runOptions = runOptions;
            Config = s_runOptions.Configs[PRNG.Next(s_runOptions.Configs.Count)];

            AstUtils = new AstUtils(this, new ConfigOptions(), null);
            Name = "TestClass" + testId;
            s_testRunner = TestRunner.GetInstance(s_runOptions.CoreRun, s_runOptions.OutputDirectory);
        }

        public void Generate()
        {
            var klass = new TestClass(this, PreGenerated.MainClassName).Generate();
            var finalCode = PreGenerated.UsingDirective + klass.ToString();

            testCaseRoot = CSharpSyntaxTree.ParseText(finalCode).GetRoot();
        }

        public TestResult Verify()
        {
            SyntaxTree syntaxTree = testCaseRoot.SyntaxTree; // RslnUtilities.GetValidSyntaxTree(testCaseRoot);

            CompileResult compileResult = s_testRunner.Compile(syntaxTree, Name);
            if (compileResult.AssemblyFullPath == null)
            {
                // StringBuilder fileContents = new StringBuilder();

                //fileContents.AppendLine(testCaseRoot.NormalizeWhitespace().ToFullString());
                //fileContents.AppendLine("/*");
                //fileContents.AppendLine($"Got {compileResult.CompileErrors.Length} compiler error(s):");
                //foreach (var error in compileResult.CompileErrors)
                //{
                //    fileContents.AppendLine(error.ToString());
                //}
                //fileContents.AppendLine("*/");

                //string errorFile = Path.Combine(s_runOptions.OutputDirectory, $"{Name}-compile-error.g.cs");
                //File.WriteAllText(errorFile, fileContents.ToString());
                return compileResult.RoslynException != null ? TestResult.RoslynException : TestResult.CompileError;
            }
#if UNREACHABLE
            else
            {
                string workingFile = Path.Combine(s_runOptions.OutputDirectory, $"{Name}-working.g.cs");
                File.WriteAllText(workingFile, testCaseRoot.ToFullString());
            }
#endif
            var baselineVariables = EnvVarOptions.BaseLineVars();
            var testVariables = EnvVarOptions.TestVars();

            // Execute test first and see if we have any errors/asserts
            var test = s_testRunner.Execute(compileResult, testVariables, 30);

            // If timeout, skip
            if (test == "TIMEOUT")
            {
                return TheTestResult(compileResult.AssemblyFullPath, TestResult.Pass);
            }

            // If OOM, skip
            else if (test.Contains("Out of memory"))
            {
#if UNREACHABLE
                SaveTestCase(compileResult.AssemblyFullPath, testCaseRoot, null, null, test, testVariables, "Out of memory", $"{Name}-test-oom");

#endif
                return TheTestResult(compileResult.AssemblyFullPath, TestResult.OOM);
            }

            var testAssertion = RslnUtilities.ParseAssertionError(test);

            // If test assertion
            if (!string.IsNullOrEmpty(testAssertion))
            {
                SaveTestCase(compileResult.AssemblyFullPath, testCaseRoot, null, null, test, testVariables, testAssertion, $"{Name}-test-assertion");
                return TheTestResult(compileResult.AssemblyFullPath, TestResult.Assertion);
            }
            else
            {
                foreach (string knownError in _knownDiffs)
                {
                    if (test.Contains(knownError))
                    {
                        return TheTestResult(compileResult.AssemblyFullPath, test.StartsWith("System.OverflowException:") ? 
                            TestResult.Overflow : TestResult.DivideByZero);
                    }
                }
            }

            string baseline = s_testRunner.Execute(compileResult, baselineVariables, 30);

            // If timeout, skip
            if (baseline == "TIMEOUT")
            {
                return TheTestResult(compileResult.AssemblyFullPath, TestResult.Pass);
            }

            // If OOM, ignore this diff
            else if (baseline.Contains("Out of memory"))
            {
#if UNREACHABLE
                SaveTestCase(compileResult.AssemblyFullPath, testCaseRoot, baseline, baselineVariables, null, null, "Out of memory", $"{Name}-base-oom"); ;
#endif
                return TheTestResult(compileResult.AssemblyFullPath, TestResult.OOM);
            }

            string baselineAssertion = RslnUtilities.ParseAssertionError(baseline);

            // Is there assertion in baseline?
            if (!string.IsNullOrEmpty(baselineAssertion))
            {

                SaveTestCase(compileResult.AssemblyFullPath, testCaseRoot, baseline, baselineVariables, null, null, baselineAssertion, $"{Name}-base-assertion");
                return TheTestResult(compileResult.AssemblyFullPath, TestResult.Assertion);
            }
            // If baseline and test output doesn't match
            else if (baseline != test)
            {
                SaveTestCase(compileResult.AssemblyFullPath, testCaseRoot, baseline, baselineVariables, test, testVariables, "OutputMismatch", $"{ Name}-output-mismatch");
                return TheTestResult(compileResult.AssemblyFullPath, TestResult.OutputMismatch);
            }

            return TheTestResult(compileResult.AssemblyFullPath, TestResult.Pass);
        }

        private TestResult TheTestResult(string assemblyPath, TestResult result)
        {
            try
            {
                File.Delete(assemblyPath);
            }
            catch (Exception ex)
            {
                // ignore errors 
                Console.WriteLine($"Error deleting {assemblyPath} : {ex}");
            }
            return result;
        }

        private void SaveTestCase(
            string assemblyPath,
            SyntaxNode testCaseRoot,
            string baselineOutput,
            Dictionary<string, string> baselineVars,
            string testOutput,
            Dictionary<string, string> testVars,
            string failureText,
            string testFileName)
        {

            string output = string.IsNullOrEmpty(baselineOutput) ? testOutput : baselineOutput;
            string uniqueIssueDirName = null;
            int assertionHashCode = failureText.GetHashCode();
            lock (this)
            {
                if (!s_uniqueIssues.ContainsKey(assertionHashCode))
                {
                    s_uniqueIssues[assertionHashCode] = s_uniqueIssues.Count;
                }

                // Create hash of testAssertion and copy files in respective bucket.
                uniqueIssueDirName = Path.Combine(s_runOptions.OutputDirectory, $"UniqueIssue{s_uniqueIssues[assertionHashCode] }");
                if (!Directory.Exists(uniqueIssueDirName))
                {
                    Directory.CreateDirectory(uniqueIssueDirName);
                    File.WriteAllText(Path.Combine(uniqueIssueDirName, "summary.txt"), output);
                }
            }
#if DEBUG
            File.Move(assemblyPath, Path.Combine(s_runOptions.OutputDirectory, $"{Name}-fail.exe"), overwrite: true);
#endif

            StringBuilder fileContents = new StringBuilder();
            if (baselineVars != null)
            {
                fileContents.AppendLine($"// BaselineVars: {string.Join("|", baselineVars.ToList().Select(x => $"{x.Key}={x.Value}"))}");
            }
            if (testVars != null)
            {
                fileContents.AppendLine($"// TestVars: {string.Join("|", testVars.ToList().Select(x => $"{x.Key}={x.Value}"))}");
            }
            fileContents.AppendLine("//");
            fileContents.AppendLine(testCaseRoot.NormalizeWhitespace().SyntaxTree.GetText().ToString());
            fileContents.AppendLine("/*");

            fileContents.AppendFormat("Config: {0}", Config.Name).AppendLine();
            fileContents.AppendLine("--------- Baseline ---------");
            fileContents.AppendLine();
            fileContents.AppendLine("Environment:");
            fileContents.AppendLine();
            if (baselineVars != null)
            {
                foreach (var envVars in baselineVars)
                {
                    fileContents.AppendFormat("{0}={1}", envVars.Key, envVars.Value).AppendLine();
                }
            }
            fileContents.AppendLine();
            fileContents.AppendLine(baselineOutput);

            fileContents.AppendLine("--------- Test ---------");
            fileContents.AppendLine();
            fileContents.AppendLine("Environment:");
            fileContents.AppendLine();
            if (testVars != null)
            {
                foreach (var envVars in testVars)
                {
                    fileContents.AppendFormat("{0}={1}", envVars.Key, envVars.Value).AppendLine();
                }
            }
            fileContents.AppendLine();
            fileContents.AppendLine(testOutput);
            fileContents.AppendLine("*/");

            string failFile = Path.Combine(uniqueIssueDirName, $"{testFileName}.g.cs");
            File.WriteAllText(failFile, fileContents.ToString());
        }

    }
}