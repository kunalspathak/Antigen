using Antigen.Tree;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Antigen
{
    public static partial class Helpers
    {
        public static VariableDeclarationSyntax GetVariableDeclaration(ExprType variableType, string variableName, ExpressionSyntax value)
        {
            return VariableDeclaration(
                    PredefinedType(
                        Token(variableType.TypeKind)))
                .WithVariables(
                    SingletonSeparatedList<VariableDeclaratorSyntax>(
                        VariableDeclarator(
                            Identifier(variableName))
                        .WithInitializer(
                            EqualsValueClause(value))));
        }

        public static string GetVariableName(ExprType variableType, int id)
        {
            return Enum.GetName(typeof(SpecialType), variableType.DataType).Replace("System_", "").ToLower() + "_" + id;
        }
    }
}
