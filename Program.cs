using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Antigen.Config;
using Antigen.Trimmer;

namespace Antigen
{
    class Program
    {
        static readonly object s_spinLock = new object();
        private static readonly RunOptions s_runOptions = RunOptions.Initialize();
        private static readonly Dictionary<TestResult, int> s_stats = new Dictionary<TestResult, int>()
        {
            { TestResult.RoslynException, 0 },
            { TestResult.CompileError, 0 },
            { TestResult.Assertion, 0 },
            { TestResult.KnownErrors, 0 },
            { TestResult.OutputMismatch, 0 },
            { TestResult.Pass, 0 },
            { TestResult.OOM, 0 },
        };

        private static int s_testId = 0;
        private static bool done = false;
        private static readonly DateTime s_StartTime = DateTime.Now;

        static void Main(string[] args)
        {
            try
            {
                PRNG.Initialize(s_runOptions.Seed);
                s_runOptions.CoreRun = args[0];

                if (!File.Exists(s_runOptions.CoreRun))
                {
                    throw new Exception($"{s_runOptions.CoreRun} doesn't exist");
                }

                s_runOptions.OutputDirectory = args[1];
                if (!Directory.Exists(s_runOptions.OutputDirectory))
                {
                    Console.WriteLine($"Creating {s_runOptions.OutputDirectory}");
                    Directory.CreateDirectory(s_runOptions.OutputDirectory);
                }

                //// trimmer
                //if (args.Length > 1)
                //{
                //    string testCaseToTrim = args[1];
                //    TestTrimmer testTrimmer = new TestTrimmer(testCaseToTrim, s_runOptions);
                //    testTrimmer.Trim();
                //    return;
                //}

                Parallel.For(0, 4, (p) => RunTest());

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
            lock (s_spinLock)
            {
                if (s_testId >= s_runOptions.NumTestCases)
                {
                    done = true;
                }
                return ++s_testId;
            }
        }

        private static void SaveResult(Dictionary<TestResult, int> localStats)
        {
            lock (s_spinLock)
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
                { TestResult.RoslynException, 0 },
                { TestResult.CompileError, 0 },
                { TestResult.Assertion, 0 },
                { TestResult.KnownErrors, 0 },
                { TestResult.OutputMismatch, 0 },
                { TestResult.Pass, 0 },
                { TestResult.OOM, 0 },
            };

            while (!done)
            {
                int currTestId = GetNextTestId();
                TestCase testCase = new TestCase(currTestId, s_runOptions);
                testCase.Generate();
                TestResult result = testCase.Verify();
                Console.WriteLine("[{4}] Test# {0, -5} [{1, -25}] - {2, -15} {3, -10} MB ",
                    currTestId,
                    testCase.Config.Name,
                    Enum.GetName(typeof(TestResult), result),
                    (double)Process.GetCurrentProcess().WorkingSet64 / 1000000,
                    (DateTime.Now - s_StartTime).ToString());


                localStats[result]++;
                if (localStats.Count == 10)
                {
                    SaveResult(localStats);
                    localStats.Clear();
                }

                GC.Collect();
            }
        }
    }
}
