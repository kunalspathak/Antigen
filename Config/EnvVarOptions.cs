// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Antigen.Config
{
    public static class EnvVarOptions
    {
        internal static readonly CSharpCompilationOptions CompileOptions = new CSharpCompilationOptions(OutputKind.ConsoleApplication, concurrentBuild: false, optimizationLevel: OptimizationLevel.Release/*, mainTypeName: "Main"*/);

        private static List<ComplusEnvVarGroup> s_baselineGroups;
        private static readonly List<Weights<ComplusEnvVarGroup>> s_baselineGroupWeight = new();

        private static List<ComplusEnvVarGroup> s_testGroups;
        private static readonly List<Weights<ComplusEnvVarGroup>> s_testGroupWeight = new();

        internal static void Initialize(List<ComplusEnvVarGroup> baselineEnvVars, List<ComplusEnvVarGroup> testEnvVars)
        {
            s_baselineGroups = baselineEnvVars;
            foreach (var base_group in s_baselineGroups)
            {
                s_baselineGroupWeight.Add(new Weights<ComplusEnvVarGroup>(base_group, base_group.Weight));
                base_group.PopulateWeights();
            }

            s_testGroups = testEnvVars;
            foreach (var test_group in s_testGroups)
            {
                s_testGroupWeight.Add(new Weights<ComplusEnvVarGroup>(test_group, test_group.Weight));
                test_group.PopulateWeights();
            }
        }

        /// <summary>
        ///     Returns a random EnvVarGroup depending on the weight.
        /// </summary>
        /// <returns></returns>
        private static ComplusEnvVarGroup GetRandomTestGroup()
        {
            return PRNG.WeightedChoice(s_testGroupWeight);
        }

        /// <summary>
        ///     Returns random set of baseline environment variables.
        /// </summary>
        /// <returns></returns>
        public static Dictionary<string, string> BaseLineVars()
        {
            var envVars = new Dictionary<string, string>();
            foreach (var group in s_baselineGroups)
            {
                foreach (var variable in group.Variables)
                {
                    envVars[$"COMPlus_{variable.Name}"] = variable.Values[PRNG.Next(variable.Values.Length)];
                }
            }
            return envVars;
        }

        /// <summary>
        ///     Returns random set of test environment variables.
        /// </summary>
        /// <returns></returns>
        public static Dictionary<string, string> TestVars()
        {
            var envVars = new Dictionary<string, string>()
            {
                { "COMPlus_TieredCompilation", "0"}
            };

            var defaultGroup = s_testGroups.First(tg => tg.Name == "Default");
            // default variables
            var usedEnvVars = new HashSet<string>();
            var defaultVariablesCount = PRNG.Next(1, 8);
            for (var i = 0; i < defaultVariablesCount; i++)
            {
                ComplusEnvVar envVar;

                // Avoid duplicate variables
                do
                {
                    envVar = defaultGroup.GetRandomVariable();
                } while (!usedEnvVars.Add(envVar.Name));

                envVars[$"COMPlus_{envVar.Name}"] = envVar.Values[PRNG.Next(envVar.Values.Length)];
            }

            var stressVariablesCount = PRNG.Next(1, 4);
            for (var i = 0; i < stressVariablesCount; i++)
            {
                ComplusEnvVar envVar;

                // Avoid duplicate variables
                do
                {
                    var stressGroup = GetRandomTestGroup();
                    envVar = stressGroup.GetRandomVariable();
                } while (!usedEnvVars.Add(envVar.Name));

                envVars[$"COMPlus_{envVar.Name}"] = envVar.Values[PRNG.Next(envVar.Values.Length)];
            }

            return envVars;
        }
    }

    public class ComplusEnvVar
    {
        public string Name;
        public string[] Values;
        public double Weight;

        public override string ToString()
        {
            return $"COMPlus_{Name}=[{string.Join(",", Values)}]";
        }

        public override bool Equals(object obj)
        {
            if (obj is not ComplusEnvVar otherObj)
            {
                return false;
            }

            return otherObj.Name == Name;
        }

        public override int GetHashCode()
        {
            return Name.GetHashCode();
        }
    }

    public class ComplusEnvVarGroup
    {
        public string Name;
        public double Weight;
        public List<ComplusEnvVar> Variables;

        /// <summary>
        ///     Weights of individual EnvVars present in this group.
        /// </summary>
        private readonly List<Weights<ComplusEnvVar>> _variableWeights = new List<Weights<ComplusEnvVar>>();

        /// <summary>
        ///     Populate list of weighted choices.
        /// </summary>
        internal void PopulateWeights()
        {
            Variables.ForEach(v => AddVariable(v));
        }

        private void AddVariable(ComplusEnvVar variable)
        {
            _variableWeights.Add(new Weights<ComplusEnvVar>(variable, variable.Weight));
        }

        public ComplusEnvVar GetRandomVariable()
        {
            return PRNG.WeightedChoice(_variableWeights);
        }

        public override string ToString()
        {
            return $"{Name}: {Variables.Count}";
        }
    }
}
