// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Antigen.Expressions;

namespace Antigen.Statements
{
    public class WhileStatement : LoopStatement
    {
        public WhileStatement(TestCase tc, int nestNum, int numOfSecondaryVars, Expression bounds, List<Statement> loopBody) :
                base(tc, nestNum, numOfSecondaryVars, bounds, loopBody)
        {
            PopulateContent();
        }

        protected override void PopulatePreLoopBody()
        {
            // Induction variables to be initialized outside the loop
            loopBodyBuilder.AppendLine(GenerateIVInitCode());

            loopBodyBuilder.Append("while(");
            loopBodyBuilder.Append($"({Bounds})");

            string loopGuardCondition = GenerateIVLoopGuardCode();
            if (!string.IsNullOrEmpty(loopGuardCondition))
                loopBodyBuilder.Append($" && ({loopGuardCondition})");
            loopBodyBuilder.Append(')');

            loopBodyBuilder.AppendLine("{");

            // Add step/break condition at the beginning 
            loopBodyBuilder.AppendLine(string.Join(Environment.NewLine, GenerateIVBreakAndStepCode(isCodeForBreakCondAtTheEnd: false)));
        }

        protected override void PopulatePostLoopBody()
        {
            // Add step/break condition at the beginning 
            loopBodyBuilder.AppendLine(string.Join(Environment.NewLine, GenerateIVBreakAndStepCode(isCodeForBreakCondAtTheEnd: true)));

            loopBodyBuilder.AppendLine("}");

            Debug.Assert(HasSuccessfullyGenerated(), "WhileStatement didn't generate properly. Please check the loop variables.");
        }
    }

}
