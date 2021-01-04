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
    public class Rsln
    {
        internal static readonly CSharpCompilationOptions CompileOptions = new CSharpCompilationOptions(OutputKind.ConsoleApplication, concurrentBuild: false, optimizationLevel: OptimizationLevel.Release/*, mainTypeName: "Main"*/);

        internal static readonly Dictionary<string, string> BaselineEnvVars = new Dictionary<string, string>()
        {
            { "COMPlus_JITMinOpts", "1" },
            { "COMPlus_TieredCompilation" , "0" }
        };

        internal static readonly Dictionary<string, string> CommonTestEnvVars = new Dictionary<string, string>()
        {
            { "COMPlus_JITMinOpts", "0" },
            { "COMPlus_TieredCompilation" , "0" }
        };

        // Use other combination from https://github.com/dotnet/runtime/blob/5a6c21cb6285a3c110f0048d9bf657042ea4ca10/src/tests/Common/testenvironment.proj
        // groups / arch: https://github.com/dotnet/runtime/blob/f8a83c898f18e3ebcd7c0ddd2e1773d8a771b346/eng/pipelines/common/templates/runtimes/run-test-job.yml
        internal static readonly List<ComplusVariableGroup> TestEnvVars = new List<ComplusVariableGroup>()
        {
            new ComplusVariableGroup("jitstress1", new ()
            {
                {  "COMPlus_JitStress", "1" },
            }),
            new ComplusVariableGroup("jitstress1_tiered", new ()
            {
                {  "COMPlus_JitStress", "1" },
                {  "COMPlus_TieredCompilation", "1" },
            }),
            new ComplusVariableGroup("jitstress2", new ()
            {
                {  "COMPlus_JitStress", "2" },
            }),
            new ComplusVariableGroup("jitstress2_tiered", new ()
            {
                {  "COMPlus_JitStress", "2" },
                {  "COMPlus_TieredCompilation", "1" },
            }),
            new ComplusVariableGroup("jitstressregs1", new ()
            {
                {  "COMPlus_JitStressRegs", "1" },
            }),
            new ComplusVariableGroup("jitstressregs2", new ()
            {
                {  "COMPlus_JitStressRegs", "2" },
            }),
            new ComplusVariableGroup("jitstressregs3", new ()
            {
                {  "COMPlus_JitStressRegs", "3" },
            }),
            new ComplusVariableGroup("jitstressregs4", new ()
            {
                {  "COMPlus_JitStressRegs", "4" },
            }),
            new ComplusVariableGroup("jitstressregs8", new ()
            {
                {  "COMPlus_JitStressRegs", "8" },
            }),
            new ComplusVariableGroup("jitstressregs10", new ()
            {
                {  "COMPlus_JitStressRegs", "0x10" },
            }),
            new ComplusVariableGroup("jitstressregs80", new ()
            {
                {  "COMPlus_JitStressRegs", "0x80" },
            }),
            new ComplusVariableGroup("jitstressregs1000", new ()
            {
                {  "COMPlus_JitStressRegs", "0x10000" },
            }),
            new ComplusVariableGroup("jitstress2_jitstressregs1", new ()
            {
                {  "COMPlus_JitStress", "2" },
                {  "COMPlus_JitStressRegs", "1" },
            }),
            new ComplusVariableGroup("jitstress2_jitstressregs2", new ()
            {
                {  "COMPlus_JitStress", "2" },
                {  "COMPlus_JitStressRegs", "2" },
            }),
            new ComplusVariableGroup("jitstress2_jitstressregs3", new ()
            {
                {  "COMPlus_JitStress", "2" },
                {  "COMPlus_JitStressRegs", "3" },
            }),
            new ComplusVariableGroup("jitstress2_jitstressregs4", new ()
            {
                {  "COMPlus_JitStress", "2" },
                {  "COMPlus_JitStressRegs", "4" },
            }),
            new ComplusVariableGroup("jitstress2_jitstressregs8", new ()
            {
                {  "COMPlus_JitStress", "2" },
                {  "COMPlus_JitStressRegs", "8" },
            }),
            new ComplusVariableGroup("jitstress2_jitstressregs0x10", new ()
            {
                {  "COMPlus_JitStress", "2" },
                {  "COMPlus_JitStressRegs", "0x10" },
            }),
            new ComplusVariableGroup("jitstress2_jitstressregs0x80", new ()
            {
                {  "COMPlus_JitStress", "2" },
                {  "COMPlus_JitStressRegs", "0x80" },
            }),
            new ComplusVariableGroup("jitstress2_jitstressregs0x1000", new ()
            {
                {  "COMPlus_JitStress", "2" },
                {  "COMPlus_JitStressRegs", "0x10000" },
            }),
        };
    }

    public class ComplusVariableGroup
    {
        public string Name { get; private set; }
        public Dictionary<string, string> Vars { get; set; }

        internal ComplusVariableGroup(string name, Dictionary<string, string> vars)
        {
            Name = name;
            Vars = vars;
        }
    }


}
