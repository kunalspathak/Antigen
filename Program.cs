using System;
using System.Diagnostics;
using Antigen.Config;

namespace Antigen
{
    class Program
    {

        public static RunOptions RunOptions = new RunOptions();

        static void Main(string[] args)
        {
            PRNG.Initialize(RunOptions.Seed);

            int testId = 1;
            while (true)
            {
                TestCase testCase = new TestCase(testId, RunOptions);
                testCase.Generate();
                TestResult result = testCase.Verify();
                Console.WriteLine($"Test# {testId} - {Enum.GetName(typeof(TestResult), result)}");
                testId++;
            }
        }
    }
}
