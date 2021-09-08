// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Antigen.Statements
{
    public class ReturnStatement : Statement
    {
        public readonly Expression Expression;

        public ReturnStatement(TestCase testCase, Expression expression) : base(testCase)
        {
            Expression = expression;
        }

        public override string ToString()
        {
            return $"return {Expression};";
        }
    }
}
