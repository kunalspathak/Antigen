// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text;
//using Microsoft.CodeAnalysis.CSharp.Syntax;
//using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;


namespace Antigen.Statements
{
    public class StructDeclStatement : Statement
    {
        public readonly string StructName;
        public readonly List<StructField> StructFields;
        public readonly List<StructDeclStatement> NestedStructs;

        public StructDeclStatement(TestCase testCase, string structName, List<StructField> structFields, List<StructDeclStatement> nestedStructs) : base(testCase)
        {
            StructName = structName;
            StructFields = structFields;
            NestedStructs = nestedStructs;
        }

        public override string ToString()
        {
            StringBuilder strBuilder = new StringBuilder();
            strBuilder.AppendFormat("public struct {0} {{", StructName).AppendLine();
            // First define all the nested structs
            NestedStructs.ForEach(ns => strBuilder.AppendFormat("{0}", ns).AppendLine());

            // Next define all the fields
            strBuilder.AppendLine(string.Join(Environment.NewLine, StructFields));
            strBuilder.AppendLine("}");

            return strBuilder.ToString();
        }

        ///// <summary>
        /////     Generate structs in this class
        ///// </summary>
        ///// <returns></returns>
        //private static List<MemberDeclarationSyntax> GenerateStructs()
        //{
        //    List<MemberDeclarationSyntax> structs = new List<MemberDeclarationSyntax>();

        //    for (int structIndex = 1; structIndex <= TC.Config.StructCount; structIndex++)
        //    {
        //        string structName = $"S{structIndex}";
        //        var (structDecl, fields) = GenerateStruct(structName, structName, structIndex, 1);
        //        structs.Add(structDecl);
        //        CurrentScope.AddStructType(structName, fields);
        //    }

        //    (MemberDeclarationSyntax, List<StructField>) GenerateStruct(string structName, string structType, int structIndex, int depth)
        //    {
        //        StructDeclarationSyntax structDeclaration = StructDeclaration(structName)
        //            .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)));

        //        List<MemberDeclarationSyntax> fieldsTree = new List<MemberDeclarationSyntax>();
        //        List<StructField> fieldsMetadata = new List<StructField>();
        //        int fieldCount = PRNG.Next(1, TC.Config.StructFieldCount);
        //        for (int fieldIndex = 1; fieldIndex <= fieldCount; fieldIndex++)
        //        {
        //            if (PRNG.Decide(TC.Config.NestedStructProbability) && depth < TC.Config.NestedStructDepth)
        //            {
        //                string nestedStructName = $"S{structIndex}_D{depth}_F{fieldIndex}";
        //                string nestedStructType = structType + "." + nestedStructName;
        //                var (structDecl, childFields) = GenerateStruct(nestedStructName, nestedStructType, structIndex, depth + 1);
        //                fieldsTree.Add(structDecl);
        //                CurrentScope.AddStructType(nestedStructType, childFields);
        //                structName = nestedStructName;
        //                continue;
        //            }

        //            Tree.ValueType fieldType;
        //            string fieldName;

        //            if (PRNG.Decide(TC.Config.StructFieldTypeProbability) && CurrentScope.NumOfStructTypes > 0)
        //            {
        //                fieldType = CurrentScope.AllStructTypes[PRNG.Next(CurrentScope.NumOfStructTypes)];
        //            }
        //            else
        //            {
        //                fieldType = GetASTUtils().GetRandomExprType();
        //            }

        //            fieldName = Helpers.GetVariableName(fieldType, fieldIndex);
        //            fieldsTree.Add(FieldDeclaration(Helpers.GetVariableDeclaration(fieldType, fieldName)).WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword))));
        //            fieldsMetadata.Add(new StructField(fieldType, fieldName));
        //        }

        //        return (structDeclaration.WithMembers(fieldsTree.ToSyntaxList()), fieldsMetadata);
        //    }

        //    return structs;
        //}
    }

    /// <summary>
    ///     Represents a field present in a struct. This is useful to
    ///     expand the fully qualifier name of a field present inside a
    ///     nested struct.
    /// </summary>
    public struct StructField
    {
        public string FieldName;
        public Tree.ValueType FieldType;
        public string Accessor;

        public StructField(Tree.ValueType type, string name, string accessor = "public")
        {
            FieldType = type;
            FieldName = name;
            Accessor = accessor;
        }

        public override string ToString()
        {
            return $"{Accessor} {FieldType} {FieldName};";
        }
    }
}
