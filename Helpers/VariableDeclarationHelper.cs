using Antigen.Tree;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Antigen
{
    public static partial class Helpers
    {
        public static string GetVariableName(Tree.ValueType variableType, int id)
        {
            return variableType.VariableNameHint() + "_" + id;
        }
    }
}
