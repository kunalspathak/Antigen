// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.Intrinsics;
using Antigen.Expressions;
using Antigen.Statements;
using Antigen.Tree;
using Microsoft.CodeAnalysis;
using ValueType = Antigen.Tree.ValueType;

namespace Antigen
{
    /// <summary>
    ///     Denotes the class to generate.
    /// </summary>
    public partial class TestClass
    {
        public Scope ClassScope { get; private set; }
        public string ClassName;
        public TestCase TC { get; private set; }
        public Stack<Scope> ScopeStack { get; private set; }
        private List<Weights<MethodSignature>> _methods { get; set; }
        private static List<MethodSignature> vectorMethods = null;
        private static bool isVectorMethodsInitialized = false;
        private int _variableId;

        public string GetVariableName(Tree.ValueType variableType)
        {
            return variableType.VariableNameHint() + "_" + _variableId++;
        }

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
            _variableId = 0;
        }

        /// <summary>
        ///     Register method
        /// </summary>
        /// <param name="methodSignature"></param>
        public void RegisterMethod(MethodSignature methodSignature)
        {
            double methodProb = (double)PRNG.Next(1, 100) / 100;
            if (methodSignature.IsVectorCreateMethod)
            {
                // Further reduce the probability of vector create methods because
                // they are anyway used to create the vectors.
                methodProb /= 10;
            }
            _methods.Add(new Weights<MethodSignature>(methodSignature, methodProb));
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
        ///     Returns all vector methods
        /// </summary>
        public IEnumerable<Weights<MethodSignature>> AllVectorMethods => _methods.Where(m => m.Data.IsVectorMethod);

        /// <summary>
        ///     Returns all vector create methods
        /// </summary>
        public IEnumerable<Weights<MethodSignature>> AllVectorCreateMethods => _methods.Where(m => m.Data.IsVectorCreateMethod);

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
        ///     Get random vector create method that returns specific returnType.
        /// </summary>
        /// <param name="returnType"></param>
        /// <returns></returns>
        public MethodSignature GetRandomVectorCreateMethod(Tree.ValueType returnType)
        {
            var matchingMethods = AllVectorCreateMethods.Where(m => m.Data.ReturnType.Equals(returnType)).ToList();
            if (matchingMethods.Count == 0)
            {
                return null;
            }
            return PRNG.WeightedChoice(matchingMethods);
        }

        /// <summary>
        ///     Get random vector create method that returns specific returnType.
        /// </summary>
        /// <param name="returnType"></param>
        /// <returns></returns>
        public MethodSignature GetRandomVectorMethod(Tree.ValueType returnType)
        {
            var matchingMethods = AllVectorMethods.Where(m => m.Data.ReturnType.Equals(returnType)).ToList();

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
            if (returnType.IsVectorType)
            {
                // VectorType is not marked as leaf-method. So just return from overall methods list.
                return GetRandomVectorMethod(returnType);
            }

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

            if (TC.ContainsVectorData)
            {
                GenerateVectorMethods();
            }

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
                        fieldType = GetASTUtils().GetRandomValueType();
                    }

                    fieldName = GetVariableName(fieldType);
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
            foreach (Tree.ValueType variableType in Tree.ValueType.GetTypes())
            {
                string variableName = (isStatic ? "s_" : string.Empty) + GetVariableName(variableType);
                Expression rhs = ConstantValue.GetConstantValue(variableType, TC._numerals);
                CurrentScope.AddLocal(variableType, variableName);

                fields.Add(new FieldDeclStatement(TC, variableType, variableName, rhs, isStatic));
            }

            if (TC.ContainsVectorData)
            {
                foreach (Tree.ValueType variableType in Tree.ValueType.GetVectorTypes())
                {
                    Debug.Assert(variableType.IsVectorType);
                    string variableName = (isStatic ? "s_" : string.Empty) + GetVariableName(variableType);

                    Expression rhs;
                    if (PRNG.Decide(0.8))
                    {
                        MethodSignature createSig = GetRandomVectorCreateMethod(variableType);

                        List<Expression> argumentNodes = new List<Expression>();
                        List<ParamValuePassing> passingWays = new List<ParamValuePassing>();

                        int parametersCount = createSig.Parameters.Count;

                        for (int i = 0; i < parametersCount; i++)
                        {
                            MethodParam methodParam = createSig.Parameters[i];
                            Tree.ValueType argType = methodParam.ParamType;
                            Debug.Assert(!argType.IsVectorType);

                            Expression parameterValue = ConstantValue.GetConstantValue(argType, TC._numerals);
                            if (i == 0)
                            {
                                parameterValue = new CastExpression(TC, parameterValue, argType);
                            }

                            argumentNodes.Add(parameterValue);
                            passingWays.Add(ParamValuePassing.None);
                        }

                        rhs = new MethodCallExpression(createSig.MethodName, argumentNodes, passingWays);
                    }
                    else
                    {
                        rhs = ConstantValue.GetConstantValue(variableType, TC._numerals);
                    }

                    CurrentScope.AddLocal(variableType, variableName);
                    fields.Add(new FieldDeclStatement(TC, variableType, variableName, rhs, isStatic));
                }
            }

            // TODO-TEMP initialize one variable of each struct type
            foreach (Tree.ValueType structType in CurrentScope.AllStructTypes)
            {
                string variableName = (isStatic ? "s_" : string.Empty) + GetVariableName(structType);

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
