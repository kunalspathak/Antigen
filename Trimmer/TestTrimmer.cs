// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        RunOptions _runOptions;
        private string _testFileToTrim;
        private static TestRunner _testRunner;
        private Dictionary<string, string> _baselineVariables;
        private Dictionary<string, string> _testVariables;
        private string _originalTestAssertion;
        static int s_iterId = 1;

        public TestTrimmer(string testFileToTrim, RunOptions runOptions)
        {
            if (!File.Exists(testFileToTrim))
            {
                throw new Exception($"{testFileToTrim} doesn't exist.");
            }
            _runOptions = runOptions;
            _testFileToTrim = testFileToTrim;
            _testRunner = TestRunner.GetInstance(_runOptions);

            ParseEnvironment();
        }

        /// <summary>
        ///     Returns a tuple of Baseline, Test environment variables
        /// </summary>
        /// <returns></returns>
        private void ParseEnvironment()
        {
            var fileContents = File.ReadAllText(_testFileToTrim);
            string[] fileContentLines = fileContents.Split(Environment.NewLine);
            _originalTestAssertion = RslnUtilities.ParseAssertionError(fileContents);

            foreach (var line in fileContentLines)
            {
                if (line.StartsWith("// BaselineVars: "))
                {
                    var baselineContents = line.Replace("// BaselineVars: ", string.Empty);
                    _baselineVariables = baselineContents.Split("|").ToList().ToDictionary(x => x.Split("=")[0], x => x.Split("=")[1]);
                    continue;
                }

                else if (line.StartsWith("// TestVars: "))
                {
                    var testContents = line.Replace("// TestVars: ", string.Empty);
                    _testVariables = testContents.Split("|").ToList().ToDictionary(x => x.Split("=")[0], x => x.Split("=")[1]);
                    return;
                }

                throw new Exception("Baseline/TestVars not present.");
            }
            //throw new Exception("Baseline/TestVars not present.");
            //return null;
        }

        public void Trim()
        {
            var trimTask = Task.Run(TrimTree);
            trimTask.Wait(TimeSpan.FromMinutes(40));
        }

        /// <summary>
        /// 1. Trim as many statements as possible
        /// 2. Trim as many expressions as possible
        /// 3. If anything was trimmed, goto 1.
        /// </summary>
        public void TrimTree()
        {
            bool trimmedAtleastOne;
            do
            {
                trimmedAtleastOne = false;
                trimmedAtleastOne |= TrimEnvVars();
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
                new ConsoleLogStmtRemoval(),
                new StructDeclStmtRemoval(),
                new BlockRemoval(),
                new DoWhileStmtRemoval(),
                new ForStmtRemoval(),
                new WhileStmtRemoval(),
                new TryCatchFinallyStmtRemoval(),
                new SwitchStmtRemoval(),
                new IfElseStmtRemoval(),
                new ExprStmtRemoval(),
                new LocalDeclStmtRemoval(),
            };

            bool trimmedAtleastOne = false;
            bool trimmedInCurrIter;

            do
            {
                trimmedInCurrIter = false;
                trimmedInCurrIter |= Trim(trimmerList);
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
                new InvocationExprRemoval(),
                new FieldExprRemoval(),
                new BinaryExpRemoval(),
                new AssignExprRemoval(),
                new MemberAccessExprRemoval(),
                new LiteralExprRemoval(),
                new IdentityNameExprRemoval(),
                new CastExprRemoval(),
                new ParenExprRemoval(),

            };

            //bool trimmedAtleastOne = false;
            //bool trimmedInCurrIter;

            //do
            //{
            //    trimmedInCurrIter = false;
            //    trimmedInCurrIter |= Trim(trimmerList);
            //    trimmedAtleastOne |= trimmedInCurrIter;
            //} while (trimmedInCurrIter);

            //return trimmedAtleastOne;
            return Trim(trimmerList);
        }

        public bool TrimEnvVars()
        {
            bool trimmedAtleastOne = false;
            SyntaxNode recentTree = CSharpSyntaxTree.ParseText(File.ReadAllText(_testFileToTrim)).GetRoot();
            var keys = _testVariables.Keys.ToList();

            foreach(var envVar in keys)
            {
                string value = _testVariables[envVar];

                _testVariables.Remove(envVar);
                Console.Write($"{s_iterId}. Removing {envVar}={value}");
                if (Verify($"trim{s_iterId++}", recentTree, _baselineVariables, _testVariables) == TestResult.Pass)
                {
                    Console.WriteLine(" - Success");
                    _testVariables[envVar] = value;
                }
                else
                {
                    Console.WriteLine(" - Revert");
                    trimmedAtleastOne = true;
                }
            }

            return trimmedAtleastOne;
        }

        /// <summary>
        /// Trim the test case.
        /// </summary>
        private bool Trim(List<SyntaxRewriter> trimmerList)
        {
            SyntaxNode recentTree = CSharpSyntaxTree.ParseText(File.ReadAllText(_testFileToTrim)).GetRoot();
            CompileResult compileResult = _testRunner.Compile(recentTree.SyntaxTree, "base");
            if (compileResult.AssemblyName == null)
            {
                return false;
            }

            bool trimmedAtleastOne = false;
            bool trimmedInCurrIter;
            TestResult expectedResult = string.IsNullOrEmpty(_originalTestAssertion) ? TestResult.OutputMismatch : TestResult.Assertion;

            do
            {
                trimmedInCurrIter = false;

                // pick category
                foreach (var trimmer in trimmerList)
                {
                    SyntaxNode treeBeforeTrim = recentTree, treeAfterTrim = null;

                    // remove all
                    Console.Write($"{s_iterId}. {trimmer.GetType()}");

                    int noOfNodes = 0;
                    bool gotException = false;
                    try
                    {
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
                    }
                    catch (ArgumentNullException)
                    {
                        gotException = true;
                    }

                    bool isSameTree = false;
                    if (!gotException)
                    {
                        treeAfterTrim = RslnUtilities.GetValidSyntaxTree(treeAfterTrim, doValidation: false).GetRoot();
                        isSameTree = treeBeforeTrim.ToFullString() == treeAfterTrim.ToFullString();
                    }

                    // compile, execute and repro
                    if (!gotException &&
                        !isSameTree &&      // If they are same tree
                        trimmer.IsAnyNodeVisited && // We have visited and removed at least one node
                        Verify($"trim{s_iterId++}", treeAfterTrim, _baselineVariables, _testVariables) == expectedResult)
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
                        treeAfterTrim = null;

                        try
                        {
                            treeAfterTrim = trimmer.Visit(recentTree);
                        } catch (ArgumentNullException)
                        {
                            gotException = true;
                        }

                        isSameTree = false;
                        if (!gotException)
                        {
                            treeAfterTrim = RslnUtilities.GetValidSyntaxTree(treeAfterTrim, doValidation: false).GetRoot();
                            isSameTree = treeBeforeTrim.ToFullString() == treeAfterTrim.ToFullString();
                        }

                        // compile, execute and repro
                        if (!gotException &&
                            !isSameTree &&
                            trimmer.IsAnyNodeVisited &&
                            Verify($"trim{s_iterId++}", treeAfterTrim, _baselineVariables, _testVariables) == expectedResult)
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
            bool hasAssertion = !string.IsNullOrEmpty(_originalTestAssertion);
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

            string currRunBaselineOutput = hasAssertion ? string.Empty :_testRunner.Execute(compileResult, EnvVarOptions.BaseLineVars(), 10);
            string currRunTestOutput = _testRunner.Execute(compileResult, testEnvVars, 10);

            TestResult verificationResult = string.IsNullOrEmpty(_originalTestAssertion) ? TestResult.OutputMismatch : TestResult.Assertion;

            if (((currRunBaselineOutput == "TIMEOUT") && (currRunTestOutput == "TIMEOUT")) ||
                (currRunBaselineOutput == currRunTestOutput))
            {
                // If output matches, then the test passes
                verificationResult = TestResult.Pass;
            }
            else if (hasAssertion)
            {
                // Otherwise, if there was an assertion, verify that it is the same assertion
                var currRunTestAssertion = RslnUtilities.ParseAssertionError(currRunTestOutput);
                if (_originalTestAssertion != currRunTestAssertion)
                {
                    // The assertion doesn't match. Consider this as PASS
                    verificationResult = TestResult.Pass;
                }
            }

            foreach (string knownError in knownDiffs)
            {
                if (currRunBaselineOutput.Contains(knownError) && currRunTestOutput.Contains(knownError))
                {
                    verificationResult = TestResult.Pass;
                    break;
                }
            }

            if (verificationResult == TestResult.Pass)
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

            string programContents = programRootNode.ToFullString();
            programContents = Regex.Replace(programContents, @"[\r\n]*$", string.Empty, RegexOptions.Multiline);

            StringBuilder fileContents = new StringBuilder();
            fileContents.AppendLine(programContents);
            fileContents.AppendLine("/*");
            fileContents.AppendLine("Got output diff:");

            fileContents.AppendLine("--------- Baseline ---------  ");
            fileContents.AppendLine();
            fileContents.AppendLine("Environment:");
            fileContents.AppendLine();
            if (baselineEnvVars != null)
            {
                foreach (var envVars in baselineEnvVars)
                {
                    fileContents.AppendFormat("{0}={1}", envVars.Key, envVars.Value).AppendLine();
                }
            }
            fileContents.AppendLine();
            fileContents.AppendLine(currRunBaselineOutput);

            fileContents.AppendLine("--------- Test ---------  ");
            fileContents.AppendLine();
            fileContents.AppendLine("Environment:");
            fileContents.AppendLine();
            foreach (var envVars in testEnvVars)
            {
                fileContents.AppendFormat("{0}={1}", envVars.Key, envVars.Value).AppendLine();
            }
            fileContents.AppendLine();
            fileContents.AppendLine(currRunTestOutput);
            fileContents.AppendLine("*/");

            //TODO: Only if something was visited

            string failedFileName = "repro-lkg";
            string failFile = Path.Combine(Path.GetDirectoryName(_testFileToTrim), $"{ failedFileName}.g.cs");
            //string failFile = Path.Combine(RunOptions.OutputDirectory, $"{failedFileName}.g.cs");
            File.WriteAllText(failFile, fileContents.ToString());
            _testFileToTrim = failFile;

            File.Move(compileResult.AssemblyFullPath, Path.Combine(_runOptions.OutputDirectory, $"{failedFileName}.exe"), overwrite: true);
            return verificationResult;
         }
    }
}
