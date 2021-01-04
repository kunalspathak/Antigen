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

        internal static readonly Dictionary<string, string> TestEnvVars = new Dictionary<string, string>()
        {
            { "COMPlus_JITMinOpts", "0" },
            { "COMPlus_TieredCompilation" , "0" }
        };
    }
}
