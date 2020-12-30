﻿using Antigen.Statements;
using Antigen.Tree;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
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
    public class BaseMethod
    {
        private TestClass testClass;
        private TestCase TC => testClass.TC;
        private string Name;
#if DEBUG
        private Dictionary<string, int> expressionsCount = new Dictionary<string, int>();
        private Dictionary<string, int> statementsCount = new Dictionary<string, int>();
#endif

        private int variablesCount = 0;

        private Scope m_ParentScope;
        //private Stack<Scope> ScopeStack = new Stack<Scope>();

        public AstUtils GetASTUtils()
        {
            return TC.AstUtils;
        }

        private Scope MethodScope;
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

        public BaseMethod(TestClass enclosingClass, string methodName)
        {
            testClass = enclosingClass;
            Name = methodName;
            MethodScope = new Scope(enclosingClass.TC, ScopeKind.FunctionScope, enclosingClass.ClassScope);
        }

        public MethodDeclarationSyntax Generate()
        {
            PushScope(MethodScope);

            MethodDeclarationSyntax methodDeclaration = MethodDeclaration(PredefinedType(Token(SyntaxKind.VoidKeyword)), Name).WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)));
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

            PopScope();

            // Wrap everything in unchecked so we do not see overflow compilation errors
            return methodDeclaration.WithBody(Block(CheckedStatement(SyntaxKind.UncheckedStatement, Block(methodBody))));
        }

        public StatementSyntax StatementHelper(StmtKind stmtKind, int depth)
        {
            switch (stmtKind)
            {
                case StmtKind.VariableDeclaration:
                    {
                        Tree.ValueType variableType;
                        //TODO:config - probability of struct variables
                        if (PRNG.Decide(0.3) && CurrentScope.NumOfStructTypes > 0)
                        {
                            variableType = CurrentScope.AllStructTypes[PRNG.Next(CurrentScope.NumOfStructTypes)];
                        }
                        else
                        {
                            variableType = GetASTUtils().GetRandomExprType();
                        }

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
                        Tree.ValueType variableType;
                        //TODO-cleanup: Somehow combine GetRandomExprType() and GetRandomStructType() functionality
                        // Currently the only problem is AllStructTypes is in scope object but GetRandomExprType() is
                        // in AstUtils.
                        if (((assignOper.InputTypes & Primitive.Struct) != 0) && PRNG.Decide(0.2) && CurrentScope.NumOfStructTypes > 0)
                        {
                            variableType = CurrentScope.AllStructTypes[PRNG.Next(CurrentScope.NumOfStructTypes)];
                        }
                        else
                        {
                            variableType = GetASTUtils().GetRandomExprType(assignOper.InputTypes);
                        }

                        ExpressionSyntax lhs = ExprHelper(ExprKind.VariableExpression, variableType, depth);
                        ExpressionSyntax rhs = ExprHelper(GetASTUtils().GetRandomExpressionReturningPrimitive(variableType.PrimitiveType), variableType, depth);
                        return Annotate(ExpressionStatement(AssignmentExpression(assignOper.Oper, lhs, rhs)), "Assign");
                    }
                case StmtKind.ForStatement:
                    {
                        Scope forLoopScope = new Scope(TC, ScopeKind.LoopScope, CurrentScope);
                        ForStatement forStmt = new ForStatement(TC);
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
                    return Annotate(Helpers.GetLiteralExpression(exprType), "Literal");

                case ExprKind.VariableExpression:
                    return Annotate(Helpers.GetVariableAccessExpression(CurrentScope.GetRandomVariable(exprType)), "Var");

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
                    return Annotate(Helpers.GetWrappedAndCastedExpression(exprType, Helpers.GetBinaryExpression(lhs, op, rhs)), "BinOp");

                default:
                    Debug.Assert(false, string.Format("Hit unknown expression type {0}", Enum.GetName(typeof(ExprKind), exprKind)));
                    break;
            }
            return null;
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
}
