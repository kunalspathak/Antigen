﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Antigen.Config;
using CommandLine;
using Utils;

namespace Antigen
{
    class Program
    {
        static readonly object s_spinLock = new object();
        private static readonly RunOptions s_runOptions = RunOptions.Initialize();
        private static readonly Dictionary<TestResult, int> s_stats = new()
        {
            { TestResult.RoslynException, 0 },
            { TestResult.CompileError, 0 },
            { TestResult.Assertion, 0 },
            { TestResult.DivideByZero, 0 },
            { TestResult.Overflow, 0 },
            { TestResult.OutputMismatch, 0 },
            { TestResult.Pass, 0 },
            { TestResult.OOM, 0 },
        };

        private static int s_testId = 0;
        private static readonly DateTime s_startTime = DateTime.Now;

        static int Main(string[] args)
        {
            return Parser.Default.ParseArguments<CommandLineOptions>(args).MapResult(Run, err => 1);
        }

        private static int Run(CommandLineOptions opts)
        {
            try
            {
                PRNG.Initialize(s_runOptions.Seed);
                s_runOptions.CoreRun = opts.CoreRunPath;
                s_runOptions.OutputDirectory = opts.IssuesFolder;
                if (opts.NumTestCases > 0)
                {
                    s_runOptions.NumTestCases = opts.NumTestCases;
                }

                if (opts.RunDuration > 0)
                {
                    s_runOptions.RunDuration = opts.RunDuration;
                }

                if (!File.Exists(s_runOptions.CoreRun))
                {
                    throw new FileNotFoundException($"{s_runOptions.CoreRun} doesn't exist");
                }

                if (!Directory.Exists(s_runOptions.OutputDirectory))
                {
                    Directory.CreateDirectory(s_runOptions.OutputDirectory);
                }

                Parallel.For(0, 2, (p) => RunTest());
                Console.WriteLine($"Executed {s_testId} test cases.");

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
                return 1;
            }
            return 0;
        }

        private static int GetNextTestId()
        {
            lock (s_spinLock)
            {
                return ++s_testId;
            }
        }

        /// <summary>
        ///     Are we done yet?
        /// </summary>
        private static bool Done
        {
            get
            {
                if ((DateTime.Now - s_startTime).Minutes >= s_runOptions.RunDuration)
                {
                    return true;
                }
                if (s_testId >= s_runOptions.NumTestCases)
                {
                    return true;
                }

                return false;
            }
        }

        /// <summary>
        ///     Save the result.
        /// </summary>
        /// <param name="localStats"></param>
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
                { TestResult.DivideByZero, 0 },
                { TestResult.Overflow, 0 },
                { TestResult.OutputMismatch, 0 },
                { TestResult.Pass, 0 },
                { TestResult.OOM, 0 },
            };

            while (!Done)
            {
                var currTestId = GetNextTestId();
                var testCase = new TestCase(currTestId, s_runOptions);
                testCase.Generate();
                var result = testCase.Verify();
                Console.WriteLine("[{4}] Test# {0, -5} [{1, -25}] - {2, -15} {3, -10} MB ",
                    currTestId,
                    testCase.Config.Name,
                    Enum.GetName(typeof(TestResult), result),
                    (double)Process.GetCurrentProcess().WorkingSet64 / 1000000,
                    (DateTime.Now - s_startTime).ToString());


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

    public class CommandLineOptions
    {
        [Option(shortName: 'c', longName: "CoreRun", Required = true, HelpText = "Path to CoreRun/CoreRun.exe.")]
        public string CoreRunPath { get; set; }

        [Option(shortName: 'o', longName: "IssuesFolder", Required = true, HelpText = "Path to folder where issues will be copied.")]
        public string IssuesFolder { get; set; }

        [Option(shortName: 'n', longName: "NumTestCases", Required = false, HelpText = "Number of test cases to execute. By default, 1000.")]
        public int NumTestCases { get; set; }

        [Option(shortName: 'd', longName: "RunDuration", Required = false, HelpText = "Duration in minutes to run. By default until NumTestCases, but if Duration is given, will override the NumTestCases.")]
        public int RunDuration { get; set; }
    }
}