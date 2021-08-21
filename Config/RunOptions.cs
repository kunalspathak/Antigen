using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
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
        public long NumTestCases = 0;

        // Duration to execute tests for (overrides number specified in each XML config file)
        public int HoursToRun = 0;

        [NonSerialized()]
        public string CoreRun = null;

        public List<ConfigOptions> Configs;

        internal static RunOptions Initialize()
        {
            string currentDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string antiGenConfig = Path.Combine(currentDirectory, "Config", "antigen.json");
            Debug.Assert(File.Exists(antiGenConfig));

            return JsonConvert.DeserializeObject<RunOptions>(File.ReadAllText(antiGenConfig));
        }
    }
}
