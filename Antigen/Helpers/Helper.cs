// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Antigen.Expressions;
using Antigen.Tree;
using Microsoft.CodeAnalysis.CSharp;

namespace Antigen
{
    public class Helper
    {
        public static Expression FixDivideByZero(TestCase testCase, Tree.ValueType leftType, Operator op, Expression rhs)
        {
            if (
                (op.Oper == SyntaxKind.DivideAssignmentExpression) ||
                (op.Oper == SyntaxKind.DivideExpression) ||
                (op.Oper == SyntaxKind.ModuloAssignmentExpression) ||
                (op.Oper == SyntaxKind.ModuloExpression))
            {
                // To avoid divide by zero errors
                var bitwiseOrExpression = new AssignExpression(
                    testCase,
                    leftType,
                    new ParenthsizedExpression(testCase, rhs),
                    Operator.ForSyntaxKind(SyntaxKind.BitwiseOrExpression),
                    ConstantValue.GetRandomConstantInt(1, 100));

                return new CastExpression(testCase, bitwiseOrExpression, leftType);
            }
            else
            {
                return rhs;
            }
        }
    }
}
