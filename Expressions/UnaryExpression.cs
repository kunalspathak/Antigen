// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Antigen.Tree;
using Microsoft.CodeAnalysis.CSharp;

namespace Antigen.Expressions
{
    public class UnaryExpression : Expression
    {
        public readonly Expression Expression;
        public readonly Operator Op;

        private readonly bool _isPostOperation = false;

        public UnaryExpression(TestCase testCase, Expression expression, Operator op) : base(testCase)
        {
            Expression = expression;
            Op = op;
            _isPostOperation = op.Oper == SyntaxKind.PostIncrementExpression || op.Oper == SyntaxKind.PostDecrementExpression;
        }

        public override string ToString()
        {
            return _isPostOperation ? PostOperation() : PreOperation();
        }

        private string PreOperation()
        {
            return $"{Op}{Expression}";
        }
        private string PostOperation()
        {
            return $"{Expression}{Op}";
        }
    }
}
