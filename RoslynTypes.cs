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
        internal static SyntaxGenerator synGen = SyntaxGenerator.GetGenerator(new AdhocWorkspace(), LanguageNames.CSharp);
        internal static readonly CSharpCompilationOptions DebugOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, concurrentBuild: false, optimizationLevel: OptimizationLevel.Debug);
        internal static readonly CSharpCompilationOptions ReleaseOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, concurrentBuild: false, optimizationLevel: OptimizationLevel.Release);

    }
}
