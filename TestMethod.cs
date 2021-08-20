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
        protected readonly int _stmtCount;

        //TODO-config: Move this to ConfigOptions
        private static readonly int s_maxStatements = 8;

#if DEBUG
        private Dictionary<string, int> expressionsCount = new Dictionary<string, int>();
        private Dictionary<string, int> statementsCount = new Dictionary<string, int>();
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

        protected Scope PopScope()
        {
            Scope ret = _testClass.PopScope();
            //Debug.Assert(ret.Parent == ScopeStack.Peek());
            return ret;
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
        protected TestMethod(TestClass enclosingClass, string methodName, int stmtCount)
        {
            _testClass = enclosingClass;
            Name = methodName;
            MethodScope = new Scope(enclosingClass.TC, ScopeKind.FunctionScope, enclosingClass.ClassScope);
            _stmtCount = stmtCount;
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

            //TODO-config: Statements in a function
            _stmtCount = PRNG.Next(1, s_maxStatements);
        }

        public virtual MethodDeclarationSyntax Generate()
        {
            PushScope(MethodScope);

            MethodDeclarationSyntax methodDeclaration = GenerateMethodSignature();
            IList<StatementSyntax> methodBody = new List<StatementSyntax>();

            // TODO-TEMP initialize one variable of each type
            foreach (Tree.ValueType variableType in Tree.ValueType.GetTypes())
            {
                //TODO-config: Only declare again 20% of variables
                if (PRNG.Decide(0.8))
                {
                    continue;
                }

                string variableName = Helpers.GetVariableName(variableType, _variablesCount++);

                ExpressionSyntax rhs = ExprHelper(ExprKind.LiteralExpression, variableType, 0);
                CurrentScope.AddLocal(variableType, variableName);

                methodBody.Add(Annotate(LocalDeclarationStatement(Helpers.GetVariableDeclaration(variableType, variableName, rhs)), "var-init", 0));
            }

            // TODO-TEMP initialize one variable of each struct type
            foreach (Tree.ValueType structType in CurrentScope.AllStructTypes)
            {
                //TODO-config: Only declare again 20% of variables
                if (PRNG.Decide(0.8))
                {
                    continue;
                }

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
            }

            // TODO-TEMP initialize out and ref method parameters
            var paramsToInitialize = MethodSignature.Parameters.Where(p => p.PassingWay == ParamValuePassing.Out);
            foreach (MethodParam param in paramsToInitialize)
            {
                methodBody.Add(VariableAssignmentHelper(param.ParamType, param.ParamName));
                CurrentScope.AddLocal(param.ParamType, param.ParamName);
            }

            for (int i = 0; i < _stmtCount; i++)
            {
                StmtKind cur = GetASTUtils().GetRandomStatemet();
                methodBody.Add(StatementHelper(cur, 0));
            }

            // For main invocation method, invoke all the other methods once
            if (_isMainInvocation)
            {
                foreach (var nonLeafMethod in _testClass.AllNonLeafMethods)
                {
                    //TODO-future: Select any assignOper
                    //Tree.Operator assignOper = GetASTUtils().GetRandomAssignmentOperator();

                    ExpressionSyntax lhs = ExprHelper(ExprKind.VariableExpression, nonLeafMethod.ReturnType, 0);
                    ExpressionSyntax rhs = MethodCallHelper(nonLeafMethod, 0);

                    methodBody.Add(Annotate(ExpressionStatement(AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, lhs, rhs)), "MethodCall-Assign", 0));
                }

                // print all static variables
                foreach (var variableName in _testClass.ClassScope.AllVariables)
                {
                    methodBody.Add(ParseStatement($"Log(\"{variableName}\", {variableName});"));
                }
            }

            // If only statement in method is a return statement,
            // do not print the variables we generated above.
            if (_stmtCount > 0)
            {
                // print all variables
                foreach (var variableName in CurrentScope.AllVariables)
                {
                    methodBody.Add(ParseStatement($"Log(\"{variableName}\", {variableName});"));
                }
            }

            // return statement
            methodBody.Add(StatementHelper(StmtKind.ReturnStatement, 0));

            PopScope();

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
                //TODO:config - No. of parameters
                numOfParameters = PRNG.Next(1, 10);
                MethodSignature.ReturnType = GetRandomExprType(structProbability: 0.7);
            }

            List<MethodParam> parameters = new List<MethodParam>();
            MethodSignature.Parameters = parameters;
            List<SyntaxNodeOrToken> parameterNodes = new List<SyntaxNodeOrToken>();

            for (int paramIndex = 0; paramIndex < numOfParameters; paramIndex++)
            {
                var paramType = GetRandomExprType(structProbability: 0.7);
                var passingWay = PRNG.WeightedChoice(MethodSignature.ValuePassing);
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

        public StatementSyntax StatementHelper(StmtKind stmtKind, int depth)
        {
            switch (stmtKind)
            {
                case StmtKind.VariableDeclaration:
                    {
                        Tree.ValueType variableType = GetRandomExprType(structProbability: 0.3);

                        string variableName = Helpers.GetVariableName(variableType, _variablesCount++);

                        ExpressionSyntax rhs = ExprHelper(GetASTUtils().GetRandomExpressionReturningPrimitive(variableType.PrimitiveType), variableType, 0);
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

                        return Annotate(LocalDeclarationStatement(Helpers.GetVariableDeclaration(variableType, variableName, rhs)), "VarDecl", depth);
                    }
                case StmtKind.IfElseStatement:
                    {
                        Tree.ValueType condValueType = Tree.ValueType.ForPrimitive(Primitive.Boolean);
                        ExpressionSyntax conditionExpr = ExprHelper(GetASTUtils().GetRandomExpressionReturningPrimitive(Primitive.Boolean), condValueType, 0);

                        Scope ifBranchScope = new Scope(TC, ScopeKind.ConditionalScope, CurrentScope);
                        Scope elseBranchScope = new Scope(TC, ScopeKind.ConditionalScope, CurrentScope);

                        //TODO-config: Add MaxDepth in config
                        int ifcount = PRNG.Next(1, s_maxStatements);
                        IList<StatementSyntax> ifBody = new List<StatementSyntax>();

                        PushScope(ifBranchScope);
                        for (int i = 0; i < ifcount; i++)
                        {
                            StmtKind cur;
                            //TODO-config: Add MaxDepth in config
                            if (depth >= 2)
                            {
                                cur = StmtKind.VariableDeclaration;
                            }
                            else
                            {
                                cur = GetASTUtils().GetRandomStatemet();
                            }
                            ifBody.Add(StatementHelper(cur, depth + 1));
                        }
                        PopScope(); // pop 'if' body scope

                        int elsecount = PRNG.Next(1, s_maxStatements);
                        IList<StatementSyntax> elseBody = new List<StatementSyntax>();

                        PushScope(elseBranchScope);
                        for (int i = 0; i < elsecount; i++)
                        {
                            StmtKind cur;
                            //TODO-config: Add MaxDepth in config
                            if (depth >= 2)
                            {
                                cur = StmtKind.VariableDeclaration;
                            }
                            else
                            {
                                cur = GetASTUtils().GetRandomStatemet();
                            }
                            elseBody.Add(StatementHelper(cur, depth + 1));
                        }
                        PopScope(); // pop 'else' body scope

                        return Annotate(IfStatement(conditionExpr, Block(ifBody), ElseClause(Block(elseBody))), "IfElse", depth);
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

                        ExpressionSyntax lhs = ExprHelper(ExprKind.VariableExpression, lhsExprType, depth);
                        ExpressionSyntax rhs = null;

                        //TODO-config no. of attempts
                        int noOfAttempts = 0;
                        while (noOfAttempts++ < 5)
                        {
                            rhs = ExprHelper(GetASTUtils().GetRandomExpressionReturningPrimitive(rhsExprType.PrimitiveType), rhsExprType, depth);
                            // Make sure that we do not end up with same lhs=lhs.
                            if (lhs.ToFullString() != rhs.ToFullString())
                            {
                                break;
                            }
                        }
                        Debug.Assert(lhs.ToFullString() != rhs.ToFullString());

                        // For division, make sure that divisor is not 0
                        if ((assignOper.Oper == SyntaxKind.DivideAssignmentExpression) || (assignOper.Oper == SyntaxKind.ModuloAssignmentExpression))
                        {
                            rhs = ParenthesizedExpression(BinaryExpression(SyntaxKind.AddExpression, ParenthesizedExpression(rhs), LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(PRNG.Next(10, 100)))));
                            rhs = Helpers.GetWrappedAndCastedExpression(rhsExprType, lhsExprType, rhs);
                        }

                        return Annotate(ExpressionStatement(AssignmentExpression(assignOper.Oper, lhs, rhs)), "Assign", depth);
                    }
                case StmtKind.ForStatement:
                    {
                        Scope forLoopScope = new Scope(TC, ScopeKind.LoopScope, CurrentScope);
                        ForStatement forStmt = new ForStatement(TC);
                        //TODO:config
                        int n = PRNG.Next(1, s_maxStatements);
                        forStmt.LoopVar = CurrentScope.GetRandomVariable(Tree.ValueType.ForPrimitive(Primitive.Int));
                        forStmt.NestNum = depth;
                        forStmt.NumOfSecondaryInductionVariables = PRNG.Next(/*GetOptions().MaxNumberOfSecondaryInductionVariable*/ 1 + 1);

                        // 50% of the time, we'll make it a simple loop, 25% each normal and complex.
                        if (PRNG.Next(2) == 0)
                            forStmt.LoopKind = Statements.ForStatement.Kind.SimpleLoop;
                        else if (PRNG.Next(2) == 0)
                            forStmt.LoopKind = Statements.ForStatement.Kind.NormalLoop;
                        else
                            forStmt.LoopKind = Statements.ForStatement.Kind.ComplexLoop;

                        PushScope(forLoopScope);

                        forStmt.Bounds = ExprHelper(GetASTUtils().GetRandomExpressionReturningPrimitive(Primitive.Int), Tree.ValueType.ForPrimitive(Primitive.Int), 0);
                        forStmt.LoopStep = ExprHelper(GetASTUtils().GetRandomExpressionReturningPrimitive(Primitive.Int), Tree.ValueType.ForPrimitive(Primitive.Int), 0);

                        //TODO-imp: AddInductionVariables
                        //TODO-imp: ctrlFlowStack
                        //TODO future: label
                        for (int i = 0; i < n; ++i)
                        {
                            StmtKind cur;
                            if (depth >= 2)
                            {
                                cur = StmtKind.VariableDeclaration;
                            }
                            else
                            {
                                cur = GetASTUtils().GetRandomStatemet();
                            }
                            forStmt.AddToBody(StatementHelper(cur, depth + 1));
                        }

                        PopScope(); // pop for-loop scope

                        return Annotate(Block(forStmt.Generate(false)), "for-loop", depth);
                    }
                case StmtKind.DoWhileStatement:
                    {
                        Scope doWhileScope = new Scope(TC, ScopeKind.LoopScope, CurrentScope);
                        DoWhileStatement doStmt = new DoWhileStatement(TC);
                        //TODO:config
                        int n = PRNG.Next(1, s_maxStatements);
                        doStmt.NestNum = depth;
                        doStmt.NumOfSecondaryInductionVariables = PRNG.Next(/*GetOptions().MaxNumberOfSecondaryInductionVariable*/ 1 + 1);

                        PushScope(doWhileScope);

                        doStmt.Bounds = ExprHelper(GetASTUtils().GetRandomExpressionReturningPrimitive(Primitive.Boolean), Tree.ValueType.ForPrimitive(Primitive.Boolean), 0);

                        //TODO-imp: AddInductionVariables
                        //TODO-imp: ctrlFlowStack
                        //TODO future: label
                        for (int i = 0; i < n; ++i)
                        {
                            StmtKind cur;
                            if (depth >= 2)
                            {
                                cur = StmtKind.VariableDeclaration;
                            }
                            else
                            {
                                cur = GetASTUtils().GetRandomStatemet();
                            }
                            doStmt.AddToBody(StatementHelper(cur, depth + 1));
                        }

                        PopScope(); // pop do-while scope
                        return Annotate(Block(doStmt.Generate(false)), "do-while", depth);
                    }
                case StmtKind.WhileStatement:
                    {
                        Scope whileScope = new Scope(TC, ScopeKind.LoopScope, CurrentScope);
                        WhileStatement whileStmt = new WhileStatement(TC);
                        //TODO:config
                        int n = PRNG.Next(1, s_maxStatements);
                        whileStmt.NestNum = depth;
                        whileStmt.NumOfSecondaryInductionVariables = PRNG.Next(/*GetOptions().MaxNumberOfSecondaryInductionVariable*/ 1 + 1);

                        PushScope(whileScope);

                        whileStmt.Bounds = ExprHelper(GetASTUtils().GetRandomExpressionReturningPrimitive(Primitive.Boolean), Tree.ValueType.ForPrimitive(Primitive.Boolean), 0);

                        //TODO-imp: AddInductionVariables
                        //TODO-imp: ctrlFlowStack
                        //TODO future: label
                        for (int i = 0; i < n; ++i)
                        {
                            StmtKind cur;
                            if (depth >= 2)
                            {
                                cur = StmtKind.VariableDeclaration;
                            }
                            else
                            {
                                cur = GetASTUtils().GetRandomStatemet();
                            }
                            whileStmt.AddToBody(StatementHelper(cur, depth + 1));
                        }

                        PopScope(); // pop while scope
                        return Annotate(Block(whileStmt.Generate(false)), "while-loop", depth);
                    }
                case StmtKind.ReturnStatement:
                    {
                        Tree.ValueType returnType = MethodSignature.ReturnType;
                        if (returnType.PrimitiveType == Primitive.Void)
                        {
                            return Annotate(ReturnStatement(), "Return", depth);
                        }

                        ExpressionSyntax returnExpr = ExprHelper(GetASTUtils().GetRandomExpressionReturningPrimitive(returnType.PrimitiveType), returnType, depth);
                        return Annotate(ReturnStatement(returnExpr), "Return", depth);
                    }
                case StmtKind.TryCatchFinallyStatement:
                    {
                        //TODO-config: Add MaxDepth in config
                        int catchCounts = PRNG.Next(0, 3);

                        //TODO-config: Add finally weight in config
                        // If there are no catch, then definitely add finally, otherwise skip it.
                        bool hasFinally = catchCounts == 0 || PRNG.Decide(0.5);
                        IList<StatementSyntax> tryBody = new List<StatementSyntax>();

                        Scope tryScope = new Scope(TC, ScopeKind.BracesScope, CurrentScope);
                        PushScope(tryScope);

                        //TODO-config: Add MaxDepth in config
                        int tryStmtCount = PRNG.Next(1, s_maxStatements);
                        for (int i = 0; i < tryStmtCount; i++)
                        {
                            StmtKind cur;
                            //TODO-config: Add MaxDepth in config
                            if (depth >= 2)
                            {
                                cur = StmtKind.VariableDeclaration;
                            }
                            else
                            {
                                cur = GetASTUtils().GetRandomStatemet();
                            }
                            tryBody.Add(StatementHelper(cur, depth + 1));
                        }
                        PopScope(); // pop 'try' body scope

                        IList<CatchClauseSyntax> catchClauses = new List<CatchClauseSyntax>();
                        var allExceptions = Tree.ValueType.AllExceptions;
                        var caughtExceptions = new List<Type>();
                        for (int catchId = 0; catchId < catchCounts; catchId++)
                        {
                            var exceptionToCatch = allExceptions[PRNG.Next(allExceptions.Count)];
                            allExceptions.Remove(exceptionToCatch);

                            // If we already generated a catch-clause of superclass, skip remaining catch clauses.
                            if (caughtExceptions.Any(x => exceptionToCatch.IsSubclassOf(x)))
                            {
                                break;
                            }
                            caughtExceptions.Add(exceptionToCatch);

                            //TODO-config: Add MaxDepth in config
                            int catchStmtCount = PRNG.Next(1, s_maxStatements / 2);
                            IList<StatementSyntax> catchBody = new List<StatementSyntax>();

                            Scope catchScope = new Scope(TC, ScopeKind.BracesScope, CurrentScope);
                            PushScope(catchScope);

                            //TODO-config: Add MaxDepth in config
                            for (int i = 0; i < catchStmtCount; i++)
                            {
                                StmtKind cur;
                                //TODO-config: Add MaxDepth in config
                                if (depth >= 2)
                                {
                                    cur = StmtKind.VariableDeclaration;
                                }
                                else
                                {
                                    cur = GetASTUtils().GetRandomStatemet();
                                }
                                catchBody.Add(StatementHelper(cur, depth + 1));
                            }
                            PopScope(); // pop 'catch' body scope

                            catchClauses.Add(CatchClause(CatchDeclaration(IdentifierName(exceptionToCatch.Name)), null, Block(catchBody)));
                        }

                        IList<StatementSyntax> finallyBody = new List<StatementSyntax>();
                        if (hasFinally)
                        {
                            Scope finallyScope = new Scope(TC, ScopeKind.BracesScope, CurrentScope);
                            PushScope(finallyScope);

                            //TODO-config: Add MaxDepth in config
                            int finallyStmtCount = PRNG.Next(1, s_maxStatements);
                            for (int i = 0; i < finallyStmtCount; i++)
                            {
                                StmtKind cur;
                                //TODO-config: Add MaxDepth in config
                                if (depth >= 2)
                                {
                                    cur = StmtKind.VariableDeclaration;
                                }
                                else
                                {
                                    cur = GetASTUtils().GetRandomStatemet();
                                }
                                finallyBody.Add(StatementHelper(cur, depth + 1));
                            }
                            PopScope(); // pop 'finally' body scope
                        }

                        return Annotate(TryStatement(Block(tryBody), catchClauses.ToSyntaxList(), FinallyClause(Block(finallyBody))), "TryCatchFinally", depth);
                    }
                case StmtKind.SwitchStatement:
                    {
                        //TODO-config: Add CaseCount in config
                        int caseCount = PRNG.Next(2, 10);

                        Primitive switchType = new Primitive[] { Primitive.Int, Primitive.Long, Primitive.Char, Primitive.String }[PRNG.Next(4)];
                        Tree.ValueType switchExprType = Tree.ValueType.ForPrimitive(switchType);
                        ExprKind switchExprKind = GetASTUtils().GetRandomExpressionReturningPrimitive(switchType);
                        ExpressionSyntax switchExpr = ExprHelper(switchExprKind, switchExprType, 0);
                        IList<SwitchSectionSyntax> listOfCases = new List<SwitchSectionSyntax>();
                        HashSet<string> usedCaseLabels = new HashSet<string>();

                        // Generate each cases
                        for (int i = 0; i < caseCount; i++)
                        {
                            Scope caseScope = new Scope(TC, ScopeKind.BracesScope, CurrentScope);
                            PushScope(caseScope);

                            //TODO-config: Add no. of case statemets in config
                            // Generate statements within each cases
                            int caseStmtCount = PRNG.Next(1, 3);
                            IList<StatementSyntax> caseBody = new List<StatementSyntax>();
                            for (int j = 0; j < caseStmtCount; j++)
                            {
                                StmtKind cur;
                                //TODO-config: Add MaxDepth in config
                                if (depth >= 2)
                                {
                                    cur = StmtKind.VariableDeclaration;
                                }
                                else
                                {
                                    cur = GetASTUtils().GetRandomStatemet();
                                }
                                caseBody.Add(StatementHelper(cur, depth + 1));
                            }
                            caseBody.Add(BreakStatement());
                            PopScope(); // pop 'case' body scope


                            LiteralExpressionSyntax caseLiteralExpression;
                            do
                            {
                                caseLiteralExpression = ExprHelper(ExprKind.LiteralExpression, switchExprType, 0) as LiteralExpressionSyntax;
                            } while (!usedCaseLabels.Add(caseLiteralExpression.Token.ValueText));

                            listOfCases.Add(SwitchSection()
                                .WithLabels(SingletonList<SwitchLabelSyntax>(CaseSwitchLabel(caseLiteralExpression)))
                                .WithStatements(caseBody.ToSyntaxList()));
                        }

                        // Generate default
                        var defaultScope = new Scope(TC, ScopeKind.BracesScope, CurrentScope);
                        PushScope(defaultScope);

                        // Generate statements within default
                        int defaultStmtCount = PRNG.Next(1, 3);
                        IList<StatementSyntax> defaultBody = new List<StatementSyntax>();
                        for (int j = 0; j < defaultStmtCount; j++)
                        {
                            StmtKind cur;
                            //TODO-config: Add MaxDepth in config
                            if (depth >= 2)
                            {
                                cur = StmtKind.VariableDeclaration;
                            }
                            else
                            {
                                cur = GetASTUtils().GetRandomStatemet();
                            }
                            defaultBody.Add(StatementHelper(cur, depth + 1));
                        }
                        defaultBody.Add(BreakStatement());
                        PopScope(); // pop 'default' body scope

                        listOfCases.Add(
                            SwitchSection()
                            .WithLabels(
                                SingletonList<SwitchLabelSyntax>(
                                    DefaultSwitchLabel()))
                            .WithStatements(defaultBody.ToSyntaxList()));

                        return Annotate(SwitchStatement(switchExpr).WithSections(listOfCases.ToSyntaxList()), "SwitchCase", depth);
                    }
                case StmtKind.MethodCallStatement:
                    {
                        return Annotate(ExpressionStatement(MethodCallHelper(_testClass.GetRandomMethod(), depth)), "MethodCall", depth); ;
                    }
                default:
                    Debug.Assert(false, string.Format("Hit unknown statement type {0}", Enum.GetName(typeof(StmtKind), stmtKind)));
                    break;
            }
            return null;
        }

        public ExpressionSyntax ExprHelper(ExprKind exprKind, Tree.ValueType exprType, int depth)
        {
            switch (exprKind)
            {
                case ExprKind.LiteralExpression:
                    {
                        return Annotate(Helpers.GetLiteralExpression(exprType), "Literal");
                    }

                case ExprKind.VariableExpression:
                    {
                        return Annotate(Helpers.GetVariableAccessExpression(CurrentScope.GetRandomVariable(exprType)), "Var");
                    }

                case ExprKind.BinaryOpExpression:
                    {
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
                        //TODO-config: Add MaxDepth in config
                        if (depth >= 3)
                        {
                            lhsExprKind = rhsExprKind = ExprKind.LiteralExpression;
                        }
                        else
                        {
                            lhsExprKind = GetASTUtils().GetRandomExpressionReturningPrimitive(lhsExprType.PrimitiveType);
                            rhsExprKind = GetASTUtils().GetRandomExpressionReturningPrimitive(rhsExprType.PrimitiveType);
                        }

                        // Fold arithmetic binop expressions that has constants.
                        // csc.exe would automatically fold that for us, but by doing it here, we eliminate generate 
                        // errors during compiling the test case.
                        if (op.HasFlag(OpFlags.Math) && lhsExprKind == ExprKind.LiteralExpression && rhsExprKind == ExprKind.LiteralExpression)
                        {
                            return Annotate(Helpers.GetWrappedAndCastedExpression(exprType, exprType, Helpers.GetLiteralExpression(exprType)), "BinOp-folded");
                        }

                        //TODO-config: Add MaxDepth in config
                        ExpressionSyntax lhs = ExprHelper(lhsExprKind, lhsExprType, depth >= 5 ? 0 : depth + 1);
                        ExpressionSyntax rhs = ExprHelper(rhsExprKind, rhsExprType, depth >= 5 ? 0 : depth + 1);

                        // For division, make sure that divisor is not 0
                        if ((op.Oper == SyntaxKind.DivideExpression) || (op.Oper == SyntaxKind.ModuloExpression))
                        {
                            rhs = ParenthesizedExpression(BinaryExpression(SyntaxKind.AddExpression, ParenthesizedExpression(rhs), LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(PRNG.Next(10, 100)))));
                            rhs = Helpers.GetWrappedAndCastedExpression(rhsExprType, exprType, rhs);
                        }

                        return Annotate(Helpers.GetWrappedAndCastedExpression(lhsExprType, exprType, Helpers.GetBinaryExpression(lhs, op, rhs)), "BinOp");
                    }
                case ExprKind.AssignExpression:
                    {
                        Tree.Operator assignOper = GetASTUtils().GetRandomAssignmentOperator(returnPrimitiveType: exprType.PrimitiveType);
                        Tree.ValueType lhsExprType, rhsExprType;
                        lhsExprType = rhsExprType = exprType;

                        if (assignOper.HasFlag(OpFlags.Shift))
                        {
                            rhsExprType = Tree.ValueType.ForPrimitive(Primitive.Int);
                        }

                        ExpressionSyntax lhs = ExprHelper(ExprKind.VariableExpression, lhsExprType, depth);
                        ExpressionSyntax rhs = null;

                        //TODO-config no. of attempts
                        int noOfAttempts = 0;
                        while (noOfAttempts++ < 5)
                        {
                            rhs = ExprHelper(GetASTUtils().GetRandomExpressionReturningPrimitive(rhsExprType.PrimitiveType), rhsExprType, depth);
                            // Make sure that we do not end up with same lhs=lhs.
                            if (lhs.ToFullString() != rhs.ToFullString())
                            {
                                break;
                            }
                        }
                        Debug.Assert(lhs.ToFullString() != rhs.ToFullString());

                        // For division, make sure that divisor is not 0
                        if ((assignOper.Oper == SyntaxKind.DivideAssignmentExpression) || (assignOper.Oper == SyntaxKind.ModuloAssignmentExpression))
                        {
                            rhs = ParenthesizedExpression(BinaryExpression(SyntaxKind.AddExpression, ParenthesizedExpression(rhs), LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(PRNG.Next(10, 100)))));
                            rhs = Helpers.GetWrappedAndCastedExpression(rhsExprType, lhsExprType, rhs);
                        }

                        return Annotate(Helpers.GetWrappedAndCastedExpression(lhsExprType, exprType,
                            AssignmentExpression(assignOper.Oper, lhs, rhs)), "Assign");
                    }
                case ExprKind.MethodCallExpression:
                    {
                        return MethodCallHelper(_testClass.GetRandomMethod(exprType), depth);
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
            ExpressionSyntax lhs = Annotate(Helpers.GetVariableAccessExpression(variableName), "specific-Var");
            ExpressionSyntax rhs = null;

            //TODO-config no. of attempts
            int noOfAttempts = 0;
            while (noOfAttempts++ < 5)
            {
                rhs = ExprHelper(GetASTUtils().GetRandomExpressionReturningPrimitive(exprType.PrimitiveType), exprType, 0);
                // Make sure that we do not end up with same lhs=lhs.
                if (lhs.ToFullString() != rhs.ToFullString())
                {
                    break;
                }
            }
            Debug.Assert(lhs.ToFullString() != rhs.ToFullString());

            return Annotate(ExpressionStatement(AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, lhs, rhs)), "specific-Assign", 0);
        }

        private ExpressionSyntax MethodCallHelper(MethodSignature methodSig, int depth)
        {
            //MethodSignature methodSig = _testClass.GetRandomMethod(exprType);
            List<SyntaxNodeOrToken> argumentNodes = new List<SyntaxNodeOrToken>();
            int paramsCount = methodSig.Parameters.Count;

            for (int paramId = 0; paramId < paramsCount; paramId++)
            {
                MethodParam parameter = methodSig.Parameters[paramId];

                Tree.ValueType argType = parameter.ParamType;
                ExprKind argExprKind = parameter.PassingWay == ParamValuePassing.None ? GetASTUtils().GetRandomExpressionReturningPrimitive(argType.PrimitiveType) : ExprKind.VariableExpression;

                ExpressionSyntax argExpr = ExprHelper(argExprKind, argType, depth);
                ArgumentSyntax argSyntax = Argument(argExpr);

                if (parameter.PassingWay == ParamValuePassing.Ref)
                {
                    argSyntax = argSyntax.WithRefKindKeyword(Token(SyntaxKind.RefKeyword));
                }
                else if (parameter.PassingWay == ParamValuePassing.Out)
                {
                    argSyntax = argSyntax.WithRefKindKeyword(Token(SyntaxKind.OutKeyword));
                }

                argumentNodes.Add(argSyntax);
                if (paramId + 1 < paramsCount)
                {
                    argumentNodes.Add(Token(SyntaxKind.CommaToken));
                }
            }

            return InvocationExpression(IdentifierName(methodSig.MethodName))
                .WithArgumentList(ArgumentList(SeparatedList<ArgumentSyntax>(argumentNodes)));
        }

        private Tree.ValueType GetRandomExprType(double structProbability)
        {
            //TODO:config - probability of struct variables
            if (PRNG.Decide(structProbability) && CurrentScope.NumOfStructTypes > 0)
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
            if (!expressionsCount.ContainsKey(typeName))
            {
                expressionsCount[typeName] = 0;
            }
            expressionsCount[typeName]++;
            return expression.WithTrailingTrivia(TriviaList(Comment($"/* E#{expressionsCount[typeName]}: {comment} */")));
#else
            return expression;
#endif
        }

        private StatementSyntax Annotate(StatementSyntax statement, string comment, int depth)
        {
#if DEBUG
            string typeName = statement.GetType().Name;
            if (!statementsCount.ContainsKey(typeName))
            {
                statementsCount[typeName] = 0;
            }
            statementsCount[typeName]++;
            return statement.WithTrailingTrivia(TriviaList(Comment($"/* {depth}: S#{statementsCount[typeName]}: {comment} */")));
#else
            return statement;
#endif
        }
    }

    public class MethodSignature
    {
        public string MethodName;
        public Tree.ValueType ReturnType;
        public List<MethodParam> Parameters;
        public bool IsLeaf;

        //TODO:config
        public static List<Weights<ParamValuePassing>> ValuePassing = new()
        {
            new Weights<ParamValuePassing>(ParamValuePassing.None, 50),
            new Weights<ParamValuePassing>(ParamValuePassing.Ref, 25),
            new Weights<ParamValuePassing>(ParamValuePassing.Out, 15),
            //new Weights<ParamValuePassing>(ParamValuePassing.In, 10),
        };

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
            : base(enclosingClass, methodName, 0)
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
