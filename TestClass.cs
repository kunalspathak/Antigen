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
            _methods.Add(new Weights<MethodSignature>(methodSignature, (double) PRNG.Next(1, 100) / 100));
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

        /// <summary>
        ///     Get random method that returns any type.
        /// </summary>
        /// <returns></returns>
        public MethodSignature GetRandomMethod()
        {
            return PRNG.WeightedChoice(_methods);
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
            classMembers.AddRange(GenerateStaticFields());
            classMembers.AddRange(GenerateLeafMethods());
            classMembers.AddRange(GenerateMethods());

            // pop class scope
            PopScope();

            return ClassDeclaration(ClassName)
               .WithMembers(classMembers.ToSyntaxList())
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

                return (structDeclaration.WithMembers(fieldsTree.ToSyntaxList()), fieldsMetadata);
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
            for (int i = 1; i < 2; i++)
            {
                var testMethod = new TestMethod(this, "Method" + i);
                methods.Add(testMethod.Generate());
            }
            methods.Add(new TestMethod(this, "Method0", true).Generate());

            var staticMethods = PreGenerated.StaticMethods;
            var mainMethod = staticMethods[0];
            var loggerMethod = staticMethods[1];
            var printLogMethod = staticMethods[2];

            mainMethod = mainMethod.WithBody(
                Block(
                    ParseStatement($"{ClassName} obj{ClassName} = new {ClassName}();"),
                    ParseStatement($"obj{ClassName}.Method0();"),
                    ParseStatement("PrintLog();")
                    )
                );

            methods.Add(mainMethod);
            methods.Add(loggerMethod);
            methods.Add(printLogMethod);

            return methods;
        }

        /// <summary>
        ///     Generate leaf methods in this class.
        /// </summary>
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

        /// <summary>
        ///     Generate static fields in this class.
        /// </summary>
        private IList<MemberDeclarationSyntax> GenerateStaticFields()
        {
            List<MemberDeclarationSyntax> fields = new List<MemberDeclarationSyntax>();

            // TODO-TEMP initialize one variable of each type
            int _variablesCount = 0;
            foreach (Tree.ValueType variableType in Tree.ValueType.GetTypes())
            {
                string variableName = "s_" + Helpers.GetVariableName(variableType, _variablesCount++);

                ExpressionSyntax rhs = Helpers.GetLiteralExpression(variableType, TC._numerals);
                CurrentScope.AddLocal(variableType, variableName);

                fields.Add(FieldDeclaration(Helpers.GetVariableDeclaration(variableType, variableName, rhs))
                    .WithModifiers(TokenList(Token(SyntaxKind.StaticKeyword))));
            }

            // TODO-TEMP initialize one variable of each struct type
            foreach (Tree.ValueType structType in CurrentScope.AllStructTypes)
            {
                string variableName = "s_" + Helpers.GetVariableName(structType, _variablesCount++);

                ExpressionSyntax rhs = Helpers.GetObjectCreationExpression(structType.TypeName);
                CurrentScope.AddLocal(structType, variableName);

                // Add all the fields to the scope
                var listOfStructFields = CurrentScope.GetStructFields(structType);
                foreach (var structField in listOfStructFields)
                {
                    CurrentScope.AddLocal(structField.FieldType, $"{variableName}.{structField.FieldName}");
                }

                fields.Add(FieldDeclaration(Helpers.GetVariableDeclaration(structType, variableName, rhs))
                    .WithModifiers(TokenList(Token(SyntaxKind.StaticKeyword))));
            }

            //TODO: Define some more constants
            fields.Add(
                FieldDeclaration(
                    Helpers.GetVariableDeclaration(
                        Tree.ValueType.ForPrimitive(Primitive.Int),
                        Constants.LoopInvariantName,
                        LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(PRNG.Next(10)))))
                .WithModifiers(TokenList(Token(SyntaxKind.StaticKeyword))));

            fields.Add(PreGenerated.LoggerVariableDecl);

            return fields;
        }
    }
}
