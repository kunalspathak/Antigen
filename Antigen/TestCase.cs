using Antigen.Config;
using Antigen.Tree;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Utils;
using Antigen.Compilation;
using Antigen.Execution;
using static System.Net.Mime.MediaTypeNames;

namespace Antigen
{
    public class TestCase : IDisposable
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
            "Value was either too large or too small for a Decimal.",
            "System.DivideByZeroException: Attempted to divide by zero.",
            "Attempted to divide by zero.",
            "Arithmetic operation resulted in an overflow.",
            "isCandidateVar(fieldVarDsc) == isMultiReg", // https://github.com/dotnet/runtime/issues/85628
            "curSize < maxSplitSize", // https://github.com/dotnet/runtime/issues/91251
        };

        private SyntaxNode testCaseRoot;

        private class UniqueIssueFile
        {
            public readonly int UniqueIssueId;
            public readonly int FileSize;
            public readonly string FileName;
            public int HitCount { get; private set; }

            public UniqueIssueFile(int _uniqueIssueId, int _fileSize, string _fileName, int hitCount)
            {
                UniqueIssueId = _uniqueIssueId;
                FileSize = _fileSize;
                FileName = _fileName;
                HitCount = hitCount;
            }

            public void IncreaseHitCount()
            {
                HitCount++;
            }
        }
        private static readonly ConcurrentDictionary<int, UniqueIssueFile> s_uniqueIssues = new();

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

        private static TestRunner s_testRunner;
        private static RunOptions s_runOptions;

        internal Compiler Compiler { get; private set; }
        internal ConfigOptions Config { get; private set; }
        public string Name { get; private set; }
        public AstUtils AstUtils { get; private set; }
        public bool ContainsVectorData { get; private set; }

        public TestCase(int testId, RunOptions runOptions)
        {
            s_runOptions = runOptions;
            Config = s_runOptions.Configs[PRNG.Next(s_runOptions.Configs.Count)];
            ContainsVectorData = PRNG.Decide(Config.VectorDataProbability);

            if (RuntimeInformation.OSArchitecture == Architecture.X64)
            {
                if (PRNG.Decide(Config.SveMethodsProbability))
                {
                    Config.UseSve = true;
                    ContainsVectorData = true;
                }
            }
            // else
            // {
            //     // local temporary change
            //     Config.UseSve = true;
            //     ContainsVectorData = true;
            // }

            AstUtils = new AstUtils(this, new ConfigOptions(), null);
            Name = "TestClass" + testId;
            s_testRunner = TestRunner.GetInstance(s_runOptions.CoreRun, s_runOptions.OutputDirectory);
            Compiler = new Compiler(s_runOptions.OutputDirectory);
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
//#if UNREACHABLE
                StringBuilder fileContents = new StringBuilder();

                fileContents.AppendLine(testCaseRoot.NormalizeWhitespace().ToFullString());
                fileContents.AppendLine("/*");
                fileContents.AppendLine($"Got {compileResult.CompileErrors.Count()} compiler error(s):");
                foreach (var error in compileResult.CompileErrors)
                {
                   fileContents.AppendLine(error.ToString());
                }
                fileContents.AppendLine("*/");

                string errorFile = Path.Combine(s_runOptions.OutputDirectory, $"{Name}-compile-error.g.cs");
                File.WriteAllText(errorFile, fileContents.ToString());
//#endif
                return compileResult.RoslynException != null ? TestResult.RoslynException : TestResult.CompileError;
            }
#if UNREACHABLE
            else
            {
                string workingFile = Path.Combine(s_runOptions.OutputDirectory, $"{Name}-working.g.cs");
                File.WriteAllText(workingFile, testCaseRoot.ToFullString());
            }
#endif
            bool isx64 = RuntimeInformation.OSArchitecture == Architecture.X64;
            var baselineVariables = EnvVarOptions.BaseLineVars(Config.UseSve && isx64);
            var testVariables = EnvVarOptions.TestVars(includeOsrSwitches: PRNG.Decide(0.3), Config.UseSve && isx64);

            // Execute test first and see if we have any errors/asserts
            //var test = s_testRunner.Execute2(compileResult);
            var test = s_testRunner.Execute(compileResult, testVariables);

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
                foreach (var knownError in _knownDiffs)
                {
                    if (testAssertion.Contains(knownError))
                    {
                        return TheTestResult(compileResult.AssemblyFullPath, TestResult.Pass);
                    }
                }
                SaveTestCase(compileResult.AssemblyFullPath, testCaseRoot, null, null, test, testVariables, testAssertion, $"{Name}-test-assertion");
                return TheTestResult(compileResult.AssemblyFullPath, TestResult.Assertion);
            }
            else
            {
                foreach (string knownError in _knownDiffs)
                {
                    if (test.Contains(knownError))
                    {
                        //SaveTestCase(compileResult.AssemblyFullPath, testCaseRoot, null, null, test, testVariables, "Out of memory", $"{Name}-test-divbyzero");

                        return TheTestResult(compileResult.AssemblyFullPath, test.Contains("System.OverflowException:") ? TestResult.Overflow : TestResult.DivideByZero);
                    }
                }
            }

            if (!PRNG.Decide(s_runOptions.ExecuteBaseline))
            {
                return TheTestResult(compileResult.AssemblyFullPath, TestResult.Pass);
            }

            string baseline = s_testRunner.Execute(compileResult, baselineVariables);

            // If timeout, skip
            if (baseline == "TIMEOUT" || string.IsNullOrEmpty(baseline) || string.IsNullOrEmpty(test))
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
                var unsupportedOperationInBaseline = baseline.Contains("System.PlatformNotSupportedException");
                var unsupportedOperationInTest = test.Contains("System.PlatformNotSupportedException");
                if (unsupportedOperationInBaseline == unsupportedOperationInTest)
                {
                    // Only return mismatch output if both baseline/test contains "not supported" or both doesn't contain this exception.
                    SaveTestCase(compileResult.AssemblyFullPath, testCaseRoot, baseline, baselineVariables, test, testVariables, "OutputMismatch", $"{Name}-output-mismatch");
                    return TheTestResult(compileResult.AssemblyFullPath, TestResult.OutputMismatch);
                }
            }

            return TheTestResult(compileResult.AssemblyFullPath, TestResult.Pass);
        }

        public TestResult Verify2()
        {
            SyntaxTree syntaxTree = testCaseRoot.SyntaxTree; // RslnUtilities.GetValidSyntaxTree(testCaseRoot);
            CompileResult compileResult = Compiler.Compile(syntaxTree, Name);
            ExecuteResult executeResult = s_testRunner.Execute(compileResult);
            if (executeResult.Result == RunOutcome.AssertionFailure)
            {
                var assertionMessage = executeResult.AssertionMessage;
                var parsedAssertion = RslnUtilities.ParseAssertionError(assertionMessage);

                if (!string.IsNullOrEmpty(parsedAssertion))
                {
                    foreach (var knownError in _knownDiffs)
                    {
                        if (parsedAssertion.Contains(knownError))
                        {
                            return TestResult.Pass;
                        }
                    }
                    SaveTestCase(compileResult.AssemblyFullPath, testCaseRoot, null, null, assertionMessage, null, parsedAssertion, $"{Name}-test-assertion");
                    return TestResult.Assertion;
                }
                else
                {
                    foreach (var knownError in _knownDiffs)
                    {
                        if (assertionMessage.Contains(knownError))
                        {
                            //SaveTestCase(compileResult.AssemblyFullPath, testCaseRoot, null, null, test, testVariables, "Out of memory", $"{Name}-test-divbyzero");

                            return assertionMessage.Contains("System.OverflowException:") ? TestResult.Overflow : TestResult.DivideByZero;
                        }
                    }
                }
                return TestResult.Assertion;
            }
            else if (executeResult.Result == RunOutcome.OtherError)
            {
                string errorMessage = executeResult.OtherErrorMessage;
                if (errorMessage.Contains("System.OutOfMemoryException"))
                {
                    return TestResult.OOM;
                }
                if (errorMessage == "Operation is not supported on this platform.")
                {
                    // probably we are running with Altjit.

                    return TestResult.Pass;
                }
                foreach (var knownError in _knownDiffs)
                {
                    if (errorMessage.Contains(knownError))
                    {
                        if (errorMessage.Contains("Attempted to divide by zero"))
                        {
                            return TestResult.DivideByZero;
                        }
                        else if (errorMessage.Contains("overflow") || errorMessage.Contains("too large or too small"))
                        {
                            return TestResult.Overflow;
                        }
                    }
                    return TestResult.OtherError;
                }
                var parsedError = RslnUtilities.ParseAssertionError(errorMessage);
                parsedError = parsedError ?? errorMessage;

                SaveTestCase(compileResult.AssemblyFullPath, testCaseRoot, null, null, executeResult.OtherErrorMessage, null, parsedError, $"{Name}-test-error");
                return TestResult.OtherError;
            }
            else if (executeResult.Result == RunOutcome.OutputMismatch)
            {
                var outputDiff = executeResult.OtherErrorMessage;
                SaveTestCase(compileResult.AssemblyFullPath, testCaseRoot, null, null, outputDiff, null, outputDiff, $"{Name}-test-output-mismatch");
                return TestResult.OutputMismatch;
            }
            else
            {
                return TestResult.Pass;
            }
            // execute
        }

        private TestResult TheTestResult(string assemblyPath, TestResult result)
        {
            try
            {
                File.Delete(assemblyPath);
            }
            catch
            {
                // ignore errors 
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
#if UNREACHABLE
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
            fileContents.AppendLine();
            fileContents.AppendLine();
            fileContents.AppendLine("GH title text:");
            fileContents.AppendLine(failureText);
            fileContents.AppendLine("*/");

            string output = string.IsNullOrEmpty(baselineOutput) ? testOutput : baselineOutput;
            StringBuilder summaryContents = new StringBuilder(output);
            string uniqueIssueDirName = null;
            int assertionHashCode = failureText.GetHashCode();
            string currentReproFile = $"{testFileName}.g.cs";
            lock (Program.s_spinLock)
            {
                UniqueIssueFile uniqueIssueFile;
                if (!s_uniqueIssues.ContainsKey(assertionHashCode))
                {
                    uniqueIssueFile = new UniqueIssueFile(s_uniqueIssues.Count, int.MaxValue, currentReproFile, 0);
                }
                else
                {
                    uniqueIssueFile = s_uniqueIssues[assertionHashCode];
                }
                uniqueIssueFile.IncreaseHitCount();
                summaryContents.AppendLine();
                summaryContents.AppendLine();
                summaryContents.AppendLine($"HitCount: {uniqueIssueFile.HitCount}");


                // Create hash of testAssertion and copy files in respective bucket.
                uniqueIssueDirName = Path.Combine(s_runOptions.OutputDirectory, $"UniqueIssue{uniqueIssueFile.UniqueIssueId}");

                if (!Directory.Exists(uniqueIssueDirName))
                {
                    Directory.CreateDirectory(uniqueIssueDirName);
                }

                File.WriteAllText(Path.Combine(uniqueIssueDirName, "summary.txt"), summaryContents.ToString());

                // Only cache 1 file of smallest possible size.
                if (uniqueIssueFile.FileSize > fileContents.Length)
                {
                    string largerReproFile = Path.Combine(uniqueIssueDirName, uniqueIssueFile.FileName);
                    if (File.Exists(largerReproFile))
                    {
                        File.Delete(largerReproFile);
                    }

                    // Write the smallest file
                    string failFile = Path.Combine(uniqueIssueDirName, currentReproFile);
                    File.WriteAllText(failFile, fileContents.ToString());

                    // Update the file size
                    s_uniqueIssues[assertionHashCode] = new UniqueIssueFile(uniqueIssueFile.UniqueIssueId, fileContents.Length, currentReproFile, uniqueIssueFile.HitCount);
                }
            }
        }

        public void Dispose()
        {
            this.testCaseRoot = null;
            this.Compiler = null;
            GC.Collect();
        }
    }
}
