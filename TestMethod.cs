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
        private TestClass testClass;
        private TestCase TC => testClass.TC;
        private string Name;
#if DEBUG
        private Dictionary<string, int> expressionsCount = new Dictionary<string, int>();
        private Dictionary<string, int> statementsCount = new Dictionary<string, int>();
#endif

        private int variablesCount = 0;
        internal HashSet<string> callsFromThisMethod = new HashSet<string>();

        public AstUtils GetASTUtils()
        {
            return TC.AstUtils;
        }

        private Scope MethodScope;
        private bool takesParameters;
        public MethodSignature MethodSignature { get; private set; }
        public Scope CurrentScope => testClass.CurrentScope;

        public void PushScope(Scope scope)
        {
            testClass.PushScope(scope);
        }

        public Scope PopScope()
        {
            Scope ret = testClass.PopScope();
            //Debug.Assert(ret.Parent == ScopeStack.Peek());
            return ret;
        }

        public TestMethod(TestClass enclosingClass, string methodName, bool takesParams = true)
        {
            testClass = enclosingClass;
            Name = methodName;
            MethodScope = new Scope(enclosingClass.TC, ScopeKind.FunctionScope, enclosingClass.ClassScope);
            takesParameters = takesParams;
        }

        public MethodDeclarationSyntax Generate()
        {
            PushScope(MethodScope);

            MethodDeclarationSyntax methodDeclaration = GenerateMethodSignature();
            IList<StatementSyntax> methodBody = new List<StatementSyntax>();

            // TODO-TEMP initialize one variable of each type
            foreach (Tree.ValueType variableType in Tree.ValueType.GetTypes())
            {
                string variableName = Helpers.GetVariableName(variableType, variablesCount++);

                ExpressionSyntax rhs = ExprHelper(ExprKind.LiteralExpression, variableType, 0);
                CurrentScope.AddLocal(variableType, variableName);

                methodBody.Add(LocalDeclarationStatement(Helpers.GetVariableDeclaration(variableType, variableName, rhs)));
            }

            // TODO-TEMP initialize one variable of each struct type
            foreach (Tree.ValueType structType in CurrentScope.AllStructTypes)
            {
                string variableName = Helpers.GetVariableName(structType, variablesCount++);

                ExpressionSyntax rhs = Annotate(Helpers.GetObjectCreationExpression(structType.TypeName), "struct-init");
                CurrentScope.AddLocal(structType, variableName);

                // Add all the fields to the scope
                var listOfStructFields = CurrentScope.GetStructFields(structType);
                foreach(var structField in listOfStructFields)
                {
                    CurrentScope.AddLocal(structField.FieldType, $"{variableName}.{structField.FieldName}");
                }

                methodBody.Add(LocalDeclarationStatement(Helpers.GetVariableDeclaration(structType, variableName, rhs)));
            }

            //TODO: Define some more constants
            methodBody.Add(
                LocalDeclarationStatement(
                    Helpers.GetVariableDeclaration(
                        Tree.ValueType.ForPrimitive(Primitive.Int),
                        Constants.LoopInvariantName,
                        LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(PRNG.Next(10))))));

            // TODO-TEMP initialize out and ref method parameters
            var paramsToInitialize = MethodSignature.Parameters.Where(p => p.PassingWay == ParamValuePassing.Out);
            foreach (MethodParam param in paramsToInitialize)
            {
                methodBody.Add(VariableAssignmentHelper(param.ParamType, param.ParamName));
                CurrentScope.AddLocal(param.ParamType, param.ParamName);
            }

            //TODO-config: Statements in a function
            for (int i = 0; i < 5; i++)
            {
                StmtKind cur = GetASTUtils().GetRandomStatemet();
                methodBody.Add(StatementHelper(cur, 0));
            }

            // print all variables
            foreach (string variableName in CurrentScope.AllVariables)
            {
                methodBody.Add(ParseStatement($"Console.WriteLine(\"{variableName}= \" + {variableName});"));
            }

            // return statement
            methodBody.Add(StatementHelper(StmtKind.ReturnStatement, 0));

            PopScope();

            testClass.RegisterMethod(MethodSignature);

            // Wrap everything in unchecked so we do not see overflow compilation errors
            return methodDeclaration.WithBody(Block(CheckedStatement(SyntaxKind.UncheckedStatement, Block(methodBody))));
        }

        /// <summary>
        ///     Generates method signature of this method.
        /// </summary>
        private MethodDeclarationSyntax GenerateMethodSignature()
        {
            MethodSignature = new MethodSignature(Name);
            int numOfParameters = 0;
            if (takesParameters)
            {
                //TODO:config - No. of parameters
                numOfParameters = PRNG.Next(1, 10);
                MethodSignature.ReturnType = GetRandomExprType();
            }

            List<MethodParam> parameters = new List<MethodParam>();
            MethodSignature.Parameters = parameters;
            List<ParameterSyntax> parameterNodes = new List<ParameterSyntax>();

            for (int paramIndex = 0; paramIndex < numOfParameters; paramIndex++)
            {
                var paramType = GetRandomExprType();
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
            }


            List<SyntaxNodeOrToken> finalPametersList = new List<SyntaxNodeOrToken>();

            if (takesParameters)
            {
                finalPametersList.Add(parameterNodes[0]);
            }

            for (int paramIndex = 1; paramIndex < numOfParameters; paramIndex++)
            {
                finalPametersList.Add(Token(SyntaxKind.CommaToken));
                finalPametersList.Add(parameterNodes[paramIndex]);
            }

            return MethodDeclaration(Helpers.GetTypeSyntax(MethodSignature.ReturnType), Name)
                    .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
                    .WithParameterList(ParameterList(SeparatedList<ParameterSyntax>(finalPametersList)));
        }

        public StatementSyntax StatementHelper(StmtKind stmtKind, int depth)
        {
            switch (stmtKind)
            {
                case StmtKind.VariableDeclaration:
                    {
                        Tree.ValueType variableType = GetRandomExprType();

                        string variableName = Helpers.GetVariableName(variableType, variablesCount++);

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

                        return Annotate(LocalDeclarationStatement(Helpers.GetVariableDeclaration(variableType, variableName, rhs)), "VarDecl");
                    }
                case StmtKind.IfElseStatement:
                    {
                        Tree.ValueType condValueType = Tree.ValueType.ForPrimitive(Primitive.Boolean);
                        ExpressionSyntax conditionExpr = ExprHelper(GetASTUtils().GetRandomExpressionReturningPrimitive(Primitive.Boolean), condValueType, 0);

                        Scope ifBranchScope = new Scope(TC, ScopeKind.ConditionalScope, CurrentScope);
                        Scope elseBranchScope = new Scope(TC, ScopeKind.ConditionalScope, CurrentScope);

                        //TODO-config: Add MaxDepth in config
                        int ifcount = 3;
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

                        int elsecount = 3;
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

                        return Annotate(IfStatement(conditionExpr, Block(ifBody), ElseClause(Block(elseBody))), "IfElse");
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
                            rhs = ParenthesizedExpression(BinaryExpression(SyntaxKind.AddExpression, ParenthesizedExpression(rhs), LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(PRNG.Next(100)))));
                            rhs = Helpers.GetWrappedAndCastedExpression(rhsExprType, lhsExprType, rhs);
                        }

                        return Annotate(ExpressionStatement(AssignmentExpression(assignOper.Oper, lhs, rhs)), "Assign");
                    }
                case StmtKind.ForStatement:
                    {
                        Scope forLoopScope = new Scope(TC, ScopeKind.LoopScope, CurrentScope);
                        ForStatement forStmt = new ForStatement(TC);
                        //TODO:config
                        int n = 3; // max statements
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

                        return Annotate(Block(forStmt.Generate(false)), "for-loop");
                    }
                case StmtKind.DoWhileStatement:
                    {
                        Scope doWhileScope = new Scope(TC, ScopeKind.LoopScope, CurrentScope);
                        DoWhileStatement doStmt = new DoWhileStatement(TC);
                        //TODO:config
                        int n = 3; // max statements
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
                        return Annotate(Block(doStmt.Generate(false)), "do-while");
                    }
                case StmtKind.WhileStatement:
                    {
                        Scope whileScope = new Scope(TC, ScopeKind.LoopScope, CurrentScope);
                        WhileStatement whileStmt = new WhileStatement(TC);
                        //TODO:config
                        int n = 3; // max statements
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
                        return Annotate(Block(whileStmt.Generate(false)), "while-loop");
                    }
                case StmtKind.ReturnStatement:
                    Tree.ValueType returnType = MethodSignature.ReturnType;
                    if (returnType.PrimitiveType == Primitive.Void)
                    {
                        return Annotate(ReturnStatement(), "Return");
                    }
                    ExpressionSyntax returnExpr = ExprHelper(GetASTUtils().GetRandomExpressionReturningPrimitive(returnType.PrimitiveType), returnType, depth);
                    return Annotate(ReturnStatement(returnExpr), "Return");
                default:
                    Debug.Assert(false, String.Format("Hit unknown statement type {0}", Enum.GetName(typeof(StmtKind), stmtKind)));
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
                        if (depth >= 5)
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
                            rhs = ParenthesizedExpression(BinaryExpression(SyntaxKind.AddExpression, ParenthesizedExpression(rhs), LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(PRNG.Next(100)))));
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
                            rhs = ParenthesizedExpression(BinaryExpression(SyntaxKind.AddExpression, ParenthesizedExpression(rhs), LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(PRNG.Next(100)))));
                            rhs = Helpers.GetWrappedAndCastedExpression(rhsExprType, lhsExprType, rhs);
                        }

                        return Annotate(Helpers.GetWrappedAndCastedExpression(lhsExprType, exprType,
                            AssignmentExpression(assignOper.Oper, lhs, rhs)), "Assign");
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

            return Annotate(ExpressionStatement(AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, lhs, rhs)), "specific-Assign");
        }

        private Tree.ValueType GetRandomExprType()
        {
            //TODO:config - probability of struct variables
            if (PRNG.Decide(0.3) && CurrentScope.NumOfStructTypes > 0)
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

        private StatementSyntax Annotate(StatementSyntax statement, string comment)
        {
#if DEBUG
            string typeName = statement.GetType().Name;
            if (!statementsCount.ContainsKey(typeName))
            {
                statementsCount[typeName] = 0;
            }
            statementsCount[typeName]++;
            return statement.WithTrailingTrivia(TriviaList(Comment($"/* S#{statementsCount[typeName]}: {comment} */")));
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

        //TODO:config
        public static List<Weights<ParamValuePassing>> ValuePassing = new()
        {
            new Weights<ParamValuePassing>(ParamValuePassing.None, 50),
            new Weights<ParamValuePassing>(ParamValuePassing.Ref, 25),
            new Weights<ParamValuePassing>(ParamValuePassing.Out, 15),
            //new Weights<ParamValuePassing>(ParamValuePassing.In, 10),
        };

        public MethodSignature(string methodName)
        {
            MethodName = methodName;
            ReturnType = Tree.ValueType.ForVoid();
            Parameters = new List<MethodParam>();
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

        //public MethodParam(Tree.ValueType paramType, ParamValuePassing passingWay)
        //{
        //    ParamType = paramType;
        //    PassingWay = passi
        //}
    }

    public enum ParamValuePassing
    {
        None,
        //TODO-future: need to add ability of marking variables readonly
        //In,
        Out,
        Ref
    };
}
