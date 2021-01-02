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
            return (float)(PRNG.Next(int.MaxValue) + 1.5);
        }

        public static double GetRandomDouble()
        {
            return (double)(PRNG.Next(int.MaxValue) + 2.5);
        }

        public static decimal GetRandomDecimal()
        {
            return (decimal)(PRNG.Next(int.MaxValue) + 3.5);
        }

        public static LiteralExpressionSyntax GetLiteralExpression(Tree.ValueType literalType)
        {
            SyntaxToken literalToken;
            SyntaxKind kind;
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
                case Tree.Primitive.Byte:
                    kind = SyntaxKind.NumericLiteralExpression;
                    literalToken = Literal(GetRandomByte());
                    break;
                case Tree.Primitive.Char:
                    kind = SyntaxKind.CharacterLiteralExpression;
                    literalToken = Literal(GetRandomChar());
                    break;
                case Tree.Primitive.Int16:
                    kind = SyntaxKind.NumericLiteralExpression;
                    literalToken = Literal(GetRandomShort());
                    break;
                case Tree.Primitive.Int32:
                    kind = SyntaxKind.NumericLiteralExpression;
                    literalToken = Literal(GetRandomInt());
                    break;
                case Tree.Primitive.Int64:
                    kind = SyntaxKind.NumericLiteralExpression;
                    literalToken = Literal(GetRandomLong());
                    break;
                case Tree.Primitive.UInt16:
                    kind = SyntaxKind.NumericLiteralExpression;
                    literalToken = Literal(GetRandomUShort());
                    break;
                case Tree.Primitive.UInt32:
                    kind = SyntaxKind.NumericLiteralExpression;
                    literalToken = Literal(GetRandomUInt());
                    break;
                case Tree.Primitive.UInt64:
                    kind = SyntaxKind.NumericLiteralExpression;
                    literalToken = Literal(GetRandomULong());
                    break;
                case Tree.Primitive.SByte:
                    kind = SyntaxKind.NumericLiteralExpression;
                    literalToken = Literal(GetRandomSByte());
                    break;
                case Tree.Primitive.Single:
                    kind = SyntaxKind.NumericLiteralExpression;
                    literalToken = Literal(GetRandomFloat());
                    break;
                case Tree.Primitive.Decimal:
                    kind = SyntaxKind.NumericLiteralExpression;
                    literalToken = Literal(GetRandomDecimal());
                    break;
                case Tree.Primitive.Double:
                    kind = SyntaxKind.NumericLiteralExpression;
                    literalToken = Literal(GetRandomDouble());
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

            return LiteralExpression(kind, literalToken);
        }
    }
}
