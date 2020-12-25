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

        public ForStatement(TestCase tc)
            : base(tc)
        {
        }

        //public StatementSyntax Generate()
        //{
        //    RenderPreLoopBody(context, labels);

        //    RenderLoopBody(context);

        //    RenderPostLoopBody(context, labels);
        //}

        public override List<StatementSyntax> Generate(bool labels)
        {
            //TODO-feature render comments
            //base.RenderInternal(context, labels);

            List<StatementSyntax> result = new List<StatementSyntax>();

            // Induction variables to be initialized outside the loop
            VariableDeclarationSyntax initCode = GenerateIVInitCode(false);
            if (initCode != null)
            {
                result.Add(LocalDeclarationStatement(initCode));
            }
            //context.BeginLine(GenerateIVInitCode(false));
            //context.WriteLine(";");

            //if (labels)
            //{
            //    foreach (string sLbl in Labels)
            //    {
            //        context.BeginLine("{0}: ", sLbl);
            //        context.WriteLine("");
            //    }
            //}

            // guard condition
            ExpressionSyntax condition = GenerateIVLoopGuardCode();
            if (LoopKind == Kind.NormalLoop || LoopKind == Kind.ComplexLoop)
            {
                ExpressionSyntax boundCond = BinaryExpression(SyntaxKind.LessThanExpression, IdentifierName(LoopVar), Bounds);
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
                incrementors.Add(PostfixUnaryExpression(SyntaxKind.PostIncrementExpression, IdentifierName(LoopVar)));
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
            return result;

            //return ForStatement(
            //    declaration: GenerateIVInitCode(true), // induction variables to be initialized inside the loop
            //    initializers: null,
            //    condition: condition,
            //    incrementors: incrementors,
            //    statement: Block(loopBody)
            //    );

            //// Init
            //context.BeginLine("for(");

            //// induction variables to be initialized inside the loop
            //context.Write(GenerateIVInitCode(true));
            //context.Write(";");

            //// Cond
            //loopCode = GenerateIVLoopGuardCode();
            //context.Write("{0}", loopCode);

            //if (LoopKind == Kind.NormalLoop || LoopKind == Kind.ComplexLoop)
            //{
            //    if (!String.IsNullOrEmpty(loopCode))
            //        context.Write(" &&");
            //    context.Write(" {0} < (", LoopVar);
            //    Bounds.Render(context);
            //    context.Write(")");

            //}
            //context.Write(";");

            //// induction variables to be incr/decr
            //loopCode = GenerateIVStepCode();
            //context.Write("{0}", loopCode);

            //if (LoopKind == Kind.NormalLoop)
            //{
            //    if (!String.IsNullOrEmpty(loopCode))
            //        context.Write(" ,");
            //    context.Write(" {0}++", LoopVar);
            //}
            //else if (LoopKind == Kind.ComplexLoop)
            //{
            //    if (!String.IsNullOrEmpty(loopCode))
            //        context.Write(",");
            //    context.Write(" ");
            //    LoopStep.Render(context);
            //}
            //context.Write(") {{");
            //context.WriteLine("");
          
            //context.Depth++;

            //// Add step/break condition at the beginning 
            //GenerateIVBreakAndStepCode(isCodeForBreakCondAtTheEnd: false).ForEach(bc => { context.BeginLine(bc); context.WriteLine(""); });

        }


        //public override void RenderPostLoopBody(RenderContext context, bool labels)
        //{
        //    // Add step/break condition at the end 
        //    GenerateIVBreakAndStepCode(isCodeForBreakCondAtTheEnd: true).ForEach(bc => { context.BeginLine(bc); context.WriteLine(""); });

        //    context.Depth--;
        //    context.BeginLine("}}");
        //    context.WriteLine("");

        //    RenderFunctionWrapperEnd(context);

        //    Debug.Assert(HasSuccessfullyGenerated(), "ForStatement didn't generate properly. Please check the loop variables.");
        //}
    };
}
