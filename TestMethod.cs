using Antigen.Config;
using Antigen.Expressions;
using Antigen.Statements;
using Antigen.Tree;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualBasic.CompilerServices;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Antigen
{
    /// <summary>
    ///     Denotes the method to generate.
    /// </summary>
    public class TestMethod
    {
        private readonly TestClass _testClass;
        private TestCase TC => _testClass.TC;
        protected readonly string Name;
        private readonly List<Weights<ParamValuePassing>> _valuePassing;


#if DEBUG
        private readonly Dictionary<string, int> _expressionsCount = new();
        private readonly Dictionary<string, int> _statementsCount = new();
#endif

        private int _variablesCount = 0;

        internal HashSet<string> callsFromThisMethod = new HashSet<string>();

        public AstUtils GetASTUtils()
        {
            return TC.AstUtils;
        }

        protected Scope MethodScope;
        private readonly bool _isMainInvocation;
        protected MethodSignature MethodSignature { get; set; }
        public Scope CurrentScope => _testClass.CurrentScope;

        protected void PushScope(Scope scope)
        {
            _testClass.PushScope(scope);
        }

        protected Scope PopScope(Action<string> trackLocalVariables)
        {
            Scope ret = _testClass.PopScope();

            // Before poping, log the variables
            foreach (var variableName in ret.AllVariables)
            {
                if (PRNG.Decide(TC.Config.LocalVariablesLogProbability))
                {
                    trackLocalVariables(variableName);
                }
            }

            return ret;
        }

        internal void LogVariable(List<Statement> stmtList, string variableName)
        {
            stmtList.Add(GetLogInvokeStatement(variableName));
        }

        /// <summary>
        ///     Register the method in enclosing class.
        /// </summary>
        protected void RegisterMethod(MethodSignature methodSignature)
        {
            _testClass.RegisterMethod(methodSignature);
        }

        /// <summary>
        ///     Creates leaf method that does not take parameters and has a single return statement.
        /// </summary>
        protected TestMethod(TestClass enclosingClass, string methodName)
        {
            _testClass = enclosingClass;
            Name = methodName;
            MethodScope = new Scope(enclosingClass.TC, ScopeKind.FunctionScope, enclosingClass.ClassScope);
        }

        /// <summary>
        ///     Creates test method.
        /// </summary>
        /// <param name="enclosingClass"></param>
        /// <param name="methodName"></param>
        /// <param name="isMainInvocation"></param>
        public TestMethod(TestClass enclosingClass, string methodName, bool isMainInvocation = false)
        {
            _testClass = enclosingClass;
            Name = methodName;
            MethodScope = new Scope(enclosingClass.TC, ScopeKind.FunctionScope, enclosingClass.ClassScope);
            _isMainInvocation = isMainInvocation;
            _valuePassing = new()
            {
                new Weights<ParamValuePassing>(ParamValuePassing.None, TC.Config.ParamPassingNoneProbability),
                new Weights<ParamValuePassing>(ParamValuePassing.Ref, TC.Config.ParamPassingRefProbability),
                new Weights<ParamValuePassing>(ParamValuePassing.Out, TC.Config.ParamPassingOutProbability),
                //new Weights<ParamValuePassing>(ParamValuePassing.In, 10),
            };
        }

        public virtual MethodDeclarationSyntax Generate()
        {
            PushScope(MethodScope);

            MethodDeclarationSyntax methodDeclaration = GenerateMethodSignature();
            IList<StatementSyntax> methodBody = new List<StatementSyntax>();

            // TODO-TEMP initialize one variable of each type
            foreach (Tree.ValueType variableType in Tree.ValueType.GetTypes())
            {
                string variableName = Helpers.GetVariableName(variableType, _variablesCount++);

                ExpressionSyntax rhs = ExprHelper(ExprKind.LiteralExpression, variableType, 0);
                CurrentScope.AddLocal(variableType, variableName);

                methodBody.Add(Annotate(LocalDeclarationStatement(Helpers.GetVariableDeclaration(variableType, variableName, rhs)), "var-init", 0));
            }

            // TODO-TEMP initialize one variable of each struct type
            foreach (Tree.ValueType structType in CurrentScope.AllStructTypes)
            {
                string variableName = Helpers.GetVariableName(structType, _variablesCount++);

                CurrentScope.AddLocal(structType, variableName);

                // Add all the fields to the scope
                var listOfStructFields = CurrentScope.GetStructFields(structType);
                foreach(var structField in listOfStructFields)
                {
                    CurrentScope.AddLocal(structField.FieldType, $"{variableName}.{structField.FieldName}");
                }

                methodBody.Add(Annotate(LocalDeclarationStatement(
                    Helpers.GetVariableDeclaration(structType, variableName,
                    Helpers.GetObjectCreationExpression(structType.TypeName))), "struct-init", 0));

                if (!PRNG.Decide(TC.Config.StructAliasProbability))
                {
                    continue;
                }

                var aliasVariableName = Helpers.GetVariableName(structType, _variablesCount++);

                CurrentScope.AddLocal(structType, aliasVariableName);

                // Add all the fields to the scope
                listOfStructFields = CurrentScope.GetStructFields(structType);
                foreach (var structField in listOfStructFields)
                {
                    CurrentScope.AddLocal(structField.FieldType, $"{aliasVariableName}.{structField.FieldName}");
                }

                methodBody.Add(Annotate(LocalDeclarationStatement(
                    Helpers.GetVariableDeclaration(structType, aliasVariableName,
                    TestCase.GetExpressionSyntax(variableName))), "struct-alias-init", 0));

            }

            // TODO-TEMP initialize out and ref method parameters
            var paramsToInitialize = MethodSignature.Parameters.Where(p => p.PassingWay == ParamValuePassing.Out);
            foreach (MethodParam param in paramsToInitialize)
            {
                methodBody.Add(VariableAssignmentHelper(param.ParamType, param.ParamName));
                CurrentScope.AddLocal(param.ParamType, param.ParamName);
            }

            for (int i = 0; i < PRNG.Next(1, TC.Config.MaxStatementCount); i++)
            {
                StmtKind cur = GetASTUtils().GetRandomStatement();
                methodBody.Add(StatementHelper(cur, 0));
            }

            // For main invocation method, invoke all the other methods once
            if (_isMainInvocation)
            {
                foreach (var nonLeafMethod in _testClass.AllNonLeafMethods)
                {
                    //TODO-future: Select any assignOper
                    //Tree.Operator assignOper = GetASTUtils().GetRandomAssignmentOperator();

                    ExpressionSyntax lhs = ExprHelper(ExprKind.VariableExpression, nonLeafMethod.Data.ReturnType, 0);
                    ExpressionSyntax rhs = MethodCallHelper(nonLeafMethod.Data, 0);

                    methodBody.Add(Annotate(ExpressionStatement(AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, lhs, rhs)), "MethodCall-Assign", 0));
                }

                // print all static variables
                foreach (var variableName in _testClass.ClassScope.AllVariables)
                {
                    methodBody.Add(GetLogInvokeStatement(variableName));
                }
            }

            // print all variables
            foreach (var variableName in CurrentScope.AllVariables)
            {
                methodBody.Add(GetLogInvokeStatement(variableName));
            }

            // return statement
            methodBody.Add(StatementHelper(StmtKind.ReturnStatement, 0));

            PopScope(variableName => { });

            RegisterMethod(MethodSignature);

            // Wrap everything in unchecked so we do not see overflow compilation errors
            return methodDeclaration.WithBody(Block(CheckedStatement(SyntaxKind.UncheckedStatement, Block(methodBody))));
        }

        /// <summary>
        ///     Generates method signature of this method.
        /// </summary>
        protected virtual MethodDeclarationSyntax GenerateMethodSignature()
        {
            MethodSignature = new MethodSignature(Name);
            int numOfParameters = 0;
            if (!_isMainInvocation)
            {
                numOfParameters = PRNG.Next(1, TC.Config.MaxMethodParametersCount);
                MethodSignature.ReturnType = GetRandomExprType();
            }

            List<MethodParam> parameters = new List<MethodParam>();
            MethodSignature.Parameters = parameters;
            List<SyntaxNodeOrToken> parameterNodes = new List<SyntaxNodeOrToken>();

            for (int paramIndex = 0; paramIndex < numOfParameters; paramIndex++)
            {
                var paramType = GetRandomExprType();
                var passingWay = PRNG.WeightedChoice(_valuePassing);
                string paramName = "p_" + Helpers.GetVariableName(paramType, paramIndex);

                // Add parameters to the scope except the one that is marked as OUT
                // OUT parameters will be added once they are initialized.
                if (passingWay != ParamValuePassing.Out)
                {
                    CurrentScope.AddLocal(paramType, paramName);
                }

                ParameterSyntax parameterNode = Helpers.GetParameterSyntax(paramType, paramName);
                if (passingWay != ParamValuePassing.None)
                {
                    SyntaxToken passingWayToken = Token(SyntaxKind.None);
                    switch (passingWay)
                    {
                        //case ParamValuePassing.In:
                        //    passingWayToken = Token(SyntaxKind.InKeyword);
                        //    break;
                        case ParamValuePassing.Out:
                            passingWayToken = Token(SyntaxKind.OutKeyword);
                            break;
                        case ParamValuePassing.Ref:
                            passingWayToken = Token(SyntaxKind.RefKeyword);
                            break;
                        default:
                            Debug.Assert(false, "invalid value for passingway!");
                            break;
                    }
                    parameterNode = parameterNode.WithModifiers(TokenList(passingWayToken));
                }

                parameters.Add(new MethodParam()
                {
                    ParamName = paramName,
                    ParamType = paramType,
                    PassingWay = passingWay
                });
                parameterNodes.Add(parameterNode);

                if (paramIndex + 1 < numOfParameters)
                {
                    parameterNodes.Add(Token(SyntaxKind.CommaToken));
                }
            }

            return MethodDeclaration(Helpers.GetTypeSyntax(MethodSignature.ReturnType), Name)
                    .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
                    .WithParameterList(ParameterList(SeparatedList<ParameterSyntax>(parameterNodes)));
        }


        /// <summary>
        ///     Generate statement of <paramref name="stmtKind"/>. It will generate terminal statement
        ///     if <paramref name="depth"/> exceeds the threshold.
        /// </summary>
        /// <param name="stmtKind"></param>
        /// <param name="depth"></param>
        /// <returns></returns>
        public Statement StatementHelper(StmtKind stmtKind, int depth)
        {
            //Debug.Assert(depth <= TC.Config.MaxStmtDepth);

            switch (stmtKind)
            {
                case StmtKind.VariableDeclaration:
                    {
                        Tree.ValueType variableType = GetRandomExprType();

                        string variableName = Helpers.GetVariableName(variableType, _variablesCount++);

                        Expression rhs = ExprHelper(GetASTUtils().GetRandomExpressionReturningPrimitive(variableType.PrimitiveType), variableType, 0);
                        CurrentScope.AddLocal(variableType, variableName);

                        // Add all the fields to the scope
                        if (variableType.PrimitiveType == Primitive.Struct)
                        {
                            var listOfStructFields = CurrentScope.GetStructFields(variableType);
                            foreach (var structField in listOfStructFields)
                            {
                                CurrentScope.AddLocal(structField.FieldType, $"{variableName}.{structField.FieldName}");
                            }
                        }

                        return new VarDeclStatement(TC, variableType, variableName, rhs);
                    }
                case StmtKind.IfElseStatement:
                    {
                        //Debug.Assert(depth <= TC.Config.MaxStmtDepth);

                        Tree.ValueType condValueType = Tree.ValueType.ForPrimitive(Primitive.Boolean);
                        Expression conditionExpr = ExprHelper(GetASTUtils().GetRandomExpressionReturningPrimitive(Primitive.Boolean), condValueType, 0);

                        Scope ifBranchScope = new Scope(TC, ScopeKind.ConditionalScope, CurrentScope);
                        Scope elseBranchScope = new Scope(TC, ScopeKind.ConditionalScope, CurrentScope);

                        int ifcount = PRNG.Next(1, TC.Config.MaxStatementCount);
                        List<Statement> ifBody = new List<Statement>();

                        PushScope(ifBranchScope);
                        for (int i = 0; i < ifcount; i++)
                        {
                            StmtKind cur;
                            if (depth < TC.Config.MaxStmtDepth)
                            {
                                cur = GetASTUtils().GetRandomStatement();
                            }
                            else
                            {
                                cur = GetASTUtils().GetRandomTerminalStatement();
                            }
                            ifBody.Add(StatementHelper(cur, depth + 1));
                        }
                        PopScope(variableName => LogVariable(ifBody, variableName)); // pop 'if' body scope

                        int elsecount = PRNG.Next(1, TC.Config.MaxStatementCount);
                        List<Statement> elseBody = new List<Statement>();

                        PushScope(elseBranchScope);
                        for (int i = 0; i < elsecount; i++)
                        {
                            StmtKind cur;
                            if (depth < TC.Config.MaxStmtDepth)
                            {
                                cur = GetASTUtils().GetRandomStatement();
                            }
                            else
                            {
                                cur = GetASTUtils().GetRandomTerminalStatement();
                            }
                            elseBody.Add(StatementHelper(cur, depth + 1));
                        }
                        PopScope(variableName => LogVariable(elseBody, variableName)); // pop 'else' body scope

                        return new IfElseStatement(TC, conditionExpr, ifBody, elseBody);
                    }
                case StmtKind.AssignStatement:
                    {
                        Tree.Operator assignOper = GetASTUtils().GetRandomAssignmentOperator();
                        Tree.ValueType lhsExprType, rhsExprType;
                        //TODO-cleanup: Somehow combine GetRandomExprType() and GetRandomStructType() functionality
                        // Currently the only problem is AllStructTypes is in scope object but GetRandomExprType() is
                        // in AstUtils.
                        if (((assignOper.InputTypes & Primitive.Struct) != 0) && PRNG.Decide(0.2) && CurrentScope.NumOfStructTypes > 0)
                        {
                            lhsExprType = CurrentScope.AllStructTypes[PRNG.Next(CurrentScope.NumOfStructTypes)];
                        }
                        else
                        {
                            lhsExprType = GetASTUtils().GetRandomExprType(assignOper.InputTypes);
                        }

                        if (assignOper.HasFlag(OpFlags.Shift))
                        {
                            rhsExprType = Tree.ValueType.ForPrimitive(Primitive.Int);
                        }
                        else
                        {
                            rhsExprType = lhsExprType;
                        }

                        ExprKind rhsKind = GetASTUtils().GetRandomExpressionReturningPrimitive(rhsExprType.PrimitiveType);
                        Expression lhs = ExprHelper(ExprKind.VariableExpression, lhsExprType, 0);
                        Expression rhs = ExprHelper(rhsKind, rhsExprType, 0);

                        return new AssignStatement(TC, lhs, assignOper, rhs);
                    }
                case StmtKind.ForStatement:
                    {
                        //Debug.Assert(depth <= TC.Config.MaxStmtDepth);

                        var forLoopScope = new Scope(TC, ScopeKind.LoopScope, CurrentScope);
                        var loopVar = CurrentScope.GetRandomVariable(Tree.ValueType.ForPrimitive(Primitive.Int));
                        var nestNum = depth;
                        var numOfSecondaryVars = PRNG.Next(/*GetOptions().MaxNumberOfSecondaryInductionVariable*/ 1 + 1);
                        var forLoopKind = Kind.ComplexLoop;

                        // 50% of the time, we'll make it a simple loop, 25% each normal and complex.
                        if (PRNG.Next(2) == 0)
                            forLoopKind = Kind.SimpleLoop;
                        else if (PRNG.Next(2) == 0)
                            forLoopKind = Kind.NormalLoop;

                        PushScope(forLoopScope);

                        var bounds = ExprHelper(GetASTUtils().GetRandomExpressionReturningPrimitive(Primitive.Int), Tree.ValueType.ForPrimitive(Primitive.Int), 0);
                        var loopStep = ExprHelper(GetASTUtils().GetRandomExpressionReturningPrimitive(Primitive.Int), Tree.ValueType.ForPrimitive(Primitive.Int), 0);

                        //TODO-imp: AddInductionVariables
                        //TODO-imp: ctrlFlowStack
                        //TODO future: label
                        List<Statement> forLoopBody = new List<Statement>();
                        for (int i = 0; i < TC.Config.MaxStatementCount; ++i)
                        {
                            StmtKind cur;
                            if (depth < TC.Config.MaxStmtDepth)
                            {
                                cur = GetASTUtils().GetRandomStatement();
                            }
                            else
                            {
                                cur = GetASTUtils().GetRandomTerminalStatement();
                            }
                            forLoopBody.Add(StatementHelper(cur, depth + 1));
                        }

                        //TODO: This can just append to forLoopBody
                        PopScope(variableName => LogVariable(forLoopBody, variableName)); // pop 'for-loop' body scope

                        return new ForStatement(TC, loopVar, nestNum, numOfSecondaryVars, bounds, forLoopBody, loopStep, forLoopKind);
                    }
                case StmtKind.DoWhileStatement:
                    {
                        //Debug.Assert(depth <= TC.Config.MaxStmtDepth);

                        Scope doWhileScope = new Scope(TC, ScopeKind.LoopScope, CurrentScope);

                        var nestNum = depth;
                        var numOfSecondaryVars = PRNG.Next(/*GetOptions().MaxNumberOfSecondaryInductionVariable*/ 1 + 1);

                        PushScope(doWhileScope);

                        var bounds = ExprHelper(GetASTUtils().GetRandomExpressionReturningPrimitive(Primitive.Boolean), Tree.ValueType.ForPrimitive(Primitive.Boolean), 0);

                        //TODO-imp: AddInductionVariables
                        //TODO-imp: ctrlFlowStack
                        //TODO future: label
                        List<Statement> whileLoopBody = new List<Statement>();

                        for (int i = 0; i < PRNG.Next(1, TC.Config.MaxStatementCount); ++i)
                        {
                            StmtKind cur;
                            if (depth < TC.Config.MaxStmtDepth)
                            {
                                cur = GetASTUtils().GetRandomStatement();
                            }
                            else
                            {
                                cur = GetASTUtils().GetRandomTerminalStatement();
                            }
                            whileLoopBody.Add(StatementHelper(cur, depth + 1));
                        }

                        PopScope(variableName => LogVariable(whileLoopBody, variableName)); // pop 'do-while' body scope

                        return new DoWhileStatement(TC, nestNum, numOfSecondaryVars, bounds, whileLoopBody);
                    }
                case StmtKind.WhileStatement:
                    {
                        //Debug.Assert(depth <= TC.Config.MaxStmtDepth);

                        Scope whileScope = new Scope(TC, ScopeKind.LoopScope, CurrentScope);

                        var nestNum = depth;
                        var numOfSecondaryVars = PRNG.Next(/*GetOptions().MaxNumberOfSecondaryInductionVariable*/ 1 + 1);

                        PushScope(whileScope);

                        var bounds = ExprHelper(GetASTUtils().GetRandomExpressionReturningPrimitive(Primitive.Boolean), Tree.ValueType.ForPrimitive(Primitive.Boolean), 0);

                        //TODO-imp: AddInductionVariables
                        //TODO-imp: ctrlFlowStack
                        //TODO future: label
                        List<Statement> whileLoopBody = new List<Statement>();

                        for (int i = 0; i < PRNG.Next(1, TC.Config.MaxStatementCount); ++i)
                        {
                            StmtKind cur;
                            if (depth < TC.Config.MaxStmtDepth)
                            {
                                cur = GetASTUtils().GetRandomStatement();
                            }
                            else
                            {
                                cur = GetASTUtils().GetRandomTerminalStatement();
                            }
                            whileLoopBody.Add(StatementHelper(cur, depth + 1));
                        }

                        PopScope(variableName => LogVariable(whileLoopBody, variableName)); // pop 'while' body scope

                        return new WhileStatement(TC, nestNum, numOfSecondaryVars, bounds, whileLoopBody);
                    }
                case StmtKind.ReturnStatement:
                    {
                        Tree.ValueType returnType = MethodSignature.ReturnType;
                        if (returnType.PrimitiveType == Primitive.Void)
                        {
                            return new ReturnStatement(TC, null);
                        }

                        Expression returnExpr = ExprHelper(GetASTUtils().GetRandomExpressionReturningPrimitive(returnType.PrimitiveType), returnType, 0);
                        return new ReturnStatement(TC, returnExpr);
                    }
                case StmtKind.TryCatchFinallyStatement:
                    {
                        //Debug.Assert(depth <= TC.Config.MaxStmtDepth);

                        int catchCounts = PRNG.Next(0, TC.Config.CatchClausesCount);

                        // If there are no catch, then definitely add finally, otherwise skip it.
                        bool hasFinally = catchCounts == 0 || PRNG.Decide(TC.Config.FinallyClauseProbability);
                        List<Statement> tryBody = new List<Statement>();

                        Scope tryScope = new Scope(TC, ScopeKind.BracesScope, CurrentScope);
                        PushScope(tryScope);

                        int tryStmtCount = PRNG.Next(1, TC.Config.MaxStatementCount);
                        for (int i = 0; i < tryStmtCount; i++)
                        {
                            StmtKind cur;
                            if (depth < TC.Config.MaxStmtDepth)
                            {
                                cur = GetASTUtils().GetRandomStatement();
                            }
                            else
                            {
                                cur = GetASTUtils().GetRandomTerminalStatement();
                            }
                            tryBody.Add(StatementHelper(cur, depth + 1));
                        }
                        PopScope(variableName => LogVariable(tryBody, variableName)); // pop 'try' body scope

                        List<Tuple<Type, List<Statement>>> catchClauses = new ();

                        var allExceptions = Tree.ValueType.AllExceptions.Select(x => x.Key).ToList();
                        var caughtExceptions = new List<Type>();

                        for (int catchId = 0; catchId < catchCounts; catchId++)
                        {
                            var exceptionToCatch = allExceptions[PRNG.Next(allExceptions.Count())];
                            allExceptions.Remove(exceptionToCatch);

                            // If we already generated a catch-clause of superclass, skip remaining catch clauses.
                            if (caughtExceptions.Any(x => exceptionToCatch.IsSubclassOf(x)))
                            {
                                break;
                            }
                            caughtExceptions.Add(exceptionToCatch);

                            int catchStmtCount = PRNG.Next(1, TC.Config.MaxStatementCount);
                            List<Statement> catchBody = new List<Statement>();

                            Scope catchScope = new Scope(TC, ScopeKind.BracesScope, CurrentScope);
                            PushScope(catchScope);

                            for (int i = 0; i < catchStmtCount; i++)
                            {
                                StmtKind cur;
                                if (depth < TC.Config.MaxStmtDepth)
                                {
                                    cur = GetASTUtils().GetRandomStatement();
                                }
                                else
                                {
                                    cur = GetASTUtils().GetRandomTerminalStatement();
                                }
                                catchBody.Add(StatementHelper(cur, depth + 1));
                            }
                            PopScope(variableName => LogVariable(catchBody, variableName)); // pop 'catch' body scope
                            catchClauses.Add(Tuple.Create(exceptionToCatch, catchBody));
                        }

                        List<Statement> finallyBody = new List<Statement>();
                        if (hasFinally)
                        {
                            Scope finallyScope = new Scope(TC, ScopeKind.BracesScope, CurrentScope);
                            PushScope(finallyScope);

                            int finallyStmtCount = PRNG.Next(1, TC.Config.MaxStatementCount);
                            for (int i = 0; i < finallyStmtCount; i++)
                            {
                                StmtKind cur;
                                if (depth < TC.Config.MaxStmtDepth)
                                {
                                    cur = GetASTUtils().GetRandomStatement();
                                }
                                else
                                {
                                    cur = GetASTUtils().GetRandomTerminalStatement();
                                }
                                finallyBody.Add(StatementHelper(cur, depth + 1));
                            }
                            PopScope(variableName => LogVariable(finallyBody, variableName)); // pop 'finally' body scope
                        }

                        return new TryCatchFinallyStatement(TC, tryBody, catchClauses, finallyBody);
                    }
                case StmtKind.SwitchStatement:
                    {
                        //Debug.Assert(depth <= TC.Config.MaxStmtDepth);

                        int caseCount = PRNG.Next(1, TC.Config.MaxCaseCounts);

                        Primitive switchType = new Primitive[] { Primitive.Int, Primitive.Long, Primitive.Char, Primitive.String }[PRNG.Next(4)];
                        Tree.ValueType switchExprType = Tree.ValueType.ForPrimitive(switchType);
                        ExprKind switchExprKind = GetASTUtils().GetRandomExpressionReturningPrimitive(switchType);
                        Expression switchExpr = ExprHelper(switchExprKind, switchExprType, 0);
                        //IList<SwitchSectionSyntax> listOfCases = new List<SwitchSectionSyntax>();
                        List<Tuple<ConstantValue, List<Statement>>> listOfCases = new ();
                        HashSet<string> usedCaseLabels = new HashSet<string>();

                        // Generate each cases
                        for (int i = 0; i < caseCount; i++)
                        {
                            Scope caseScope = new Scope(TC, ScopeKind.BracesScope, CurrentScope);
                            PushScope(caseScope);

                            // Generate statements within each cases
                            int caseStmtCount = PRNG.Next(1, TC.Config.MaxStatementCount);
                            List<Statement> caseBody = new List<Statement>();
                            for (int j = 0; j < caseStmtCount; j++)
                            {
                                StmtKind cur;
                                if (depth < TC.Config.MaxStmtDepth)
                                {
                                    cur = GetASTUtils().GetRandomStatement();
                                }
                                else
                                {
                                    cur = GetASTUtils().GetRandomTerminalStatement();
                                }
                                caseBody.Add(StatementHelper(cur, depth + 1));
                            }

                            PopScope(variableName => LogVariable(caseBody, variableName)); // pop 'case' body scope

                            ConstantValue caseConstantValue;
                            do
                            {
                                caseConstantValue = ExprHelper(ExprKind.LiteralExpression, switchExprType, 0) as ConstantValue;
                            } while (!usedCaseLabels.Add(caseConstantValue.Value));

                            listOfCases.Add(Tuple.Create(caseConstantValue, caseBody));
                        }

                        // Generate default
                        var defaultScope = new Scope(TC, ScopeKind.BracesScope, CurrentScope);
                        PushScope(defaultScope);

                        // Generate statements within default
                        int defaultStmtCount = PRNG.Next(1, 3);
                        List<Statement> defaultBody = new List<Statement>();
                        for (int j = 0; j < defaultStmtCount; j++)
                        {
                            StmtKind cur;
                            if (depth < TC.Config.MaxStmtDepth)
                            {
                                cur = GetASTUtils().GetRandomStatement();
                            }
                            else
                            {
                                cur = GetASTUtils().GetRandomTerminalStatement();
                            }
                            defaultBody.Add(StatementHelper(cur, depth + 1));
                        }

                        PopScope(variableName => LogVariable(defaultBody, variableName)); // pop 'default' body scope

                        return new SwitchStatement(TC, switchExpr, listOfCases, defaultBody);
                    }
                case StmtKind.MethodCallStatement:
                    {
                        return new MethodCallStatement(TC, MethodCallHelper(_testClass.GetRandomMethod(), 0));
                    }
                default:
                    Debug.Assert(false, string.Format("Hit unknown statement type {0}", Enum.GetName(typeof(StmtKind), stmtKind)));
                    break;
            }
            return null;
        }

        /// <summary>
        ///     Generate statement of <paramref name="exprKind"/> that returns a value of <paramref name="exprType"/>
        ///     type. It will generate terminal expression if <paramref name="depth"/> exceeds the threshold.
        /// </summary>
        /// <param name="exprKind"></param>
        /// <param name="exprType"></param>
        /// <param name="depth"></param>
        /// <returns></returns>
        public Expression ExprHelper(ExprKind exprKind, Tree.ValueType exprType, int depth)
        {
            //Debug.Assert(depth <= TC.Config.MaxExprDepth);

            switch (exprKind)
            {
                case ExprKind.LiteralExpression:
                    {
                        return ConstantValue.GetConstantValue(exprType, TC._numerals);
                    }

                case ExprKind.VariableExpression:
                    {
                        return new VariableExpression(TC, CurrentScope.GetRandomVariable(exprType));
                    }

                case ExprKind.BinaryOpExpression:
                    {
                        //Debug.Assert(depth <= TC.Config.MaxExprDepth);

                        Primitive returnType = exprType.PrimitiveType;

                        Operator op = GetASTUtils().GetRandomBinaryOperator(returnPrimitiveType: returnType);

                        // If the return type is boolean, then take any ExprType that returns boolean.
                        // However for other types, choose the same type for BinOp expression as the one used to store the result on LHS.
                        //TODO-future: Consider doing GetRandomExprType(op.InputTypes) below. Currently, if this is done,
                        // we end up getting code like (short)(1233342432.5M + 35435435.5M), where "short" is the exprType and
                        // the literals are selected of different type ("decimal" in this example) and we get compilation error
                        // because they can't be casted to short.
                        Tree.ValueType lhsExprType = GetASTUtils().GetRandomExprType(returnType == Primitive.Boolean ? op.InputTypes : returnType);
                        //Tree.ValueType lhsExprType = GetASTUtils().GetRandomExprType(op.InputTypes);
                        Tree.ValueType rhsExprType = lhsExprType;

                        if (op.HasFlag(OpFlags.Shift))
                        {
                            rhsExprType = Tree.ValueType.ForPrimitive(Primitive.Int);
                        }

                        ExprKind lhsExprKind, rhsExprKind;
                        if (depth < TC.Config.MaxExprDepth)
                        {
                            lhsExprKind = GetASTUtils().GetRandomExpressionReturningPrimitive(lhsExprType.PrimitiveType);
                            rhsExprKind = GetASTUtils().GetRandomExpressionReturningPrimitive(rhsExprType.PrimitiveType);
                        }
                        else
                        {
                            lhsExprKind = GetRandomTerminalExpression(exprType);
                            rhsExprKind = GetRandomTerminalExpression(exprType);
                        }

                        // Fold arithmetic binop expressions that has constants.
                        // csc.exe would automatically fold that for us, but by doing it here, we eliminate generate 
                        // errors during compiling the test case.
                        if (op.HasFlag(OpFlags.Math) && lhsExprKind == ExprKind.LiteralExpression && rhsExprKind == ExprKind.LiteralExpression)
                        {
                            return ConstantValue.GetConstantValue(exprType, TC._numerals);
                        }

                        Expression lhs = ExprHelper(lhsExprKind, lhsExprType, depth + 1);
                        Expression rhs = ExprHelper(rhsExprKind, rhsExprType, depth + 1);

                        return new CastExpression(TC, new BinaryExpression(TC, lhs, op, rhs), exprType);
                    }
                case ExprKind.AssignExpression:
                    {
                        //Debug.Assert(depth <= TC.Config.MaxExprDepth);

                        Tree.Operator assignOper = GetASTUtils().GetRandomAssignmentOperator(returnPrimitiveType: exprType.PrimitiveType);
                        Tree.ValueType lhsExprType, rhsExprType;
                        lhsExprType = rhsExprType = exprType;

                        if (assignOper.HasFlag(OpFlags.Shift))
                        {
                            rhsExprType = Tree.ValueType.ForPrimitive(Primitive.Int);
                        }


                        ExprKind rhsKind;
                        if (depth < TC.Config.MaxExprDepth)
                        {
                            rhsKind = GetASTUtils().GetRandomExpressionReturningPrimitive(rhsExprType.PrimitiveType);
                        }
                        else
                        {
                            rhsKind = GetRandomTerminalExpression(rhsExprType);
                        }

                        Expression lhs = ExprHelper(ExprKind.VariableExpression, lhsExprType, depth + 1);
                        Expression rhs = ExprHelper(rhsKind, rhsExprType, depth + 1);

                        return new CastExpression(TC, new AssignExpression(TC, lhs, assignOper, rhs), exprType);
                    }
                case ExprKind.MethodCallExpression:
                    {
                        if (depth < TC.Config.MaxExprDepth)
                        {
                            return MethodCallHelper(_testClass.GetRandomMethod(exprType), depth + 1);
                        }
                        else
                        {
                            return MethodCallHelper(_testClass.GetRandomLeafMethod(exprType), depth + 1);
                        }
                    }

                default:
                    Debug.Assert(false, string.Format("Hit unknown expression type {0}", Enum.GetName(typeof(ExprKind), exprKind)));
                    break;
            }
            return null;
        }

        /// <summary>
        ///     Generates assignment for variable name
        /// </summary>
        /// <returns></returns>
        public StatementSyntax VariableAssignmentHelper(Tree.ValueType exprType, string variableName)
        {
            ExpressionSyntax lhs = Annotate(TestCase.GetExpressionSyntax(variableName), "specific-Var");
            ExpressionSyntax rhs;

            int noOfAttempts = TC.Config.NumOfAttemptsForExpression;
            do
            {
                rhs = ExprHelper(GetASTUtils().GetRandomExpressionReturningPrimitive(exprType.PrimitiveType), exprType, 0);
                // Make sure that we do not end up with same lhs=lhs.
                if (lhs.ToFullString() != rhs.ToFullString())
                {
                    break;
                }
            } while (noOfAttempts++ < 5);
            Debug.Assert(lhs.ToFullString() != rhs.ToFullString());

            return Annotate(ExpressionStatement(AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, lhs, rhs)), "specific-Assign", 0);
        }

        /// <summary>
        ///     Makes sure to pick either  a method call that has valid return type
        ///     or one of the variable or literal expression.
        /// </summary>
        /// <param name="exprType"></param>
        /// <returns></returns>
        private ExprKind GetRandomTerminalExpression(Tree.ValueType exprType)
        {
            ExprKind kind;

            int noOfAttempts = TC.Config.NumOfAttemptsForExpression;
            bool found = false;

            do
            {
                kind = GetASTUtils().GetRandomTerminalExpression();
                switch (kind)
                {
                    case ExprKind.LiteralExpression:
                        {
                            if (exprType.PrimitiveType != Primitive.Struct)
                            {
                                found = true;
                            }
                            break;
                        }
                    case ExprKind.VariableExpression:
                        {
                            found = true;
                            break;
                        }
                    case ExprKind.MethodCallExpression:
                        {
                            // If terminal expression is a method call, make sure we have a method that 
                            // returns value of "exprType"
                            if (_testClass.GetRandomMethod(exprType) != null)
                            {
                                found = true;
                                break;
                            }
                            break;
                        }
                    default:
                        throw new Exception("Unsupported terminal expression");

                }

                if (found)
                {
                    break;
                }
            } while (noOfAttempts++ < 5);

            if (!found)
            {
                if (exprType.PrimitiveType == Primitive.Struct)
                {
                    kind = ExprKind.VariableExpression;
                }
                else
                {
                    kind = PRNG.Decide(0.7) ? ExprKind.VariableExpression : ExprKind.LiteralExpression;
                }
            }

            return kind;
        }

        private Expression MethodCallHelper(MethodSignature methodSig, int depth)
        {
            List<Expression> argumentNodes = new List<Expression>();
            List<ParamValuePassing> passingWays = new List<ParamValuePassing>();

            int paramsCount = methodSig.Parameters.Count;

            for (int paramId = 0; paramId < paramsCount; paramId++)
            {
                MethodParam parameter = methodSig.Parameters[paramId];

                Tree.ValueType argType = parameter.ParamType;
                ExprKind argExprKind;

                if (parameter.PassingWay == ParamValuePassing.None)
                {
                    if (depth < TC.Config.MaxExprDepth)
                    {
                        argExprKind = GetASTUtils().GetRandomExpressionReturningPrimitive(argType.PrimitiveType);
                    }
                    else
                    {
                        argExprKind = GetRandomTerminalExpression(argType);
                    }
                }
                else
                {
                    argExprKind = ExprKind.VariableExpression;
                }

                Expression argExpr = ExprHelper(argExprKind, argType, depth + 1);

                passingWays.Add(parameter.PassingWay);
                argumentNodes.Add(argExpr);
            }

            return new MethodCallExpression(methodSig.MethodName, argumentNodes, passingWays);
        }

        private Tree.ValueType GetRandomExprType()
        {
            if (PRNG.Decide(TC.Config.StructUsageProbability) && CurrentScope.NumOfStructTypes > 0)
            {
                return CurrentScope.AllStructTypes[PRNG.Next(CurrentScope.NumOfStructTypes)];
            }
            else
            {
                return GetASTUtils().GetRandomExprType();
            }
        }

        private ExpressionSyntax Annotate(ExpressionSyntax expression, string comment)
        {
#if DEBUG
            string typeName = expression.GetType().Name;
            if (!_expressionsCount.ContainsKey(typeName))
            {
                _expressionsCount[typeName] = 0;
            }
            _expressionsCount[typeName]++;
            return expression.WithTrailingTrivia(TriviaList(Comment($"/* E#{_expressionsCount[typeName]}: {comment} */")));
#else
            return expression;
#endif
        }

        private StatementSyntax Annotate(StatementSyntax statement, string comment, int depth)
        {
#if DEBUG
            string typeName = statement.GetType().Name;
            if (!_statementsCount.ContainsKey(typeName))
            {
                _statementsCount[typeName] = 0;
            }
            _statementsCount[typeName]++;
            return statement.WithTrailingTrivia(TriviaList(Comment($"/* {depth}: S#{_statementsCount[typeName]}: {comment} */")));
#else
            return statement;
#endif
        }


        /// <summary>
        ///     Generate log invoke statement
        /// </summary>
        /// <param name="variableName"></param>
        /// <returns></returns>
        public static StatementSyntax GetLogInvokeStatement(string variableName)
        {
            return ExpressionStatement(
                PreGenerated.LogInvocationExpression
                .WithArgumentList(
                    ArgumentList(
                    SeparatedList<ArgumentSyntax>(
                        new SyntaxNodeOrToken[]
                        {
                            // For variable names, just take 10 characters for longer variable names
                            Argument(LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(variableName.Substring(0, Math.Min(variableName.Length, 10))))),
                            Token(SyntaxKind.CommaToken),
                            Argument(TestCase.GetExpressionSyntax(variableName))
                        }
             ))));
        }
    }

    public class MethodSignature
    {
        public string MethodName;
        public Tree.ValueType ReturnType;
        public List<MethodParam> Parameters;
        public bool IsLeaf;

        public MethodSignature(string methodName, bool isLeaf = false)
        {
            MethodName = methodName;
            ReturnType = Tree.ValueType.ForVoid();
            Parameters = new List<MethodParam>();
            IsLeaf = isLeaf;
        }

        public override bool Equals(object obj)
        {
            if (obj is not MethodSignature otherMethodSig)
            {
                return false;
            }
            return MethodName == otherMethodSig.MethodName &&
                (ReturnType.Equals(otherMethodSig.ReturnType)) &&
                Parameters.Count == otherMethodSig.Parameters.Count &&
                Enumerable.Range(0, Parameters.Count)
                    .All(pIndex => (Parameters[pIndex].ParamType.Equals(otherMethodSig.Parameters[pIndex].ParamType) &&
                                    (Parameters[pIndex].PassingWay == otherMethodSig.Parameters[pIndex].PassingWay)));
        }

        public override int GetHashCode()
        {
            int hashCode = 0;
            foreach (var p in Parameters)
            {
                hashCode ^= p.ParamType.GetHashCode();
            }
            return MethodName.GetHashCode() ^ ReturnType.GetHashCode() ^ hashCode;
        }

        public override string ToString()
        {
            var paramList = Parameters.Select(p => $"{(p.PassingWay == ParamValuePassing.None ? "" : Enum.GetName(typeof(ParamValuePassing), p.PassingWay))} {p.ParamType}");
            return $"{ReturnType} {MethodName}({string.Join(", ", paramList)})";
        }
    }

    public class MethodParam
    {
        public string ParamName;
        public Tree.ValueType ParamType;
        public ParamValuePassing PassingWay;

        public override string ToString()
        {
            return $"{Enum.GetName(typeof(ParamValuePassing), PassingWay)} {ParamType} {ParamName}";
        }
    }

    public enum ParamValuePassing
    {
        None,
        //TODO-future: need to add ability of marking variables readonly
        //In,
        Out,
        Ref
    };

    public class TestLeafMethod : TestMethod
    {
        private Tree.ValueType _returnType;
        public TestLeafMethod(TestClass enclosingClass, string methodName, Tree.ValueType returnType)
            : base(enclosingClass, methodName)
        {
            _returnType = returnType;
        }

        protected override MethodDeclarationSyntax GenerateMethodSignature()
        {
            MethodSignature = new MethodSignature(Name, isLeaf: true)
            {
                Parameters = new List<MethodParam>(),
                ReturnType = _returnType
            };

            var methodDecl = MethodDeclaration(Helpers.GetTypeSyntax(_returnType), Name)
                .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)));

            //TODO-config: 50% of time, add attribute of NoInline
            if (PRNG.Decide(0.5))
            {
                methodDecl = methodDecl.WithAttributeLists(Helpers.NoInlineAttr);
            }

            return methodDecl;
        }

        public override MethodDeclarationSyntax Generate()
        {
            GetASTUtils().EnterLeafMethod();

            // return statement
            MethodDeclarationSyntax methodDeclaration = GenerateMethodSignature();
            IList<StatementSyntax> methodBody = new List<StatementSyntax>();

            methodBody.Add(StatementHelper(StmtKind.ReturnStatement, 0));

            RegisterMethod(MethodSignature);

            // Wrap everything in unchecked so we do not see overflow compilation errors
            var leafMethod = methodDeclaration.WithBody(Block(CheckedStatement(SyntaxKind.UncheckedStatement, Block(methodBody))));
            GetASTUtils().LeaveLeafMethod();
            return leafMethod;
        }
    }
}
