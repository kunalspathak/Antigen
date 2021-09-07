// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;


namespace Antigen.Statements
{
    public class ForStatement : LoopStatement
    {
        public List<string> symTableLog = new List<string>();

        public ExpressionSyntax LoopStep;

        public Kind LoopKind;
        public enum Kind
        {
            SimpleLoop,
            NormalLoop,
            ComplexLoop
        }

        public ForStatement(TestCase tc) : base(tc) {}

        public override List<StatementSyntax> Generate(bool labels)
        {
            List<StatementSyntax> result = new List<StatementSyntax>();

            // Induction variables to be initialized outside the loop
            VariableDeclarationSyntax initCode = GenerateIVInitCode(false);
            if (initCode != null)
            {
                result.Add(LocalDeclarationStatement(initCode));
            }

            // guard condition
            ExpressionSyntax condition = GenerateIVLoopGuardCode();
            if (LoopKind == Kind.NormalLoop || LoopKind == Kind.ComplexLoop)
            {
                ExpressionSyntax boundCond = BinaryExpression(SyntaxKind.LessThanExpression, TestCase.GetExpressionSyntax(LoopVar), Bounds);
                if (condition == null)
                {
                    condition = boundCond;
                }
                else
                {
                    condition = BinaryExpression(SyntaxKind.LogicalAndExpression, condition, boundCond);
                }
            }

            // induction variables to be incr/decr
            SeparatedSyntaxList<ExpressionSyntax> incrementors = GenerateIVStepCode();
            if (LoopKind == Kind.NormalLoop)
            {
                incrementors.Add(PostfixUnaryExpression(SyntaxKind.PostIncrementExpression, TestCase.GetExpressionSyntax(LoopVar)));
            }
            else if (LoopKind == Kind.ComplexLoop)
            {
                incrementors.Add(LoopStep);
            }

            // Add step/break condition at the beginning 
            List<StatementSyntax> loopBody = GenerateIVBreakAndStepCode(isCodeForBreakCondAtTheEnd: false);

            // Add actual loop body
            loopBody.AddRange(GetLoopBody());

            // Add step/break condition at the end 
            loopBody.AddRange(GenerateIVBreakAndStepCode(isCodeForBreakCondAtTheEnd: true));

            ForStatementSyntax forStmt = ForStatement(Block(loopBody));

            VariableDeclarationSyntax declaration = GenerateIVInitCode(true);
            if (declaration != null)
            {
                forStmt = forStmt.WithDeclaration(declaration);
            }
            forStmt = forStmt.WithCondition(condition).WithIncrementors(incrementors);
            result.Add(forStmt);
            Debug.Assert(HasSuccessfullyGenerated(), "ForStatement didn't generate properly. Please check the loop variables.");

            return result;
        }
    };
}
