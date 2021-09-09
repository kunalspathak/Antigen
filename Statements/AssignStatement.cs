// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Antigen.Expressions;
using Antigen.Tree;

namespace Antigen.Statements
{
    public class AssignStatement : Statement
    {
        public readonly Expression Left;
        public readonly Operator Op;
        public readonly Expression Right;

        public AssignStatement(TestCase testCase, Expression lhs, Operator op, Expression rhs) : base(testCase)
        {
            Left = lhs;
            Op = op;
            Right = rhs;
        }

        public override string ToString()
        {
            return $"{Left} {Op} {Right};";
        }
    }
}
