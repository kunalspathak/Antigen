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
    public class MethodCallStatement : Statement
    {
        public readonly string MethodName;
        public readonly List<Expression> Arguments;

        public MethodCallStatement(TestCase testCase, string methodName, List<Expression> arguments) : base(testCase)
        {
            MethodName = methodName;
            Arguments = arguments;
        }

        public override string ToString()
        {
            return $"{MethodName}({string.Join(", ", Arguments)});";
        }
    }
}
