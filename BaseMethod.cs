using Antigen.Statements;
using Antigen.Tree;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Antigen
{
    public class BaseMethod
    {
        private TestCase testCase;
        private string Name;
#if DEBUG
        private bool annotateComments = true;
#else
        private bool annotateComments = false;
#endif
        private List<string> variableNames = new List<string>();

        private Scope m_ParentScope;
        private Stack<Scope> ScopeStack = new Stack<Scope>();

        public MethodDeclarationSyntax GeneratedMethod { get; private set; }

        public AstUtils GetASTUtils()
        {
            return testCase.AstUtils;
        }

        public void AddParentScope(Scope parent)
        {
            m_ParentScope = parent;
        }

        public Scope GetParentScope()
        {
            if (m_ParentScope != null)
                return m_ParentScope;
            else
                return null;
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

        public BaseMethod(TestCase tc, string name)
        {
            testCase = tc;
            Name = name;
        }

        public void Generate()
        {
            MethodDeclarationSyntax methodDeclaration = MethodDeclaration(PredefinedType(Token(SyntaxKind.VoidKeyword)), Name).WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)));
            IList<StatementSyntax> methodBody = new List<StatementSyntax>();

            // TODO-TEMP initialize one variable of each type
            foreach (Tree.ValueType variableType in Tree.ValueType.GetTypes())
            {
                string variableName = Helpers.GetVariableName(variableType, variableNames.Count);
                variableNames.Add(variableName);

                ExpressionSyntax rhs = ExprHelper(ExprKind.LiteralExpression, variableType, 0);
                CurrentScope.AddLocal(variableType, variableName);

                methodBody.Add(LocalDeclarationStatement(Helpers.GetVariableDeclaration(variableType, variableName, rhs)));
            }

            //TODO: Define some more constants
            methodBody.Add(
                LocalDeclarationStatement(
                    Helpers.GetVariableDeclaration(
                        Tree.ValueType.ForPrimitive(Primitive.Int32),
                        Constants.LoopInvariantName,
                        LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(PRNG.Next(10))))));

            for (int i = 0; i < 10; i++)
            {
                StmtKind cur = GetASTUtils().GetRandomStatemet();
                methodBody.Add(StatementHelper(cur, 0));
            }

            // print all variables
            foreach (string variableName in CurrentScope.AllVariables)
            {
                methodBody.Add(ParseStatement($"Console.WriteLine(\"{variableName}= \" + {variableName});"));
            }

            // Wrap everything in unchecked so we do not see overflow compilation errors
            GeneratedMethod = methodDeclaration.WithBody(Block(CheckedStatement(SyntaxKind.UncheckedStatement, Block(methodBody))));
        }

        public StatementSyntax StatementHelper(StmtKind stmtKind, int depth)
        {
            switch (stmtKind)
            {
                case StmtKind.VariableDeclaration:
                    {
                        Tree.ValueType variableType = GetASTUtils().GetRandomExprType();
                        string variableName = Helpers.GetVariableName(variableType, variableNames.Count);
                        variableNames.Add(variableName);

                        ExpressionSyntax rhs = ExprHelper(GetASTUtils().GetRandomExpressionReturningPrimitive(variableType.PrimitiveType), variableType, 0);
                        CurrentScope.AddLocal(variableType, variableName);

                        return Annotate(LocalDeclarationStatement(Helpers.GetVariableDeclaration(variableType, variableName, rhs)), "S:VarDecl");
                    }
                case StmtKind.IfElseStatement:
                    {
                        Tree.ValueType condValueType = Tree.ValueType.ForPrimitive(Primitive.Boolean);
                        ExpressionSyntax conditionExpr = ExprHelper(GetASTUtils().GetRandomExpressionReturningPrimitive(Primitive.Boolean), condValueType, 0);

                        Scope ifBranchScope = new Scope(testCase, ScopeKind.ConditionalScope, CurrentScope);
                        Scope elseBranchScope = new Scope(testCase, ScopeKind.ConditionalScope, CurrentScope);

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

                        return Annotate(IfStatement(conditionExpr, Block(ifBody), ElseClause(Block(elseBody))), "S:IfElse");
                    }
                case StmtKind.AssignStatement:
                    {
                        Tree.Operator assignOper = GetASTUtils().GetRandomAssignmentOperator();
                        Tree.ValueType variableType = GetASTUtils().GetRandomExprType(assignOper.InputTypes);
                        ExpressionSyntax lhs = ExprHelper(ExprKind.VariableExpression, variableType, depth);
                        ExpressionSyntax rhs = ExprHelper(GetASTUtils().GetRandomExpressionReturningPrimitive(variableType.PrimitiveType), variableType, depth);
                        return Annotate(ExpressionStatement(AssignmentExpression(assignOper.Oper, lhs, rhs)), "S:Assign");
                    }
                case StmtKind.ForStatement:
                    {
                        Scope forLoopScope = new Scope(testCase, ScopeKind.LoopScope, CurrentScope);
                        ForStatement forStmt = new ForStatement(testCase);
                        //TODO:config
                        int n = 3; // max statements
                        forStmt.LoopVar = CurrentScope.GetRandomVariable(Tree.ValueType.ForPrimitive(Primitive.Int32));
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

                        forStmt.Bounds = ExprHelper(GetASTUtils().GetRandomExpressionReturningPrimitive(Primitive.Int32), Tree.ValueType.ForPrimitive(Primitive.Int32), 0);
                        forStmt.LoopStep = ExprHelper(GetASTUtils().GetRandomExpressionReturningPrimitive(Primitive.Int32), Tree.ValueType.ForPrimitive(Primitive.Int32), 0);

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

                        return Annotate(Block(forStmt.Generate(false)), "S:for-loop");

                    }
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
                    return Annotate(Helpers.GetLiteralExpression(exprType), "E:Literal");

                case ExprKind.VariableExpression:
                    return Annotate(IdentifierName(CurrentScope.GetRandomVariable(exprType)), "E:Var");

                case ExprKind.BinaryOpExpression:
                    Operator op = GetASTUtils().GetRandomBinaryOperator(returnPrimitiveType: exprType.PrimitiveType);

                    Tree.ValueType lhsExprType = GetASTUtils().GetRandomExprType(op.InputTypes);
                    Tree.ValueType rhsExprType = lhsExprType;
                    if (op.HasFlag(OpFlags.Shift))
                    {
                        rhsExprType = Tree.ValueType.ForPrimitive(Primitive.Int32);
                    }
                    ExpressionSyntax lhs, rhs;

                    //TODO-config: Add MaxDepth in config
                    if (depth >= 5)
                    {
                        lhs = ExprHelper(ExprKind.LiteralExpression, lhsExprType, 0);
                        rhs = ExprHelper(ExprKind.LiteralExpression, rhsExprType, 0);
                    }
                    else
                    {
                        lhs = ExprHelper(GetASTUtils().GetRandomExpressionReturningPrimitive(lhsExprType.PrimitiveType), lhsExprType, depth + 1);
                        rhs = ExprHelper(GetASTUtils().GetRandomExpressionReturningPrimitive(rhsExprType.PrimitiveType), rhsExprType, depth + 1);
                    }
                    return Annotate(Helpers.GetWrappedAndCastedExpression(exprType, Helpers.GetBinaryExpression(lhs, op, rhs)), "E:BinOp");

                default:
                    Debug.Assert(false, String.Format("Hit unknown expression type {0}", Enum.GetName(typeof(ExprKind), exprKind)));
                    break;
            }
            return null;
        }

        private ExpressionSyntax Annotate(ExpressionSyntax expression, string comment)
        {
            if (!annotateComments)
            {
                return expression;
            }
            return expression.WithTrailingTrivia(TriviaList(Comment("/* " + comment + " */")));
        }

        private StatementSyntax Annotate(StatementSyntax statement, string comment)
        {
            if (!annotateComments)
            {
                return statement;
            }
            return statement.WithTrailingTrivia(TriviaList(Comment("/* " + comment + " */")));
        }
    }
}
