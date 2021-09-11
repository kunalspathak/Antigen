// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Antigen.Expressions;
using Antigen.Statements;
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
            _methods.Add(new Weights<MethodSignature>(methodSignature, (double)PRNG.Next(1, 100) / 100));
        }

        /// <summary>
        ///     Returns all non-leaf methods
        /// </summary>
        public IEnumerable<Weights<MethodSignature>> AllNonLeafMethods => _methods.Where(m => !m.Data.IsLeaf);

        /// <summary>
        ///     Returns all leaf methods
        /// </summary>
        public IEnumerable<Weights<MethodSignature>> AllLeafMethods => _methods.Where(m => m.Data.IsLeaf);

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
        ///     Gets random leaf method that returns specific returnType. Null if no such
        ///     method is generated yet.
        /// </summary>
        /// <param name="returnType"></param>
        /// <returns></returns>
        public MethodSignature GetRandomLeafMethod(Tree.ValueType returnType)
        {
            var matchingMethods = AllLeafMethods.Where(m => m.Data.ReturnType.Equals(returnType));
            if (matchingMethods.Count() == 0)
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

        public ClassDeclStatement Generate()
        {
            // push class scope
            PushScope(ClassScope);

            List<Statement> classMembers = new List<Statement>();
            classMembers.AddRange(GenerateStructs());
            classMembers.AddRange(GenerateFields(isStatic: true));
            classMembers.AddRange(GenerateFields(isStatic: false));

            // special
            classMembers.Add(new FieldDeclStatement(TC, Tree.ValueType.ForPrimitive(Primitive.Int), Constants.LoopInvariantName, ConstantValue.GetRandomConstantInt(0, 10), true));
            classMembers.Add(new ArbitraryCodeStatement(TC, "private static List<string> toPrint = new List<string>();"));

            classMembers.AddRange(GenerateLeafMethods());
            classMembers.AddRange(GenerateMethods());
            classMembers.Add(PreGenerated.StaticMethods);

            // pop class scope
            PopScope();

            return new ClassDeclStatement(TC, ClassName, classMembers);
        }

        /// <summary>
        ///     Generate structs in this class
        /// </summary>
        /// <returns></returns>
        private List<Statement> GenerateStructs()
        {
            List<Statement> structs = new List<Statement>();

            for (int structIndex = 1; structIndex <= TC.Config.StructCount; structIndex++)
            {
                string structName = $"S{structIndex}";
                var structDecl = GenerateStruct(structName, structName, structIndex, 1);
                structs.Add(structDecl);
                CurrentScope.AddStructType(structName, structDecl.StructFields);
            }

            StructDeclStatement GenerateStruct(string structName, string structType, int structIndex, int depth)
            {
                List<StructDeclStatement> nestedStructs = new List<StructDeclStatement>();
                List<StructField> fieldsMetadata = new List<StructField>();
                int fieldCount = PRNG.Next(1, TC.Config.StructFieldCount);
                for (int fieldIndex = 1; fieldIndex <= fieldCount; fieldIndex++)
                {
                    if (PRNG.Decide(TC.Config.NestedStructProbability) && depth < TC.Config.NestedStructDepth)
                    {
                        string nestedStructName = $"S{structIndex}_D{depth}_F{fieldIndex}";
                        string nestedStructType = structType + "." + nestedStructName;
                        var structDecl = GenerateStruct(nestedStructName, nestedStructType, structIndex, depth + 1);
                        nestedStructs.Add(structDecl);
                        CurrentScope.AddStructType(nestedStructType, structDecl.StructFields);

                        //structName = nestedStructName;
                        continue;
                    }

                    Tree.ValueType fieldType;
                    string fieldName;

                    if (PRNG.Decide(TC.Config.StructFieldTypeProbability) && CurrentScope.NumOfStructTypes > 0)
                    {
                        fieldType = CurrentScope.AllStructTypes[PRNG.Next(CurrentScope.NumOfStructTypes)];
                    }
                    else
                    {
                        fieldType = GetASTUtils().GetRandomExprType();
                    }

                    fieldName = Helpers.GetVariableName(fieldType, fieldIndex);
                    //fieldsTree.Add(new VarDeclStatement(TC, fieldType, fieldName, null));
                    fieldsMetadata.Add(new StructField(fieldType, fieldName));
                }
                return new StructDeclStatement(TC, structName, fieldsMetadata, nestedStructs);
            }

            return structs;
        }

        /// <summary>
        ///     Generate methods in this class
        /// </summary>
        /// <returns></returns>
        private IList<MethodDeclStatement> GenerateMethods()
        {
            List<MethodDeclStatement> methods = new List<MethodDeclStatement>();

            for (int i = 1; i < TC.Config.MethodCount; i++)
            {
                var testMethod = new TestMethod(this, "Method" + i);
                methods.Add(testMethod.Generate());
            }
            methods.Add(new TestMethod(this, "Method0", true).Generate());

            return methods;
        }

        /// <summary>
        ///     Generate leaf methods in this class.
        /// </summary>
        private IList<MethodDeclStatement> GenerateLeafMethods()
        {
            List<MethodDeclStatement> leafMethods = new List<MethodDeclStatement>();
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
        ///     Generate fields in this class.
        /// </summary>
        private List<Statement> GenerateFields(bool isStatic)
        {
            List<Statement> fields = new List<Statement>();

            // TODO-TEMP initialize one variable of each type
            int _variablesCount = 0;
            foreach (Tree.ValueType variableType in Tree.ValueType.GetTypes())
            {
                string variableName = (isStatic ? "s_" : string.Empty) + Helpers.GetVariableName(variableType, _variablesCount++);

                Expression rhs = ConstantValue.GetConstantValue(variableType, TC._numerals);

                CurrentScope.AddLocal(variableType, variableName);

                fields.Add(new FieldDeclStatement(TC, variableType, variableName, rhs, isStatic));
            }

            // TODO-TEMP initialize one variable of each struct type
            foreach (Tree.ValueType structType in CurrentScope.AllStructTypes)
            {
                string variableName = (isStatic ? "s_" : string.Empty) + Helpers.GetVariableName(structType, _variablesCount++);

                Expression rhs = new CreationExpression(TC, structType.TypeName, null);
                CurrentScope.AddLocal(structType, variableName);

                // Add all the fields to the scope
                var listOfStructFields = CurrentScope.GetStructFields(structType);
                foreach (var structField in listOfStructFields)
                {
                    CurrentScope.AddLocal(structField.FieldType, $"{variableName}.{structField.FieldName}");
                }

                fields.Add(new FieldDeclStatement(TC, structType, variableName, rhs, isStatic));
            }

            //TODO: Define some more constants

            return fields;
        }
    }
}
