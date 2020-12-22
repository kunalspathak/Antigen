﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Editing;
using System;

namespace Antigen
{
    class Program
    {
        static void Main(string[] args)
        {



            //GenerateTestCase();
            int testId = 1;
            while (true)
            {
                TestCase testCase = new TestCase(testId++);
                testCase.Generate();
                testCase.CompileAndExecute();
            }
        }
    }
}
