namespace Antigen
{
    class Program
    {
        static void Main(string[] args)
        {
            PRNG.Initialize(5);

            //GenerateTestCase();
            int testId = 1;
            //while (true)
            {
                TestCase testCase = new TestCase(testId++);
                testCase.Generate();
                testCase.CompileAndExecute();
            }
        }
    }
}
