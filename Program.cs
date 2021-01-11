using System;
using System.Collections.Generic;
using System.Diagnostics;
using Antigen.Config;
using Antigen.Trimmer;

namespace Antigen
{
    class Program
    {
        public static RunOptions RunOptions = new RunOptions();

        static void Main(string[] args)
        {
            PRNG.Initialize(RunOptions.Seed);

            RunOptions.CoreRun = args[0];

            // trimmer
            if (args.Length > 1)
            {
                string testCaseToTrim = args[1];
                TestTrimmer testTrimmer = new TestTrimmer(testCaseToTrim, RunOptions);
                testTrimmer.Trim();
                return;
            }

            int testId = 1;
            Dictionary<TestResult, int> stats = new Dictionary<TestResult, int>()
            {
                { TestResult.CompileError, 0 },
                { TestResult.Fail, 0 },
                {TestResult.OutputMismatch, 0 },
                {TestResult.Pass, 0 },
            };
            while (true)
            {
                TestCase testCase = new TestCase(testId, RunOptions);
                testCase.Generate();
                TestResult result = testCase.Verify();
                stats[result]++;
                Console.Write($"Test# {testId} - {Enum.GetName(typeof(TestResult), result)}. ");
                if ((testId % 100) == 0)
                {
                    foreach (var st in stats)
                    {
                        Console.Write($"{Enum.GetName(typeof(TestResult), st.Key)}={st.Value}, ");
                    }
                }
                Console.WriteLine();
                testId++;
            }
        }
    }
}
