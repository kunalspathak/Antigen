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
using static System.Net.Mime.MediaTypeNames;

namespace Antigen
{
    public enum TestResult
    {
        CompileError,
        KnownErrors,
        OutputMismatch,
        Assertion,
        Pass,
        OOM
    }

    public class TestCase
    {
        private static int outputDiffHashCode = "Output diff".GetHashCode();
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
        private static Dictionary<int, int> uniqueIssues = new Dictionary<int, int>();

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
            ClassDeclarationSyntax klass = new TestClass(this, Name).Generate();

            testCaseRoot = CompilationUnit()
                            .WithUsings(PreGenerated.UsingDirective.ToSyntaxList())
                            .WithMembers(new SyntaxList<MemberDeclarationSyntax>(klass));
        }

        public TestResult Verify()
        {
            SyntaxTree syntaxTree = testCaseRoot.SyntaxTree; // RslnUtilities.GetValidSyntaxTree(testCaseRoot);

            CompileResult compileResult = TestRunner.Compile(syntaxTree, Name);
            StringBuilder fileContents;
            if (compileResult.AssemblyFullPath == null)
            {
                fileContents = new StringBuilder();

                fileContents.AppendLine(testCaseRoot.NormalizeWhitespace().ToFullString());
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

            // Execute test first and see if we have any errors/asserts
            string test = TestRunner.Execute(compileResult, testVariables);
            string testAssertion = RslnUtilities.ParseAssertionError(test);

            // If OOM, skip
            if (test.Contains("Out of memory"))
            {
                SaveTestCase(testCaseRoot, null, null, test, testVariables, RunOptions.OutputDirectory, $"{Name}-oom");
                return TestResult.OOM;
            }
            // If assertion
            else if (!string.IsNullOrEmpty(testAssertion))
            {
                int assertionHashCode = testAssertion.GetHashCode();
                if (!uniqueIssues.ContainsKey(assertionHashCode))
                {
                    uniqueIssues[assertionHashCode] = uniqueIssues.Count;
                }

                // Create hash of testAssertion and copy files in respective bucket.
                string uniqueIssueDirName = Path.Combine(RunOptions.OutputDirectory, $"UniqueIssue{uniqueIssues[assertionHashCode] }");
                if (!Directory.Exists(uniqueIssueDirName))
                {
                    Directory.CreateDirectory(uniqueIssueDirName);
                }

                SaveTestCase(testCaseRoot, null, null, test, testVariables, uniqueIssueDirName, $"{Name}-fail"); 
                File.Move(compileResult.AssemblyFullPath, Path.Combine(RunOptions.OutputDirectory, $"{Name}-fail.exe"), overwrite: true);

                return TestResult.Assertion;
            }
            else
            {
                foreach (string knownError in knownDiffs)
                {
                    if (test.Contains(knownError))
                    {
                        try
                        {
                            File.Delete(compileResult.AssemblyFullPath);
                        }
                        catch (Exception)
                        {
                            // ignore errors 
                        }

                        //TODO: Temporary - should delete once we fix all the knownerrors.
                        SaveTestCase(testCaseRoot, null, null, test, testVariables, RunOptions.OutputDirectory, $"{Name}-knownerrors");

                        return TestResult.KnownErrors;
                    }
                }
            }


            string baseline = TestRunner.Execute(compileResult, baselineVariables);
            string baselineAssertion = RslnUtilities.ParseAssertionError(baseline);

            // Make sure baseline/test output is same
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

            string outputMismatchDirName = Path.Combine(RunOptions.OutputDirectory, "OutputMismatch");
            if (!Directory.Exists(outputMismatchDirName))
            {
                Directory.CreateDirectory(outputMismatchDirName);
            }

            SaveTestCase(testCaseRoot, baseline, baselineVariables, test, testVariables, outputMismatchDirName, $"{Name}-fail");
            File.Move(compileResult.AssemblyFullPath, Path.Combine(RunOptions.OutputDirectory, $"{Name}-fail.exe"), overwrite: true);

            return TestResult.OutputMismatch;


            //foreach (string knownError in knownDiffs)
            //{
            //    if (baseline.Contains(knownError) && test.Contains(knownError))
            //    {
            //        try
            //        {
            //            File.Delete(compileResult.AssemblyFullPath);
            //        }
            //        catch (Exception)
            //        {
            //            // ignore errors 
            //        }
            //        SaveTestCase(testCaseRoot, baseline, baselineVariables, test, testVariables, RunOptions.OutputDirectory, $"{Name}-knownerrors");

            //        return TestResult.KnownErrors;
            //    }
            //}

            //if (baseline.Contains("Out of memory") || test.Contains("Out of memory"))
            //{
            //    SaveTestCase(testCaseRoot, baseline, baselineVariables, test, testVariables, RunOptions.OutputDirectory, $"{Name}-oom");
            //    return TestResult.OOM;
            //}

            //if ((baseline == test) && string.IsNullOrEmpty(baselineAssertion))
            //{
            //    try
            //    {
            //        File.Delete(compileResult.AssemblyFullPath);
            //    }
            //    catch (Exception)
            //    {
            //        // ignore errors 
            //    }
            //    return TestResult.Pass;
            //}

            //int assertionHashCode = testAssertion == null ? outputDiffHashCode : testAssertion.GetHashCode();
            //if (!uniqueIssues.ContainsKey(assertionHashCode))
            //{
            //    uniqueIssues[assertionHashCode] = uniqueIssues.Count;
            //}

            //// Create hash of testAssertion and copy files in respective bucket.
            //string uniqueIssueDirName = Path.Combine(RunOptions.OutputDirectory, $"UniqueIssue{uniqueIssues[assertionHashCode] }");
            //if (!Directory.Exists(uniqueIssueDirName))
            //{
            //    Directory.CreateDirectory(uniqueIssueDirName);
            //}

            //SaveTestCase(testCaseRoot, baseline, baselineVariables, test, testVariables, uniqueIssueDirName, $"{Name}-fail");

            //File.Move(compileResult.AssemblyFullPath, Path.Combine(RunOptions.OutputDirectory, $"{Name}-fail.exe"), overwrite: true);
            //return string.IsNullOrEmpty(testAssertion) ? TestResult.OutputMismatch : TestResult.Assertion;
        }

        private void SaveTestCase(
            SyntaxNode testCaseRoot,
            string baselineOutput,
            Dictionary<string, string> baselineVars,
            string testOutput,
            Dictionary<string, string> testVars,
            string folderName,
            string testFileName)
        {
            StringBuilder fileContents = new StringBuilder();
            if (baselineVars != null)
            {
                fileContents.AppendLine($"// BaselineVars: {string.Join("|", baselineVars.ToList().Select(x => $"{x.Key}={x.Value}"))}");
            }
            fileContents.AppendLine($"// TestVars: {string.Join("|", testVars.ToList().Select(x => $"{x.Key}={x.Value}"))}");
            fileContents.AppendLine("//");
            fileContents.AppendLine(testCaseRoot.NormalizeWhitespace().ToFullString());
            fileContents.AppendLine("/*");

            fileContents.AppendLine($"Got output diff:");
            fileContents.AppendLine("--------- Baseline ---------");
            fileContents.AppendLine();
            fileContents.AppendLine("Environment:");
            fileContents.AppendLine();
            if (baselineVars != null)
            {
                foreach (var envVars in baselineVars)
                {
                    fileContents.AppendLine($"{envVars.Key}={envVars.Value}");
                }
            }
            fileContents.AppendLine();
            fileContents.AppendLine(baselineOutput);

            fileContents.AppendLine("--------- Test ---------");
            fileContents.AppendLine();
            fileContents.AppendLine("Environment:");
            fileContents.AppendLine();
            foreach (var envVars in testVars)
            {
                fileContents.AppendLine($"{envVars.Key}={envVars.Value}");
}
            fileContents.AppendLine();
            fileContents.AppendLine(testOutput);
            fileContents.AppendLine("*/");

            string failFile = Path.Combine(folderName, $"{testFileName}.g.cs");
            File.WriteAllText(failFile, fileContents.ToString());
        }

    }
}
