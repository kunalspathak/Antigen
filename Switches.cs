using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.VisualBasic.CompilerServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Antigen
{
    internal class ComplusEnvVarGroup
    {
        public string Name { get; }
        public List<ComplusEnvVar> EnvVariables { get; }

        /// <summary>
        ///     Weights of individual EnvVars present in this group.
        /// </summary>
        private readonly List<Weights<ComplusEnvVar>> variableWeights;

        public ComplusEnvVarGroup(string name)
        {
            Name = name;
            EnvVariables = new List<ComplusEnvVar>();
            variableWeights = new List<Weights<ComplusEnvVar>>();
        }

        public void AddVariable(ComplusEnvVar variable)
        {
            EnvVariables.Add(variable);
            variableWeights.Add(new Weights<ComplusEnvVar>(variable, variable.Weight));
        }

        public ComplusEnvVar GetRandomVariable()
        {
            return PRNG.WeightedChoice(variableWeights);
        }

        public ComplusEnvVar GetVariable(string name)
        {
            return EnvVariables.First(envVar => envVar.Name == name);
        }
    }

    internal class ComplusEnvVar
    {
        public string Name { get; }
        public string[] Values { get; }
        public double Weight { get; }

        public ComplusEnvVar(string name, string[] values, double weight)
        {
            Name = name;
            Values = values;
            Weight = weight;
        }

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

    public class Switches
    {

        private static ComplusEnvVarGroup s_baselineGroup;
        private static ComplusEnvVarGroup s_defaultGroup;
        private static ComplusEnvVarGroup s_jitStressGroup;
        private static ComplusEnvVarGroup s_jitStressRegsGroup;
        private static ComplusEnvVarGroup s_hardwareGroup;
        private static ComplusEnvVarGroup s_commonVarGroup;

        private static readonly string[] s_onOffSwitch = new string[] { "0", "1" };

        public static void Initialize()
        {
            // Populate Baseline
            s_baselineGroup = new ComplusEnvVarGroup("Baseline");
            s_baselineGroup.AddVariable(new ComplusEnvVar("JITMinOpts", new string[] { "1" }, 1.0));
            s_baselineGroup.AddVariable(new ComplusEnvVar("TieredCompilation", new string[] { "1" }, 1.0));

            // Populate Common
            s_commonVarGroup = new ComplusEnvVarGroup("Common");
            s_commonVarGroup.AddVariable(new ComplusEnvVar("JITMinOpts", new string[] { "0" }, 1.0));
            s_commonVarGroup.AddVariable(new ComplusEnvVar("TieredCompilation", new string[] { "0" }, 1.0));

            // Default group
            s_defaultGroup = new ComplusEnvVarGroup("Default");
            s_defaultGroup.AddVariable(new ComplusEnvVar("JitCloneLoops", s_onOffSwitch, 0.04));
            s_defaultGroup.AddVariable(new ComplusEnvVar("JitAlignLoops", s_onOffSwitch, 0.02));
            s_defaultGroup.AddVariable(new ComplusEnvVar("JitAlignLoopAdaptive", s_onOffSwitch, 0.01));
            s_defaultGroup.AddVariable(new ComplusEnvVar("JitAlignLoopMinBlockWeight", new string[] { "0", "1", "2", "10", "20" }, 0.01));
            s_defaultGroup.AddVariable(new ComplusEnvVar("JitDoubleAlign", s_onOffSwitch, 0.01));
            s_defaultGroup.AddVariable(new ComplusEnvVar("JitEnableDevirtualization", s_onOffSwitch, 0.01));
            s_defaultGroup.AddVariable(new ComplusEnvVar("JitEnableLateDevirtualization", s_onOffSwitch, 0.01));
            s_defaultGroup.AddVariable(new ComplusEnvVar("JitForceFallback", s_onOffSwitch, 0.01));
            s_defaultGroup.AddVariable(new ComplusEnvVar("JITInlineDepth", new string[] { "0", "1", "2", "10", "20" }, 0.01));
            s_defaultGroup.AddVariable(new ComplusEnvVar("JitNoCMOV", s_onOffSwitch, 0.04));
            s_defaultGroup.AddVariable(new ComplusEnvVar("JitNoCSE", s_onOffSwitch, 0.03));
            s_defaultGroup.AddVariable(new ComplusEnvVar("JitNoCSE2", s_onOffSwitch, 0.05));
            s_defaultGroup.AddVariable(new ComplusEnvVar("JitNoForceFallback", s_onOffSwitch, 0.01));
            s_defaultGroup.AddVariable(new ComplusEnvVar("JitNoHoist", s_onOffSwitch, 0.05));
            s_defaultGroup.AddVariable(new ComplusEnvVar("JitNoInline", s_onOffSwitch, 0.04));
            s_defaultGroup.AddVariable(new ComplusEnvVar("JitNoMemoryBarriers", s_onOffSwitch, 0.02));
            s_defaultGroup.AddVariable(new ComplusEnvVar("JitNoStructPromotion", new string[] { "0", "1", "2" }, 0.02));
            s_defaultGroup.AddVariable(new ComplusEnvVar("JitNoUnroll", s_onOffSwitch, 0.02));
            s_defaultGroup.AddVariable(new ComplusEnvVar("JitStackAllocToLocalSize", new string[] { "0", "1", "2", "4", "10", "16", "100"}, 0.01));
            s_defaultGroup.AddVariable(new ComplusEnvVar("JitSkipArrayBoundCheck", s_onOffSwitch, 0.02));
            s_defaultGroup.AddVariable(new ComplusEnvVar("JitSlowDebugChecksEnabled", s_onOffSwitch, 0.02));
            s_defaultGroup.AddVariable(new ComplusEnvVar("JitSplitFunctionSize", new string[] { "0", "1", "2", "4", "10", "16", "100" }, 0.01));
            s_defaultGroup.AddVariable(new ComplusEnvVar("JitStackChecks", s_onOffSwitch, 0.01));
            s_defaultGroup.AddVariable(new ComplusEnvVar("InjectFault", s_onOffSwitch, 0.01));
            s_defaultGroup.AddVariable(new ComplusEnvVar("JitNoRngChks", s_onOffSwitch, 0.01));
            s_defaultGroup.AddVariable(new ComplusEnvVar("JitEnableNoWayAssert", s_onOffSwitch, 0.01));
            s_defaultGroup.AddVariable(new ComplusEnvVar("JitAggressiveInlining", s_onOffSwitch, 0.05));
            s_defaultGroup.AddVariable(new ComplusEnvVar("JitMaxLocalsToTrack", new string[] { "0", "0x10", "0x400", "0x800", "0x1000"}, 0.05));
            s_defaultGroup.AddVariable(new ComplusEnvVar("JitDoAssertionProp", s_onOffSwitch, 0.05));
            s_defaultGroup.AddVariable(new ComplusEnvVar("JitDoCopyProp", s_onOffSwitch, 0.05));
            s_defaultGroup.AddVariable(new ComplusEnvVar("JitDoEarlyProp", s_onOffSwitch, 0.05));
            s_defaultGroup.AddVariable(new ComplusEnvVar("JitDoLoopHoisting", s_onOffSwitch, 0.05));
            s_defaultGroup.AddVariable(new ComplusEnvVar("JitDoLoopInversion", s_onOffSwitch, 0.05));
            s_defaultGroup.AddVariable(new ComplusEnvVar("JitDoRangeAnalysis", s_onOffSwitch, 0.05));
            s_defaultGroup.AddVariable(new ComplusEnvVar("JitDoRedundantBranchOpts", s_onOffSwitch, 0.05));
            s_defaultGroup.AddVariable(new ComplusEnvVar("JitDoSsa", s_onOffSwitch, 0.05));
            s_defaultGroup.AddVariable(new ComplusEnvVar("JitDoValueNumber", s_onOffSwitch, 0.05));
            s_defaultGroup.AddVariable(new ComplusEnvVar("JitOptRepeat", new string[] { "*" }, 0.05));
            s_defaultGroup.AddVariable(new ComplusEnvVar("JitOptRepeatCount", new string[] { "1", "2", "5" }, 0.03));
            s_defaultGroup.AddVariable(new ComplusEnvVar("TailCallLoopOpt", s_onOffSwitch, 0.03));
            s_defaultGroup.AddVariable(new ComplusEnvVar("FastTailCalls", s_onOffSwitch, 0.03));
            s_defaultGroup.AddVariable(new ComplusEnvVar("JitEnableFinallyCloning", s_onOffSwitch, 0.03));
            s_defaultGroup.AddVariable(new ComplusEnvVar("JitEnableRemoveEmptyTry", s_onOffSwitch, 0.03));
            s_defaultGroup.AddVariable(new ComplusEnvVar("JitEnableGuardedDevirtualization", s_onOffSwitch, 0.03));
            s_defaultGroup.AddVariable(new ComplusEnvVar("JitExpandCallsEarly", s_onOffSwitch, 0.03));

            // JitStress
            s_jitStressGroup = new ComplusEnvVarGroup("JitStress");
            s_jitStressGroup.AddVariable(new ComplusEnvVar("JitStress", new string[] { "0", "1", "2" }, 0.04));
            s_jitStressGroup.AddVariable(new ComplusEnvVar("TailcallStress", s_onOffSwitch, 0.02));

            // JitStressRegs
            s_jitStressRegsGroup = new ComplusEnvVarGroup("JitStressRegs");
            s_jitStressRegsGroup.AddVariable(new ComplusEnvVar("JitStressRegs", new string[] { "0", "1", "2", "3", "4", "8", "0x10", "0x80", "0x1000" }, 0.04));

            // Hardware
            s_hardwareGroup = new ComplusEnvVarGroup("Hardware");
            s_hardwareGroup.AddVariable(new ComplusEnvVar("EnableAES", s_onOffSwitch, 0.02));
            s_hardwareGroup.AddVariable(new ComplusEnvVar("EnableAVX", s_onOffSwitch, 0.02));
            s_hardwareGroup.AddVariable(new ComplusEnvVar("EnableAVX2", s_onOffSwitch, 0.02));
            s_hardwareGroup.AddVariable(new ComplusEnvVar("EnableBMI1", s_onOffSwitch, 0.02));
            s_hardwareGroup.AddVariable(new ComplusEnvVar("EnableBMI2", s_onOffSwitch, 0.02));
            s_hardwareGroup.AddVariable(new ComplusEnvVar("EnableFMA", s_onOffSwitch, 0.02));
            s_hardwareGroup.AddVariable(new ComplusEnvVar("EnableHWIntrinsic", s_onOffSwitch, 0.02));
            s_hardwareGroup.AddVariable(new ComplusEnvVar("EnableIncompleteISAClass", s_onOffSwitch, 0.02));
            s_hardwareGroup.AddVariable(new ComplusEnvVar("EnableLZCNT", s_onOffSwitch, 0.02));
            s_hardwareGroup.AddVariable(new ComplusEnvVar("EnablePCLMULQDQ", s_onOffSwitch, 0.02));
            s_hardwareGroup.AddVariable(new ComplusEnvVar("EnablePOPCNT", s_onOffSwitch, 0.02));
            s_hardwareGroup.AddVariable(new ComplusEnvVar("EnableSSE", s_onOffSwitch, 0.02));
            s_hardwareGroup.AddVariable(new ComplusEnvVar("EnableSSE2", s_onOffSwitch, 0.02));
            s_hardwareGroup.AddVariable(new ComplusEnvVar("EnableSSE3", s_onOffSwitch, 0.02));
            s_hardwareGroup.AddVariable(new ComplusEnvVar("EnableSSE3_4", s_onOffSwitch, 0.02));
            s_hardwareGroup.AddVariable(new ComplusEnvVar("EnableSSE41", s_onOffSwitch, 0.02));
            s_hardwareGroup.AddVariable(new ComplusEnvVar("EnableSSE42", s_onOffSwitch, 0.02));
            s_hardwareGroup.AddVariable(new ComplusEnvVar("EnableSSSE3", s_onOffSwitch, 0.02));
            s_hardwareGroup.AddVariable(new ComplusEnvVar("FeatureSIMD", s_onOffSwitch, 0.02));
        }

        /// <summary>
        ///     Returns baseline variables
        /// </summary>
        /// <returns></returns>
        public static Dictionary<string, string> BaseLineVars()
        {
            var envVars = new Dictionary<string, string>();
            foreach (var variable in s_baselineGroup.EnvVariables)
            {
                envVars[$"COMPlus_{variable.Name}"] = variable.Values[PRNG.Next(variable.Values.Length)];
            }
            return envVars;
        }

        /// <summary>
        ///     Returns test variables
        /// </summary>
        /// <returns></returns>
        public static Dictionary<string, string> TestVars()
        {
            var envVars = new Dictionary<string, string>();

            // common variables
            foreach (var variable in s_commonVarGroup.EnvVariables)
            {
                envVars[$"COMPlus_{variable.Name}"] = variable.Values[PRNG.Next(variable.Values.Length)];
            }

            // default variables
            var usedEnvVars = new HashSet<string>();
            //TODO: config no. of variables to pick
            var defaultVariablesCount = PRNG.Next(1, 8);
            for (var i = 0; i < defaultVariablesCount; i++)
            {
                ComplusEnvVar envVar;

                // Avoid duplicate variables
                do
                {
                    envVar = s_defaultGroup.GetRandomVariable();
                } while (!usedEnvVars.Add(envVar.Name));

                envVars[$"COMPlus_{envVar.Name}"] = envVar.Values[PRNG.Next(envVar.Values.Length)];
            }


            // 30% of time add JitStress flag
            if (PRNG.Decide(0.3))
            {
                var envVar = s_jitStressGroup.GetRandomVariable();
                envVars[$"COMPlus_{envVar.Name}"] = envVar.Values[PRNG.Next(envVar.Values.Length)];
            }

            // 30% of time add JitStressRegs flag
            if (PRNG.Decide(0.3))
            {
                var envVar = s_jitStressRegsGroup.GetRandomVariable();
                envVars[$"COMPlus_{envVar.Name}"] = envVar.Values[PRNG.Next(envVar.Values.Length)];
            }

            // 20% of time add Hardware flag
            if (PRNG.Decide(0.2))
            {
                usedEnvVars = new HashSet<string>();
                //TODO: config no. of variables to pick
                var hardwareVariablesCount = PRNG.Next(1, 5);
                for (var i = 0; i < hardwareVariablesCount; i++)
                {
                    ComplusEnvVar envVar;

                    // Avoid duplicate variables
                    do
                    {
                        envVar = s_hardwareGroup.GetRandomVariable();
                    } while (!usedEnvVars.Add(envVar.Name));

                    envVars[$"COMPlus_{envVar.Name}"] = envVar.Values[PRNG.Next(envVar.Values.Length)];
                }
            }

            return envVars;
        }

        internal static readonly CSharpCompilationOptions CompileOptions = new CSharpCompilationOptions(OutputKind.ConsoleApplication, concurrentBuild: false, optimizationLevel: OptimizationLevel.Release/*, mainTypeName: "Main"*/);
    }

}
