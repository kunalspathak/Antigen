// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Antigen.Tree;
using Microsoft.CodeAnalysis.CSharp;

namespace Antigen.Expressions
{
    public class BinaryExpression : Expression
    {
        public readonly Expression Left;
        public readonly Operator Op;
        public readonly Expression Right;

        public BinaryExpression(TestCase testCase, Tree.ValueType leftType, Expression lhs, Operator op, Expression rhs) : base(testCase)
        {
            Left = lhs;
            Op = op;

            if (
                (Op.Oper == SyntaxKind.DivideAssignmentExpression) ||
                (Op.Oper == SyntaxKind.DivideExpression) ||
                (Op.Oper == SyntaxKind.ModuloAssignmentExpression) ||
                (Op.Oper == SyntaxKind.ModuloExpression))
            {
                // To avoid divide by zero errors
                var addExpression = new AssignExpression(
                    testCase,
                    leftType,
                    new ParenthsizedExpression(testCase, rhs),
                    Operator.ForSyntaxKind(SyntaxKind.AddExpression),
                    ConstantValue.GetRandomConstantInt(10, 100));

                Right = new CastExpression(testCase, addExpression, leftType);
            }
            else
            {
                Right = rhs;
            }
        }

        public override string ToString()
        {
            return $"{Left} {Op} {Right}";
        }
    }
}
