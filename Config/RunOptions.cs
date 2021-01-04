using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Antigen.Config
{
    public class RunOptions : OptionsBase
    {
        // random seed
        public int Seed = -1;

        // sets the output directory for tests
        public string OutputDirectory = "." + Path.DirectorySeparatorChar;

        // Total number of test cases (overrides number specified in each XML config file)
        public long NumTestCases = 0;

        // Duration to execute tests for (overrides number specified in each XML config file)
        public ulong SecondsToRun = 0;
    }
}
