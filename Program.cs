using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Antigen.Config;
using Antigen.Trimmer;

namespace Antigen
{
    class Program
    {
        static object SpinLock = new object();
        public static RunOptions RunOptions = new RunOptions();
        private static readonly Dictionary<TestResult, int> s_stats = new Dictionary<TestResult, int>()
        {
            { TestResult.CompileError, 0 },
            { TestResult.Assertion, 0 },
            { TestResult.KnownErrors, 0 },
            {TestResult.OutputMismatch, 0 },
            {TestResult.Pass, 0 },
            {TestResult.OOM, 0 },
        };

        private static int s_testId = 0;

        static void Main(string[] args)
        {
            try
            {
                PRNG.Initialize(RunOptions.Seed);
                Switches.Initialize();
                RunOptions.CoreRun = args[0];

                // trimmer
                if (args.Length > 1)
                {
                    string testCaseToTrim = args[1];
                    TestTrimmer testTrimmer = new TestTrimmer(testCaseToTrim, RunOptions);
                    testTrimmer.Trim();
                    return;
                }

                Parallel.For(0, 8, (p) => RunTest());

            }
            catch (OutOfMemoryException oom)
            {
                Console.WriteLine(oom.Message);
                var myProcess = Process.GetCurrentProcess();
                Console.WriteLine($"  Physical memory usage     : {myProcess.WorkingSet64}");
                Console.WriteLine($"  Base priority             : {myProcess.BasePriority}");
                Console.WriteLine($"  Priority class            : {myProcess.PriorityClass}");
                Console.WriteLine($"  User processor time       : {myProcess.UserProcessorTime}");
                Console.WriteLine($"  Privileged processor time : {myProcess.PrivilegedProcessorTime}");
                Console.WriteLine($"  Total processor time      : {myProcess.TotalProcessorTime}");
                Console.WriteLine($"  Paged system memory size  : {myProcess.PagedSystemMemorySize64}");
                Console.WriteLine($"  Paged memory size         : {myProcess.PagedMemorySize64}");
            }
        }

        private static int GetNextTestId()
        {
            lock (SpinLock)
            {
                return ++s_testId;
            }
        }

        private static void SaveResult(Dictionary<TestResult, int> localStats)
        {
            lock (SpinLock)
            {
                foreach (var resultStat in localStats)
                {
                    s_stats[resultStat.Key] += resultStat.Value;
                }

                if ((s_stats.Count % 50) == 0)
                {
                    Console.Write("** ");
                    foreach (var st in s_stats)
                    {
                        Console.Write($"{Enum.GetName(typeof(TestResult), st.Key)}={st.Value}, ");
                    }
                    Console.WriteLine();
                }
            }
        }

        static void RunTest()
        {
            Dictionary<TestResult, int> localStats = new Dictionary<TestResult, int>()
            {
                { TestResult.CompileError, 0 },
                { TestResult.Assertion, 0 },
                { TestResult.KnownErrors, 0 },
                { TestResult.OutputMismatch, 0 },
                { TestResult.Pass, 0 },
                { TestResult.OOM, 0 },
            };

            while (true)
            {
                int currTestId = GetNextTestId();
                TestCase testCase = new TestCase(currTestId, RunOptions);
                testCase.Generate();
                TestResult result = testCase.Verify();
                Console.WriteLine($"Test# {currTestId} - {Enum.GetName(typeof(TestResult), result)}. {(double)Process.GetCurrentProcess().WorkingSet64 / 1000000} MB ");

                localStats[result]++;
                if (localStats.Count == 10)
                {
                    SaveResult(localStats);
                    localStats.Clear();
                }
            }
        }
    }
}
