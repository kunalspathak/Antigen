using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;


namespace Antigen.Statements
{
    public class DoWhileStatement : LoopStatement
    {
        public DoWhileStatement(TestCase tc) : base(tc)
        {
        }

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
            result.Add(DoStatement(Block(loopBody), condition));
            return result;
        }
    }
}
