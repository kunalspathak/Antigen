using Antigen.Tree;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Antigen
{
    public static partial class Helpers
    {
        private static char[] Alphabet = new char[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z' };

        private static IList<Weights<int>> Numerals = new List<Weights<int>>()
        {
            new Weights<int>(int.MinValue, (double) PRNG.Next(1, 10) / 10000 ),
            new Weights<int>(int.MinValue + 1, (double)PRNG.Next(1, 10) / 10000 ),
            new Weights<int>(PRNG.Next(-100, -6), (double) PRNG.Next(1, 10) / 1000 ),
            new Weights<int>(-5, (double) PRNG.Next(1, 10) / 1000 ),
            new Weights<int>(-2, (double) PRNG.Next(1, 10) / 1000 ),
            new Weights<int>(-1, (double) PRNG.Next(1, 10) / 1000 ),
            new Weights<int>(0, (double) PRNG.Next(1, 10) / 1000 ),
            new Weights<int>(1, (double) PRNG.Next(1, 10) / 1000 ),
            new Weights<int>(2, (double) PRNG.Next(1, 10) / 1000 ),
            new Weights<int>(5, (double) PRNG.Next(1, 10) / 1000 ),
            new Weights<int>(PRNG.Next(6, 100), (double) PRNG.Next(1, 10) / 1000 ),
            new Weights<int>(int.MaxValue - 1, (double) PRNG.Next(1, 10) / 10000 ),
            new Weights<int>(int.MaxValue, (double) PRNG.Next(1, 10) / 10000 ),
        };

        public static byte GetRandomByte()
        {
            return (byte)PRNG.Next(byte.MinValue, byte.MaxValue);
        }

        public static short GetRandomShort()
        {
            return (short)PRNG.Next(short.MinValue, short.MaxValue);
        }

        public static int GetRandomInt()
        {
            return PRNG.Next(int.MinValue, int.MaxValue);
        }

        public static long GetRandomLong()
        {
            return PRNG.NextLong(long.MaxValue);
        }

        public static ushort GetRandomUShort()
        {
            return (ushort)PRNG.Next(ushort.MaxValue);
        }

        public static uint GetRandomUInt()
        {
            return (uint)PRNG.NextLong(uint.MaxValue);
        }

        public static ulong GetRandomULong()
        {
            return (ulong)PRNG.NextLong(long.MaxValue);
        }

        public static char GetRandomChar()
        {
            return Alphabet[PRNG.Next(Alphabet.Length)];
        }

        public static string GetRandomString(int length = -1)
        {
            length = (length == -1) ? PRNG.Next(10) : length;
            StringBuilder strBuilder = new StringBuilder();
            for (int c = 0; c < length; c++)
            {
                strBuilder.Append(Alphabet[PRNG.Next(Alphabet.Length)]);
            }
            return strBuilder.ToString();
        }

        public static bool GetRandomBoolean()
        {
            return PRNG.Decide(0.5) ? true : false;
        }

        public static sbyte GetRandomSByte()
        {
            return (sbyte)PRNG.Next(sbyte.MinValue, sbyte.MaxValue);
        }

        public static float GetRandomFloat()
        {
            return (float)PRNG.Next(10) + (1 / PRNG.Next(1, 5));
        }

        public static double GetRandomDouble()
        {
            return (double)PRNG.Next(10) + (1 / PRNG.Next(1, 5));
        }

        public static decimal GetRandomDecimal()
        {
            return (decimal)PRNG.Next(10) + (1 / PRNG.Next(1, 5));
        }

        public static LiteralExpressionSyntax GetLiteralExpression(Tree.ValueType literalType)
        {
            SyntaxToken literalToken;
            SyntaxKind kind;

            if ((literalType.PrimitiveType & Primitive.Numeric) != 0)
            {
                // numeric
                kind = SyntaxKind.NumericLiteralExpression;
                int literalValue = PRNG.WeightedChoice(Numerals);

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
                        literalToken = Literal((byte)(literalValue % byte.MaxValue));
                        break;
                    case Tree.Primitive.SByte:
                        literalToken = Literal((sbyte)(literalValue % sbyte.MaxValue));
                        break;
                    case Tree.Primitive.UShort:
                        literalToken = Literal((ushort)(literalValue % ushort.MaxValue));
                        break;
                    case Tree.Primitive.Short:
                        literalToken = Literal((short)(literalValue % short.MaxValue));
                        break;
                    case Tree.Primitive.Int:
                        literalToken = Literal(literalValue);
                        break;
                    case Tree.Primitive.UInt:
                        literalToken = Literal(literalValue);
                        break;
                    case Tree.Primitive.Long:
                        literalToken = Literal(literalValue);
                        break;
                    case Tree.Primitive.ULong:
                        literalToken = Literal(literalValue);
                        break;
                    case Tree.Primitive.Float:
                        literalToken = Literal((float)literalValue + (float)PRNG.Next(5) / PRNG.Next(10, 100));
                        break;
                    case Tree.Primitive.Decimal:
                        literalToken = Literal((decimal)literalValue + (decimal)PRNG.Next(5) / PRNG.Next(10, 100));
                        break;
                    case Tree.Primitive.Double:
                        literalToken = Literal((double)literalValue + (double)PRNG.Next(5) / PRNG.Next(10, 100));
                        break;
                    default:
                        Debug.Assert(false, String.Format("Hit unknown value type {0}", Enum.GetName(typeof(Tree.Primitive), literalType.PrimitiveType)));

                        kind = SyntaxKind.NumericLiteralExpression;
                        literalToken = Literal(1);
                        break;
                }
            }
            else
            {
                // non-numeric
                switch (literalType.PrimitiveType)
                {
                    case Tree.Primitive.Boolean:
                        if (GetRandomBoolean())
                        {
                            kind = SyntaxKind.TrueLiteralExpression;
                            literalToken = Token(SyntaxKind.TrueKeyword);
                        }
                        else
                        {
                            kind = SyntaxKind.FalseLiteralExpression;
                            literalToken = Token(SyntaxKind.FalseKeyword);
                        }
                        break;

                    case Tree.Primitive.Char:
                        kind = SyntaxKind.CharacterLiteralExpression;
                        literalToken = Literal(GetRandomChar());
                        break;

                    case Tree.Primitive.String:
                        kind = SyntaxKind.StringLiteralExpression;
                        literalToken = Literal(GetRandomString());
                        break;
                    default:
                        Debug.Assert(false, String.Format("Hit unknown value type {0}", Enum.GetName(typeof(Tree.Primitive), literalType.PrimitiveType)));

                        kind = SyntaxKind.NumericLiteralExpression;
                        literalToken = Literal(1);
                        break;
                }
            }

            return LiteralExpression(kind, literalToken);
        }
    }
}
