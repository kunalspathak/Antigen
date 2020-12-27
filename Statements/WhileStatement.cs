// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Antigen.Statements
{
    public class WhileStatement : LoopStatement
    {
        public WhileStatement(TestCase tc) : base(tc) {}

        public override List<StatementSyntax> Generate(bool labels)
        {
            List<StatementSyntax> result = new List<StatementSyntax>();

            VariableDeclarationSyntax initCode = GenerateIVInitCode(false);
            if (initCode != null)
            {
                result.Add(LocalDeclarationStatement(initCode));
            }

            // Add step/break condition at the beginning 
            List<StatementSyntax> loopBody = GenerateIVBreakAndStepCode(isCodeForBreakCondAtTheEnd: false);

            // Add actual loop body
            loopBody.AddRange(GetLoopBody());

            // Add step/break condition at the end 
            loopBody.AddRange(GenerateIVBreakAndStepCode(isCodeForBreakCondAtTheEnd: true));

            // guard condition
            ExpressionSyntax condition = GenerateIVLoopGuardCode();
            if (condition == null)
            {
                condition = Bounds;
            }
            else
            {
                condition = BinaryExpression(SyntaxKind.LogicalAndExpression, condition, Bounds);
            }
            result.Add(WhileStatement(condition, Block(loopBody)));
            Debug.Assert(HasSuccessfullyGenerated(), "WhileStatement didn't generate properly. Please check the loop variables.");

            return result;
        }
    }
}
