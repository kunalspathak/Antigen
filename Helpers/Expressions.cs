﻿using Antigen.Tree;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        public static ParenthesizedExpressionSyntax GetWrappedAndCastedExpression(ValueType fromType, ValueType toType, ExpressionSyntax expr)
        {
            //Debug.Assert(fromType.CanConvert(toType) || toType.PrimitiveType == Primitive.Boolean);
            ParenthesizedExpressionSyntax parenExpr = ParenthesizedExpression(expr);

            //if (fromType.CanConvertExplicit(toType))
            //{
                parenExpr = ParenthesizedExpression(CastExpression(Helpers.GetToken(toType.TypeKind), parenExpr));
            //}
            //else
            //{
            //    Debug.Assert(fromType.CanConvertImplicit(toType) || toType.PrimitiveType == Primitive.Boolean);
            //}

            return parenExpr;
        }
    }
}
