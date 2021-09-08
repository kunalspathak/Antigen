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
    public class AssignExpression : BinaryExpression
    {
        public AssignExpression(TestCase testCase, Expression lhs, Operator op, Expression rhs) : base(testCase, lhs, op, rhs)
        {
        }
    }
}
