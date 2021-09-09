// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
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

        protected ConstantValue(Tree.ValueType valueType, string value) : base(null)
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

        public static ConstantValue GetRandomConstantInt()
        {
            return new ConstantValue(Tree.ValueType.ForPrimitive(Primitive.Int), PRNG.Next(10, 100).ToString());
        }

        public static ConstantValue GetConstantValue(Tree.ValueType literalType, IList<Weights<int>> numerals)
        {
            string constantValue;
            if ((literalType.PrimitiveType & Primitive.Numeric) != 0)
            {
                // numeric
                int literalValue = PRNG.WeightedChoice(numerals);
                constantValue = literalValue.ToString();

                // If unsigned, and number selected is negative, then flip it
                if ((literalType.PrimitiveType & Primitive.UnsignedInteger) != 0)
                {
                    if (literalValue < 0)
                    {
                        if (literalValue == int.MinValue)
                        {
                            literalValue = int.MaxValue;
                        }
                        else
                        {
                            literalValue *= -1;
                        }
                    }
                }

                switch (literalType.PrimitiveType)
                {
                    case Tree.Primitive.Byte:
                        constantValue = ((byte)(literalValue % byte.MaxValue)).ToString();
                        break;
                    case Tree.Primitive.SByte:
                        constantValue = ((sbyte)(literalValue % sbyte.MaxValue)).ToString();
                        break;
                    case Tree.Primitive.UShort:
                        constantValue = ((ushort)(literalValue % ushort.MaxValue)).ToString();
                        break;
                    case Tree.Primitive.Short:
                        constantValue = ((short)(literalValue % short.MaxValue)).ToString();
                        break;
                    case Tree.Primitive.Int:
                    case Tree.Primitive.UInt:
                    case Tree.Primitive.Long:
                    case Tree.Primitive.ULong:
                        // already constantValue is populated
                        break;
                    case Tree.Primitive.Float:
                        constantValue = ((float)literalValue + (float)PRNG.Next(5) / PRNG.Next(10, 100)).ToString();
                        break;
                    case Tree.Primitive.Decimal:
                        constantValue = ((decimal)literalValue + (decimal)PRNG.Next(5) / PRNG.Next(10, 100)).ToString();
                        break;
                    case Tree.Primitive.Double:
                        constantValue = ((double)literalValue + (double)PRNG.Next(5) / PRNG.Next(10, 100)).ToString();
                        break;
                    default:
                        Debug.Assert(false, string.Format("Hit unknown value type {0}", Enum.GetName(typeof(Tree.Primitive), literalType.PrimitiveType)));
                        constantValue = "1";
                        break;
                }
            }
            else
            {
                // non-numeric
                switch (literalType.PrimitiveType)
                {
                    case Tree.Primitive.Boolean:
                        constantValue = PRNG.Decide(0.5) ? "true" : "false";
                        break;

                    case Tree.Primitive.Char:
                        constantValue = Helpers.GetRandomChar().ToString();
                        break;

                    case Tree.Primitive.String:
                        constantValue = Helpers.GetRandomString();
                        break;
                    default:
                        Debug.Assert(false, string.Format("Hit unknown value type {0}", Enum.GetName(typeof(Tree.Primitive), literalType.PrimitiveType)));
                        constantValue = "1";

                        break;
                }
            }

            return new ConstantValue(literalType, constantValue);
        }
    }
}
