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
        public static VariableDeclarationSyntax GetVariableDeclaration(Tree.ValueType variableType, string variableName, ExpressionSyntax value = null)
        {
            VariableDeclarationSyntax varDecl;
            if (variableType.PrimitiveType == Primitive.Struct)
            {
                varDecl = GetVariableDeclaration(variableType.TypeName, variableName);
            }
            else
            {
                varDecl = VariableDeclaration(PredefinedType(Token(variableType.TypeKind)));
            }

            var declarator = VariableDeclarator(Identifier(variableName));
            if (value != null)
            {
                declarator = declarator.WithInitializer(EqualsValueClause(value));
            }
            return varDecl.WithVariables(SingletonSeparatedList(declarator));
        }

        private static VariableDeclarationSyntax GetVariableDeclaration(string variableType, string variableName)
        {
            TypeSyntax variableTypeSyntax;
            if (variableType.Contains('.'))
            {
                string[] seperatedTypes = variableType.Split('.', StringSplitOptions.RemoveEmptyEntries);
                NameSyntax nameSyntax = QualifiedName(
                    IdentifierName(seperatedTypes[0]),
                    IdentifierName(seperatedTypes[1]));
                for (int subType = 2; subType < seperatedTypes.Length; subType++)
                {
                    nameSyntax = QualifiedName(nameSyntax, IdentifierName(seperatedTypes[subType]));
                }
                variableTypeSyntax = nameSyntax;
            }
            else
            {
                variableTypeSyntax = IdentifierName(variableType);
            }

            var varDecl = VariableDeclarator(Identifier(variableName));
            return VariableDeclaration(variableTypeSyntax)
                .WithVariables(SingletonSeparatedList(varDecl));
        }

        public static string GetVariableName(Tree.ValueType variableType, int id)
        {
            return variableType.VariableNameHint() + "_" + id;
        }

        public static PredefinedTypeSyntax GetToken(SyntaxKind syntaxKind)
        {
            return PredefinedType(Token(syntaxKind));
        }

        public static ExpressionSyntax GetVariableAccessExpression(string variableName)
        {
            if (variableName.Contains('.'))
            {
                string[] seperatedFields = variableName.Split('.', StringSplitOptions.RemoveEmptyEntries);
                var memberAccessExpr = MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    IdentifierName(seperatedFields[0]),
                    IdentifierName(seperatedFields[1]));
                for (int subType = 2; subType < seperatedFields.Length; subType++)
                {
                    memberAccessExpr = MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        memberAccessExpr,
                        IdentifierName(seperatedFields[subType]));
                }

                return memberAccessExpr;
            }
            else
            {
                return IdentifierName(variableName);
            }
        }

        public static ObjectCreationExpressionSyntax GetObjectCreationExpression(string objectType)
        {
            TypeSyntax objectTypeSyntax;

            if (objectType.Contains('.'))
            {
                string[] seperatedTypes = objectType.Split('.', StringSplitOptions.RemoveEmptyEntries);
                NameSyntax nameSyntax = QualifiedName(
                    IdentifierName(seperatedTypes[0]),
                    IdentifierName(seperatedTypes[1]));
                for (int subType = 2; subType < seperatedTypes.Length; subType++)
                {
                    nameSyntax = QualifiedName(nameSyntax, IdentifierName(seperatedTypes[subType]));
                }
                objectTypeSyntax = nameSyntax;
            }
            else
            {
                objectTypeSyntax = IdentifierName(objectType);
            }
            return ObjectCreationExpression(objectTypeSyntax).WithArgumentList(
                                                            ArgumentList());
        }

        //TODO: Reuse in GetVariableDeclaration
        public static ParameterSyntax GetParameterSyntax(Tree.ValueType variableType, string variableName)
        {
            ParameterSyntax parameterSyntax = Parameter(Identifier(variableName));
            return parameterSyntax.WithType(GetTypeSyntax(variableType));
        }

        //TODO: Reuse in GetVariableDeclaration
        public static TypeSyntax GetTypeSyntax(Tree.ValueType variableType)
        {
            if (variableType.PrimitiveType == Primitive.Struct)
            {
                if (!variableType.TypeName.Contains("."))
                {
                    return IdentifierName(variableType.TypeName);
                }

                // contains nested struct
                string[] seperatedTypes = variableType.TypeName.Split('.', StringSplitOptions.RemoveEmptyEntries);
                NameSyntax nameSyntax = QualifiedName(
                    IdentifierName(seperatedTypes[0]),
                    IdentifierName(seperatedTypes[1]));
                for (int subType = 2; subType < seperatedTypes.Length; subType++)
                {
                    nameSyntax = QualifiedName(nameSyntax, IdentifierName(seperatedTypes[subType]));
                }
                return nameSyntax;
            }
            else
            {
                return GetToken(variableType.TypeKind);
            }
        }
    }
}
