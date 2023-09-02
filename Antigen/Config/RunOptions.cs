using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.Intrinsics;
using Newtonsoft.Json;

namespace Antigen.Config
{
    public class RunOptions
    {
        // random seed
        public int Seed = -1;

        // sets the output directory for tests
        public string OutputDirectory = "." + Path.DirectorySeparatorChar;

        // Total number of test cases (overrides number specified in each XML config file)
        public long NumTestCases;

        // Duration to execute tests for (overrides number specified in each XML config file)
        public int RunDuration;

        // Percent of time to execute baseline
        public double ExecuteBaseline;

        [NonSerialized()]
        public string CoreRun = null;

        public List<ConfigOptions> Configs;

        public List<DotnetEnvVarGroup> BaselineEnvVars;

        public List<DotnetEnvVarGroup> TestEnvVars;

        /// <summary>
        ///     If this run supports Vector64.
        /// </summary>
        public bool SupportsVector64 { get; private set; }

        /// <summary>
        ///      If this run supports Vector128.
        /// </summary>
        public bool SupportsVector128 { get; private set; }

        /// <summary>
        ///     If this run supports Vector256.
        /// </summary>
        public bool SupportsVector256 { get; private set; }

        /// <summary>
        ///     If this run supports Vector512.
        /// </summary>
        public bool SupportsVector512 { get; private set; }


        internal static RunOptions Initialize()
        {
            string currentDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string antiGenConfig = Path.Combine(currentDirectory, "Config", "antigen.json");
            Debug.Assert(File.Exists(antiGenConfig));

            var runOption = JsonConvert.DeserializeObject<RunOptions>(File.ReadAllText(antiGenConfig));
            EnvVarOptions.Initialize(runOption.BaselineEnvVars, runOption.TestEnvVars);

            runOption.SupportsVector64 = Vector64.IsHardwareAccelerated;
            runOption.SupportsVector128 = Vector128.IsHardwareAccelerated;
            runOption.SupportsVector256 = Vector256.IsHardwareAccelerated;
            runOption.SupportsVector512 = Vector512.IsHardwareAccelerated;

            return runOption;
        }
    }
}
