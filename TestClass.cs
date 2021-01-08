// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Antigen.Tree;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;


namespace Antigen
{
    /// <summary>
    ///     Denotes the class to generate.
    /// </summary>
    public class TestClass
    {
        public Scope ClassScope { get; private set; }
        public string ClassName;
        public TestCase TC { get; private set; }
        public Stack<Scope> ScopeStack { get; private set; }
        private List<Weights<MethodSignature>> _methods { get; set; }

        public AstUtils GetASTUtils()
        {
            return TC.AstUtils;
        }

        public TestClass(TestCase tc, string className)
        {
            ScopeStack = new Stack<Scope>();
            ClassScope = new Scope(tc);
            ClassName = className;
            TC = tc;
            _methods = new List<Weights<MethodSignature>>();
        }

        public void RegisterMethod(MethodSignature methodSignature)
        {
            double weight = 0.5;
            if (methodSignature.MethodName.StartsWith("Leaf"))
            {
                // left methods have lower weight. try calling actual
                // methods first.
                weight = 0.20;
            }
            _methods.Add(new Weights<MethodSignature>(methodSignature, weight));
        }

        /// <summary>
        ///     Returns all non-leaf methods
        /// </summary>
        public List<MethodSignature> AllNonLeafMethods => _methods.Where(m => !m.Data.IsLeaf).Select(m => m.Data).ToList();

        /// <summary>
        ///     Get random method that returns specfic returnType. Null if no such
        ///     method is generated yet.
        /// </summary>
        public MethodSignature GetRandomMethod(Tree.ValueType returnType)
        {
            var matchingMethods = _methods.Where(m => m.Data.ReturnType.Equals(returnType)).ToList();
            if (matchingMethods.Count == 0)
            {
                return null;
            }
            return PRNG.WeightedChoice(matchingMethods);
        }

        public Scope CurrentScope
        {
            get { return ScopeStack.Peek(); }
        }

        public void PushScope(Scope scope)
        {
            ScopeStack.Push(scope);
        }

        public Scope PopScope()
        {
            Scope ret = ScopeStack.Pop();
            //Debug.Assert(ret.Parent == ScopeStack.Peek());
            return ret;
        }

        public ClassDeclarationSyntax Generate()
        {
            // push class scope
            PushScope(ClassScope);

            List<MemberDeclarationSyntax> classMembers = new List<MemberDeclarationSyntax>();
            classMembers.AddRange(GenerateStructs());
            classMembers.AddRange(GenerateLeafMethods());
            classMembers.AddRange(GenerateMethods());

            // pop class scope
            PopScope();

            return ClassDeclaration(ClassName)
               .WithMembers(new SyntaxList<MemberDeclarationSyntax>(classMembers))
               .WithModifiers(new SyntaxTokenList(Token(SyntaxKind.PublicKeyword)));
        }

        /// <summary>
        ///     Generate structs in this class
        /// </summary>
        /// <returns></returns>
        private List<MemberDeclarationSyntax> GenerateStructs()
        {
            List<MemberDeclarationSyntax> structs = new List<MemberDeclarationSyntax>();

            //TODO:config - number of structs
            for (int structIndex = 1; structIndex <= 5; structIndex++)
            {
                string structName = $"S{structIndex}";
                var (structDecl, fields) = GenerateStruct(structName, structName, structIndex, 1);
                structs.Add(structDecl);
                CurrentScope.AddStructType(structName, fields);
            }

            (MemberDeclarationSyntax, List<StructField>) GenerateStruct(string structName, string structType, int structIndex, int depth)
            {
                StructDeclarationSyntax structDeclaration = StructDeclaration(structName)
                    .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)));

                List<MemberDeclarationSyntax> fieldsTree = new List<MemberDeclarationSyntax>();
                List<StructField> fieldsMetadata = new List<StructField>();
                //TODO: config - number of fields
                int fieldCount = PRNG.Next(1, 5);
                for (int fieldIndex = 1; fieldIndex <= fieldCount; fieldIndex++)
                {
                    //TODO:config - probability of nested structs
                    //TODO:config - struct nested depth
                    if (PRNG.Decide(0.1) && depth < 3)
                    {
                        string nestedStructName = $"S{structIndex}_D{depth}_F{fieldIndex}";
                        string nestedStructType = structType + "." + nestedStructName;
                        var (structDecl, childFields) = GenerateStruct(nestedStructName, nestedStructType, structIndex, depth + 1);
                        fieldsTree.Add(structDecl);
                        CurrentScope.AddStructType(nestedStructType, childFields);
                        structName = nestedStructName;
                        continue;
                    }

                    Tree.ValueType fieldType;
                    string fieldName;

                    //TODO:config - probability of fields of type struct
                    if (PRNG.Decide(0.3) && CurrentScope.NumOfStructTypes > 0)
                    {
                        fieldType = CurrentScope.AllStructTypes[PRNG.Next(CurrentScope.NumOfStructTypes)];
                    }
                    else
                    {
                        fieldType = GetASTUtils().GetRandomExprType();
                    }

                    fieldName = Helpers.GetVariableName(fieldType, fieldIndex);
                    fieldsTree.Add(FieldDeclaration(Helpers.GetVariableDeclaration(fieldType, fieldName)).WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword))));
                    fieldsMetadata.Add(new StructField(fieldType, fieldName));
                }

                return (structDeclaration.WithMembers(new SyntaxList<MemberDeclarationSyntax>(fieldsTree)), fieldsMetadata);
            }

            return structs;
        }

        /// <summary>
        ///     Generate methods in this class
        /// </summary>
        /// <returns></returns>
        private IList<MemberDeclarationSyntax> GenerateMethods()
        {
            List<MemberDeclarationSyntax> methods = new List<MemberDeclarationSyntax>();

            //TODO-config: No. of methods per class
            for (int i = 1; i < 5; i++)
            {
                var testMethod = new TestMethod(this, "Method" + i);
                methods.Add(testMethod.Generate());
            }
            methods.Add(new TestMethod(this, "Method0", true).Generate());
            methods.Add(ParseMemberDeclaration(
@$"public static void Main(string[] args) {{
    {ClassName} obj{ClassName} = new {ClassName}();
    obj{ClassName}.Method0();
}}
"));

            return methods;
        }

        private IList<MemberDeclarationSyntax> GenerateLeafMethods()
        {
            List<MemberDeclarationSyntax> leafMethods = new List<MemberDeclarationSyntax>();
            int leafMethodId = 0;
            foreach (Tree.ValueType variableType in Tree.ValueType.GetTypes())
            {
                var testMethod = new TestLeafMethod(this, "LeafMethod" + leafMethodId++, variableType);
                leafMethods.Add(testMethod.Generate());
            }

            foreach (Tree.ValueType structType in CurrentScope.AllStructTypes)
            {
                var testMethod = new TestLeafMethod(this, "LeafMethod" + leafMethodId++, structType);
                leafMethods.Add(testMethod.Generate());
            }
            return leafMethods;
        }
    }
}
