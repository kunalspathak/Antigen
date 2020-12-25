using Antigen.Tree;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using ValueType = Antigen.Tree.ValueType;

namespace Antigen
{
    public static partial class Helpers
    {
        public static BinaryExpressionSyntax GetBinaryExpression(ExpressionSyntax lhs, Operator op, ExpressionSyntax rhs)
        {
            return BinaryExpression(op.Oper, lhs, rhs);
        }

        public static ParenthesizedExpressionSyntax GetWrappedAndCastedExpression(ValueType returnType, ExpressionSyntax expr)
        {
            return ParenthesizedExpression(CastExpression(Helpers.GetToken(returnType.TypeKind), ParenthesizedExpression(expr)));
        }
    }
}
