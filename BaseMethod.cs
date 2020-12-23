using Antigen.Tree;
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
        private List<string> variableNames = new List<string>();

        private Scope m_ParentScope;
        private Stack<Scope> ScopeStack = new Stack<Scope>();

        public MethodDeclarationSyntax GeneratedMethod { get; private set;}

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
            foreach(ExprType variableType in ExprType.GetTypes())
            {
                string variableName = Helpers.GetVariableName(variableType, variableNames.Count);
                variableNames.Add(variableName);

                ExpressionSyntax rhs = ExprHelper(ExprKind.LiteralExpression, variableType);
                CurrentScope.AddLocal(variableType, variableName);

                methodBody.Add(LocalDeclarationStatement(Helpers.GetVariableDeclaration(variableType, variableName, rhs)));
            }

            for (int i = 0; i < 10; i++)
            {
                StmtKind cur = GetASTUtils().GetRandomStatemet();
                methodBody.Add(StatementHelper(cur));
            }

            // print all variables
            foreach(string variableName in variableNames)
            {
                methodBody.Add(ParseStatement($"Console.WriteLine(\"{variableName}= \" + {variableName});"));
            }

            GeneratedMethod = methodDeclaration.WithBody(Block(methodBody));
        }

        public StatementSyntax StatementHelper(StmtKind stmtKind)
        {
            switch (stmtKind)
            {
                case StmtKind.VariableDeclaration:
                    ExprType variableType = GetASTUtils().GetRandomType();
                    string variableName = Helpers.GetVariableName(variableType, variableNames.Count);
                    variableNames.Add(variableName);

                    ExpressionSyntax rhs = ExprHelper(GetASTUtils().GetRandomExpression(), variableType);
                    CurrentScope.AddLocal(variableType, variableName);

                    return LocalDeclarationStatement(Helpers.GetVariableDeclaration(variableType, variableName, rhs));

                default:
                    Debug.Assert(false, String.Format("Hit unknown statement type {0}", Enum.GetName(typeof(StmtKind), stmtKind)));
                    break;
            }
            return null;
        }

        public ExpressionSyntax ExprHelper(ExprKind exprKind, ExprType exprType)
        {
            switch (exprKind)
            {
                case ExprKind.LiteralExpression:
                    return Helpers.GetLiteralExpression(exprType);
                case ExprKind.VariableExpression:
                    return IdentifierName(CurrentScope.GetRandomVariable(exprType));
                default:
                    Debug.Assert(false, String.Format("Hit unknown expression type {0}", Enum.GetName(typeof(ExprKind), exprKind)));
                    break;
            }
            return null;
        }
    }
}
