// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Antigen.Config;
using Antigen.Trimmer.Rewriters;
using Antigen.Trimmer.Rewriters.Expressions;
using Antigen.Trimmer.Rewriters.Statements;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Antigen.Trimmer
{
    public class TestTrimmer
    {
        RunOptions RunOptions;
        private string _testFileToTrim;
        private static TestRunner _testRunner;
        static int s_iterId = 1;

        public TestTrimmer(string testFileToTrim, RunOptions runOptions)
        {
            RunOptions = runOptions;
            _testFileToTrim = testFileToTrim;
            _testRunner = TestRunner.GetInstance(RunOptions);
        }

        public void Trim()
        {
            var trimTask = Task.Run(TrimTree);
            trimTask.Wait(TimeSpan.FromMinutes(20));
        }

        /// <summary>
        /// 1. Trim as many statements as possible
        /// 2. Trim as many expressions as possible
        /// 3. If anything was trimmed, goto 1.
        /// </summary>
        public void TrimTree()
        {
            bool trimmedAtleastOne = false;
            do
            {
                trimmedAtleastOne |= TrimStatements();
                trimmedAtleastOne |= TrimExpressions();

            } while (trimmedAtleastOne);

        }

        /// <summary>
        ///     Iterate through all the trimmers and trim the tree using them.
        ///     If at least one trimmer could trim the tree successfully, rerun all
        ///     the trimmers until there was no change made to the tree.
        /// </summary>
        /// <returns></returns>
        public bool TrimStatements()
        {
            List<SyntaxRewriter> trimmerList = new List<SyntaxRewriter>()
            {
                // From high to low

                // statements/blocks
                new MethodDeclStmtRemoval(),
                new BlockRemoval(),
                new DoWhileStmtRemoval(),
                new ForStmtRemoval(),
                new WhileStmtRemoval(),
                new IfElseStmtRemoval(),
                new AssignStmtRemoval(),
                new LocalDeclStmtRemoval(),
            };

            bool trimmedAtleastOne = false;
            bool trimmedInCurrIter;

            do
            {
                trimmedInCurrIter = false;
                trimmedInCurrIter |= TrimWithTrimmer(trimmerList);
                trimmedAtleastOne |= trimmedInCurrIter;
            } while (trimmedInCurrIter);

            return trimmedAtleastOne;
        }

        /// <summary>
        ///     Iterate through all the trimmers and trim the tree using them.
        ///     If at least one trimmer could trim the tree successfully, rerun all
        ///     the trimmers until there was no change made to the tree.
        /// </summary>
        /// <returns></returns>
        public bool TrimExpressions()
        {
            List<SyntaxRewriter> trimmerList = new List<SyntaxRewriter>()
            {
                // From high to low

                // expressions
                new CastExprRemoval(),
                new ParenExprRemoval(),
                new BinaryExpRemoval(),
                //new MemberAccessExprRemoval(),
                //new LiteralExprRemoval(),
                //new IdentityNameExprRemoval(),
            };

            bool trimmedAtleastOne = false;
            bool trimmedInCurrIter;

            do
            {
                trimmedInCurrIter = false;
                trimmedInCurrIter |= TrimWithTrimmer(trimmerList);
                trimmedAtleastOne |= trimmedInCurrIter;
            } while (trimmedInCurrIter);

            return trimmedAtleastOne;
        }

        /// <summary>
        /// Trim the test case.
        /// </summary>
        private bool TrimWithTrimmer(List<SyntaxRewriter> trimmerList)
        {
            SyntaxNode recentTree = CSharpSyntaxTree.ParseText(File.ReadAllText(_testFileToTrim)).GetRoot();
            bool trimmedAtleastOne = false;
            bool trimmedInCurrIter;

            do
            {
                trimmedInCurrIter = false;

                //TODO: populate baseline and test envvars:
                Dictionary<string, string> baselineEnvVars = Rsln.BaselineEnvVars;
                Dictionary<string, string> testEnvVars = new Dictionary<string, string>()
                {
                    {"COMPlus_JitStress", "2" },
                    {"COMPlus_JitStressRegs", "0x80" },
                    {"COMPlus_TieredCompilation", "0" }
                };
                TestResult reproTestResult = TestResult.Fail;

                // pick category
                foreach (var trimmer in trimmerList)
                {
                    SyntaxNode treeBeforeTrim = recentTree, treeAfterTrim;

                    // remove all
                    Console.Write($"{s_iterId}. {trimmer.GetType()}");

                    int noOfNodes;

                    // For expression, it can be nested, so first count
                    // total expressions present.
                    if (trimmer is BinaryExpRemoval)
                    {
                        treeAfterTrim = trimmer.Visit(recentTree);
                        noOfNodes = trimmer.TotalVisited;
                        trimmer.RemoveAll();
                        treeAfterTrim = trimmer.Visit(recentTree);
                    }
                    // for statements, count while removing them all.
                    else
                    {
                        trimmer.RemoveAll();

                        treeAfterTrim = trimmer.Visit(recentTree);
                        noOfNodes = trimmer.TotalVisited;
                    }

                    // compile, execute and repro
                    if (trimmer.IsAnyNodeVisited && Verify($"trim{s_iterId++}", treeAfterTrim, baselineEnvVars, testEnvVars) == reproTestResult)
                    {
                        // move to next trimmer
                        recentTree = treeAfterTrim;
                        Console.WriteLine(" - Success");
                        trimmedInCurrIter = true;
                        continue;
                    }
                    else
                    {
                        recentTree = treeBeforeTrim;
                        Console.WriteLine(" - Revert");
                    }


                    trimmer.Reset();
                    trimmer.RemoveOneByOne();

                    int nodeId = 0;
                    int localIterId = 0;
                    while (nodeId < noOfNodes)
                    {
                        Console.Write($"{s_iterId}. {trimmer.GetType()}, localIterId = {localIterId++}");
                        trimmer.Reset();
                        trimmer.UpdateId(nodeId);

                        treeBeforeTrim = recentTree;
                        treeAfterTrim = trimmer.Visit(recentTree);

                        // compile, execute and repro
                        if (trimmer.IsAnyNodeVisited && Verify($"trim{s_iterId++}", treeAfterTrim, baselineEnvVars, testEnvVars) == reproTestResult)
                        {
                            // move to next trimmer
                            recentTree = treeAfterTrim;

                            if (trimmer is BinaryExpRemoval)
                            {
                                nodeId++;
                            }
                            else
                            {
                                // We have just removed a node, so decrease nodes count
                                noOfNodes--;
                            }

                            Console.WriteLine(" - Success");
                            trimmedInCurrIter = true;
                            trimmedAtleastOne = true;
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
            } while (trimmedInCurrIter);

            return trimmedAtleastOne;
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

            string programContents = programRootNode.ToFullString();
            programContents = Regex.Replace(programContents, @"[\r\n]*$", string.Empty, RegexOptions.Multiline);
            fileContents.AppendLine(programContents);
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

            //TODO: Only if something was visited

            string failedFileName = $"{iterId}-lkg";
            string failFile = Path.Combine(@"E:\temp\antigen-trimmer\test2\round8", $"{ failedFileName}.g.cs");
            //string failFile = Path.Combine(RunOptions.OutputDirectory, $"{failedFileName}.g.cs");
            File.WriteAllText(failFile, fileContents.ToString());

            File.Move(compileResult.AssemblyFullPath, Path.Combine(RunOptions.OutputDirectory, $"{failedFileName}.exe"), overwrite: true);
            return TestResult.Fail;
        }
    }
}
