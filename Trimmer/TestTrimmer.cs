// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Antigen.Config;
using Antigen.Trimmer.Rewriters;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Antigen.Trimmer
{
    public class TestTrimmer
    {
        RunOptions RunOptions;
        private string _testFileToTrim;
        private static TestRunner _testRunner;

        public TestTrimmer(string testFileToTrim, RunOptions runOptions)
        {
            RunOptions = runOptions;
            _testFileToTrim = testFileToTrim;
            _testRunner = TestRunner.GetInstance(RunOptions);
        }

        public void Trim()
        {
            var treeRoot = CSharpSyntaxTree.ParseText(File.ReadAllText(_testFileToTrim)).GetRoot();


            //TODO: populate baseline and test envvars:
            Dictionary<string, string> baselineEnvVars = Rsln.BaselineEnvVars;
            Dictionary<string, string> testEnvVars = new Dictionary<string, string>()
            {
                {"COMPlus_JitStress", "2" },
                {"COMPlus_JitStressRegs", "0x10000" },
                {"COMPlus_TieredCompilation", "0" }
            };
            TestResult reproTestResult = TestResult.Fail;

            //IfElseStmtRemoval ifElse = new IfElseStmtRemoval();
            //ifElse.UpdateId(0);
            //var newRoot = ifElse.Visit(treeRoot);
            //File.WriteAllText(@"E:\temp\antigen-trimmer\1.trim.g.cs", newRoot.ToFullString());

            List<SyntaxRewriter> trimmerList = new List<SyntaxRewriter>()
            {
                new DoWhileStmtRemoval(),
                new ForStmtRemoval(),
                new WhileStmtRemoval(),
                new IfElseStmtRemoval(),
                new AssignStmtRemoval(),
                new LocalDeclStmtRemoval(),
            };

            // pick category
            SyntaxNode recentTree = treeRoot;
            int iterId = 0;
            foreach (var trimmer in trimmerList)
            {
                SyntaxNode treeBeforeTrim = recentTree;

                // remove all
                Console.Write($"{iterId}. {trimmer.GetType()}");

                trimmer.RemoveAll();
                SyntaxNode treeAfterTrim = trimmer.Visit(recentTree);

                //repros = Verify(treeAfterTrim, baselineEnvVars, testEnvVars) == reproTestResult;

                // compile, execute and repro
                if (Verify($"trim{iterId++}", treeAfterTrim, baselineEnvVars, testEnvVars) == reproTestResult)
                {
                    // move to next trimmer
                    recentTree = treeAfterTrim;
                    Console.WriteLine(" - Success");
                    continue;
                }
                else
                {
                    recentTree = treeBeforeTrim;
                    Console.WriteLine(" - Revert");
                }

                int noOfNodes = trimmer.TotalVisited;

                trimmer.Reset();
                trimmer.RemoveOneByOne();

                int nodeId = 0;
                while (nodeId < noOfNodes)
                {
                    Console.Write($"{iterId}. {trimmer.GetType()}, nodeId = {nodeId}");
                    trimmer.Reset();
                    trimmer.UpdateId(nodeId);
                    treeAfterTrim = trimmer.Visit(recentTree);

                    // compile, execute and repro
                    if (Verify($"trim{iterId++}", treeAfterTrim, baselineEnvVars, testEnvVars) == reproTestResult)
                    {
                        // move to next trimmer
                        recentTree = treeAfterTrim;

                        // We have just removed a node, so decrease nodes count
                        noOfNodes--;

                        Console.WriteLine(" - Success");

                    }
                    else
                    {
                        recentTree = treeBeforeTrim;

                        // Go to next nodeId
                        nodeId++;

                        Console.WriteLine(" - Revert");
                    }
                }
            }
        }

        private List<string> knownDiffs = new List<string>()
        {
            "System.OverflowException: Value was either too large or too small for a Decimal.",
            "System.DivideByZeroException: Attempted to divide by zero.",
        };

        //TODO: refactor and merge with TestCase's Verify
        private TestResult Verify(string iterId, SyntaxNode programRootNode, Dictionary<string, string> baselineEnvVars, Dictionary<string, string> testEnvVars/*, bool skipBaseline*/)
        {
            StringBuilder fileContents = new StringBuilder();
            CompileResult compileResult = _testRunner.Compile(programRootNode.SyntaxTree, iterId);
            if (compileResult.AssemblyFullPath == null)
            {
                return TestResult.CompileError;
            }
            //else
            //{
            //    string workingFile = Path.Combine(RunOptions.OutputDirectory, $"{Name}-working.g.cs");
            //    File.WriteAllText(workingFile, testCaseRoot.ToFullString());
            //}

            string baseline = _testRunner.Execute(compileResult, Rsln.BaselineEnvVars);
            string test = _testRunner.Execute(compileResult, testEnvVars);

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

            foreach (string knownError in knownDiffs)
            {
                if (baseline.Contains(knownError) && test.Contains(knownError))
                {
                    return TestResult.Pass;
                }
            }

            fileContents.AppendLine(programRootNode.ToFullString());
            fileContents.AppendLine("/*");
            fileContents.AppendLine($"Got output diff:");

            fileContents.AppendLine("--------- Baseline ---------  ");
            fileContents.AppendLine();
            fileContents.AppendLine("Environment:");
            fileContents.AppendLine();
            foreach (var envVars in baselineEnvVars)
            {
                fileContents.AppendLine($"{envVars.Key}={envVars.Value}");
            }
            fileContents.AppendLine();
            fileContents.AppendLine(baseline);

            fileContents.AppendLine("--------- Test ---------  ");
            fileContents.AppendLine();
            fileContents.AppendLine("Environment:");
            fileContents.AppendLine();
            foreach (var envVars in testEnvVars)
            {
                fileContents.AppendLine($"{envVars.Key}={envVars.Value}");
            }
            fileContents.AppendLine();
            fileContents.AppendLine(test);
            fileContents.AppendLine("*/");

            string failedFileName = $"{iterId}-lkg";
            string failFile = Path.Combine(RunOptions.OutputDirectory, $"{failedFileName}.g.cs");
            File.WriteAllText(failFile, fileContents.ToString());

            File.Move(compileResult.AssemblyFullPath, Path.Combine(RunOptions.OutputDirectory, $"{failedFileName}.exe"), overwrite: true);
            return TestResult.Fail;
        }
    }
}
