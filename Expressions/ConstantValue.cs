// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Antigen.Tree;

namespace Antigen.Expressions
{
    public class ConstantValue : Expression
    {
        public readonly string Value;

        public ConstantValue(TestCase testCase, ValueType valueType, string value) : base(testCase)
        {
            if (valueType.PrimitiveType == Primitive.Char)
            {
                Value = $"'{Value}'";
                return;
            }
            else if (valueType.PrimitiveType == Primitive.String)
            {
                Value = $"\"{Value}\"";
                return;
            }
            else if (valueType.PrimitiveType == Primitive.Boolean)
            {
                Debug.Assert(value == "false" || value == "true");
            }
            Value = value;
        }
        public override string ToString()
        {
            return $"{Value}";
        }
    }
}
