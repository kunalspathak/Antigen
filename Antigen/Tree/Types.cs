using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Antigen.Tree
{
    public enum Primitive : ulong
    {
        Void = 0x0,

        Boolean = 0x1,
        Byte = 0x2,
        SByte = 0x4,
        Short = 0x8,
        UShort = 0x10,
        Int = 0x20,
        UInt = 0x40,
        Long = 0x80,
        ULong = 0x100,

        Float = 0x200,
        Double = 0x400,
        Decimal = 0x800,

        Char = 0x1000,
        String = 0x2000,
        Struct = 0x4000,

        Numeric = Byte | SByte | Short | UShort | Int | UInt | Long | ULong | Float | Double | Decimal,
        SignedInteger = SByte | Short | Int | Long,
        UnsignedInteger = Byte | UShort | UInt | ULong,
        Integer = SignedInteger | UnsignedInteger,
        FloatingPoint = Float | Double | Decimal,
        Any = Numeric | Char | String | Boolean,
    }

    public enum VectorType : ulong
    {
        Vector64_Byte = 1,
        Vector64_SByte = 2,
        Vector64_Short = 3,
        Vector64_UShort = 4,
        Vector64_Int = 5,
        Vector64_UInt = 6,
        Vector64_Long = 7,
        Vector64_ULong = 8,
        Vector64_Float = 9,
        Vector64_Double = 10,

        Vector128_Byte = 11,
        Vector128_SByte = 12,
        Vector128_Short = 13,
        Vector128_UShort = 14,
        Vector128_Int = 15,
        Vector128_UInt = 16,
        Vector128_Long = 17,
        Vector128_ULong = 18,
        Vector128_Float = 19,
        Vector128_Double = 20,

        Vector256_Byte = 21,
        Vector256_SByte = 22,
        Vector256_Short = 23,
        Vector256_UShort = 24,
        Vector256_Int = 25,
        Vector256_UInt = 26,
        Vector256_Long = 27,
        Vector256_ULong = 28,
        Vector256_Float = 29,
        Vector256_Double = 30,

        Vector512_Byte = 31,
        Vector512_SByte = 32,
        Vector512_Short = 33,
        Vector512_UShort = 34,
        Vector512_Int = 35,
        Vector512_UInt = 36,
        Vector512_Long = 37,
        Vector512_ULong = 38,
        Vector512_Float = 39,
        Vector512_Double = 40,

        Vector2 = 41,
        Vector3 = 42,
        Vector4 = 43,
    }

    public struct ValueType
    {
        public bool IsVectorNumerics()
        {
            if (IsVectorType)
            {
                if (VectorType >= VectorType.Vector2 && VectorType <= VectorType.Vector4)
                {
                    return true;
                }
            }

            return false;
        }

        public bool IsVectorIntrinsics()
        {
            if (IsVectorType)
            {
                if (VectorType >= VectorType.Vector64_Byte && VectorType <= VectorType.Vector512_Double)
                {
                    return true;
                }
            }

            return false;
        }

        // TODO: Matrix about implicit and explicit cast

        public bool AllowedPrimitive(Primitive primitives)
        {
            return (primitives & PrimitiveType) != 0;
        }

        public bool AllowedVector(Operator operatorForExpr)
        {
            return (operatorForExpr.IsVectorIntrinsics && IsVectorIntrinsics()) || (operatorForExpr.IsVectorNumerics && IsVectorNumerics());
        }

        public string TypeName
        {
            get
            {
                Debug.Assert(PrimitiveType == Primitive.Struct);
                return _structTypeName;
            }
        }

        private bool isVectorType;
        private string _variableNameHint;
        private string _structTypeName;
        public Primitive PrimitiveType;
        public VectorType VectorType;
        public SyntaxKind TypeKind;
        private readonly string _displayText;

        private ValueType(Primitive valueType, string displayText, SyntaxKind typeKind)
        {
            PrimitiveType = valueType;
            TypeKind = typeKind;
            _structTypeName = null;
            _displayText = displayText;
            _variableNameHint = displayText;
            isVectorType = false;
            VectorType = 0;
        }

        private ValueType(VectorType vectorType, string displayText, string variableNameHint)
        {
            VectorType = vectorType;
            _displayText = displayText;
            _structTypeName = null;
            _variableNameHint = variableNameHint;
            isVectorType = true;
        }

        private static readonly List<ValueType> types = new List<ValueType>()
        {
            new ValueType(Primitive.Boolean,    "bool",     SyntaxKind.BoolKeyword),
            new ValueType(Primitive.Byte,       "byte",     SyntaxKind.ByteKeyword),
            new ValueType(Primitive.Char,       "char",       SyntaxKind.CharKeyword),
            new ValueType(Primitive.Decimal,    "decimal",    SyntaxKind.DecimalKeyword),
            new ValueType(Primitive.Double,     "double",     SyntaxKind.DoubleKeyword),
            new ValueType(Primitive.Short,      "short" ,      SyntaxKind.ShortKeyword),
            new ValueType(Primitive.Int,        "int",      SyntaxKind.IntKeyword),
            new ValueType(Primitive.Long,       "long",      SyntaxKind.LongKeyword),
            new ValueType(Primitive.SByte,      "sbyte",      SyntaxKind.SByteKeyword),
            new ValueType(Primitive.Float,      "float",     SyntaxKind.FloatKeyword),
            new ValueType(Primitive.String,     "string",     SyntaxKind.StringKeyword),
            new ValueType(Primitive.UShort,     "ushort",     SyntaxKind.UShortKeyword),
            new ValueType(Primitive.UInt,       "uint",     SyntaxKind.UIntKeyword),
            new ValueType(Primitive.ULong,      "ulong",     SyntaxKind.ULongKeyword),
        };

        private static readonly List<ValueType> s_vectorTypes = new();

        static ValueType()
        {
            if (Program.s_runOptions.SupportsVector64)
            {
                s_vectorTypes.Add(new ValueType(VectorType.Vector64_Byte, "Vector64<byte>", "v64_byte"));
                s_vectorTypes.Add(new ValueType(VectorType.Vector64_SByte, "Vector64<sbyte>", "v64_sbyte"));
                s_vectorTypes.Add(new ValueType(VectorType.Vector64_Short, "Vector64<short>", "v64_short"));
                s_vectorTypes.Add(new ValueType(VectorType.Vector64_UShort, "Vector64<ushort>", "v64_ushort"));
                s_vectorTypes.Add(new ValueType(VectorType.Vector64_Int, "Vector64<int>", "v64_int"));
                s_vectorTypes.Add(new ValueType(VectorType.Vector64_UInt, "Vector64<uint>", "v64_uint"));
                s_vectorTypes.Add(new ValueType(VectorType.Vector64_Long, "Vector64<long>", "v64_long"));
                s_vectorTypes.Add(new ValueType(VectorType.Vector64_ULong, "Vector64<ulong>", "v64_ulong"));
                s_vectorTypes.Add(new ValueType(VectorType.Vector64_Float, "Vector64<float>", "v64_float"));
                s_vectorTypes.Add(new ValueType(VectorType.Vector64_Double, "Vector64<double>", "v64_double"));
            }

            if (Program.s_runOptions.SupportsVector128)
            {
                s_vectorTypes.Add(new ValueType(VectorType.Vector128_Byte, "Vector128<byte>", "v128_byte"));
                s_vectorTypes.Add(new ValueType(VectorType.Vector128_SByte, "Vector128<sbyte>", "v128_sbyte"));
                s_vectorTypes.Add(new ValueType(VectorType.Vector128_Short, "Vector128<short>", "v128_short"));
                s_vectorTypes.Add(new ValueType(VectorType.Vector128_UShort, "Vector128<ushort>", "v128_ushort"));
                s_vectorTypes.Add(new ValueType(VectorType.Vector128_Int, "Vector128<int>", "v128_int"));
                s_vectorTypes.Add(new ValueType(VectorType.Vector128_UInt, "Vector128<uint>", "v128_uint"));
                s_vectorTypes.Add(new ValueType(VectorType.Vector128_Long, "Vector128<long>", "v128_long"));
                s_vectorTypes.Add(new ValueType(VectorType.Vector128_ULong, "Vector128<ulong>", "v128_ulong"));
                s_vectorTypes.Add(new ValueType(VectorType.Vector128_Float, "Vector128<float>", "v128_float"));
                s_vectorTypes.Add(new ValueType(VectorType.Vector128_Double, "Vector128<double>", "v128_double"));
            }

            if (Program.s_runOptions.SupportsVector256)
            {
                s_vectorTypes.Add(new ValueType(VectorType.Vector256_Byte, "Vector256<byte>", "v256_byte"));
                s_vectorTypes.Add(new ValueType(VectorType.Vector256_SByte, "Vector256<sbyte>", "v256_sbyte"));
                s_vectorTypes.Add(new ValueType(VectorType.Vector256_Short, "Vector256<short>", "v256_short"));
                s_vectorTypes.Add(new ValueType(VectorType.Vector256_UShort, "Vector256<ushort>", "v256_ushort"));
                s_vectorTypes.Add(new ValueType(VectorType.Vector256_Int, "Vector256<int>", "v256_int"));
                s_vectorTypes.Add(new ValueType(VectorType.Vector256_UInt, "Vector256<uint>", "v256_uint"));
                s_vectorTypes.Add(new ValueType(VectorType.Vector256_Long, "Vector256<long>", "v256_long"));
                s_vectorTypes.Add(new ValueType(VectorType.Vector256_ULong, "Vector256<ulong>", "v256_ulong"));
                s_vectorTypes.Add(new ValueType(VectorType.Vector256_Float, "Vector256<float>", "v256_float"));
                s_vectorTypes.Add(new ValueType(VectorType.Vector256_Double, "Vector256<double>", "v256_double"));
            }

            if (Program.s_runOptions.SupportsVector512)
            {
                s_vectorTypes.Add(new ValueType(VectorType.Vector512_Byte, "Vector512<byte>", "v512_byte"));
                s_vectorTypes.Add(new ValueType(VectorType.Vector512_SByte, "Vector512<sbyte>", "v512_sbyte"));
                s_vectorTypes.Add(new ValueType(VectorType.Vector512_Short, "Vector512<short>", "v512_short"));
                s_vectorTypes.Add(new ValueType(VectorType.Vector512_UShort, "Vector512<ushort>", "v512_ushort"));
                s_vectorTypes.Add(new ValueType(VectorType.Vector512_Int, "Vector512<int>", "v512_int"));
                s_vectorTypes.Add(new ValueType(VectorType.Vector512_UInt, "Vector512<uint>", "v512_uint"));
                s_vectorTypes.Add(new ValueType(VectorType.Vector512_Long, "Vector512<long>", "v512_long"));
                s_vectorTypes.Add(new ValueType(VectorType.Vector512_ULong, "Vector512<ulong>", "v512_ulong"));
                s_vectorTypes.Add(new ValueType(VectorType.Vector512_Float, "Vector512<float>", "v512_float"));
                s_vectorTypes.Add(new ValueType(VectorType.Vector512_Double, "Vector512<double>", "v512_double"));
            }

            s_vectorTypes.Add(new ValueType(VectorType.Vector2, "Vector2", "v2"));
            s_vectorTypes.Add(new ValueType(VectorType.Vector3, "Vector3", "v3"));
            s_vectorTypes.Add(new ValueType(VectorType.Vector4, "Vector4", "v4"));
        }

        private static readonly Regex vectorRegex = new Regex(@"Vector(64|128|256|512)`1\[(.*)\]");
        private static readonly Regex multipleVectorsRegex = new Regex(@"Vector(64|128|256|512)`1");

        /// <summary>
        ///     Returns Vector length for given type. `null` if not a VectorType.
        /// </summary>
        /// <param name="typeName"></param>
        /// <returns></returns>
        public static string GetVectorList(string typeName)
        {

            var vectorTypeMatches = multipleVectorsRegex.Matches(typeName);
            if (vectorTypeMatches.Count == 0)
            {
                return null;
            }

            string result = "|";
            foreach (Match vectorTypeMatch in vectorTypeMatches)
            {
                Debug.Assert(vectorTypeMatch.Groups.Count == 2);
                result += (vectorTypeMatch.Groups[1].Value + "|");
            }

            return result;
        }

        public static ValueType ParseType(string typeName)
        {
            VectorType parsedVectorType;
            var vectorTypeMatch = vectorRegex.Match(typeName);
            if (vectorTypeMatch.Success)
            {
                Debug.Assert(vectorTypeMatch.Groups.Count == 3);
                var vectorLength = vectorTypeMatch.Groups[1].Value;
                var templateParameterType = vectorTypeMatch.Groups[2].Value;

                parsedVectorType = vectorLength switch
                {
                    "64" => templateParameterType switch
                    {
                        "System.Byte" => VectorType.Vector64_Byte,
                        "System.SByte" => VectorType.Vector64_SByte,
                        "System.Int16" => VectorType.Vector64_Short,
                        "System.UInt16" => VectorType.Vector64_UShort,
                        "System.Int32" => VectorType.Vector64_Int,
                        "System.UInt32" => VectorType.Vector64_UInt,
                        "System.Int64" => VectorType.Vector64_Long,
                        "System.UInt64" => VectorType.Vector64_ULong,
                        "System.Single" => VectorType.Vector64_Float,
                        "System.Double" => VectorType.Vector64_Double,
                        _ => throw new Exception("Invalid template parameter for Vector64"),
                    },
                    "128" => templateParameterType switch
                    {
                        "System.Byte" => VectorType.Vector128_Byte,
                        "System.SByte" => VectorType.Vector128_SByte,
                        "System.Int16" => VectorType.Vector128_Short,
                        "System.UInt16" => VectorType.Vector128_UShort,
                        "System.Int32" => VectorType.Vector128_Int,
                        "System.UInt32" => VectorType.Vector128_UInt,
                        "System.Int64" => VectorType.Vector128_Long,
                        "System.UInt64" => VectorType.Vector128_ULong,
                        "System.Single" => VectorType.Vector128_Float,
                        "System.Double" => VectorType.Vector128_Double,
                        _ => throw new Exception("Invalid template parameter for Vector128"),
                    },
                    "256" => templateParameterType switch
                    {
                        "System.Byte" => VectorType.Vector256_Byte,
                        "System.SByte" => VectorType.Vector256_SByte,
                        "System.Int16" => VectorType.Vector256_Short,
                        "System.UInt16" => VectorType.Vector256_UShort,
                        "System.Int32" => VectorType.Vector256_Int,
                        "System.UInt32" => VectorType.Vector256_UInt,
                        "System.Int64" => VectorType.Vector256_Long,
                        "System.UInt64" => VectorType.Vector256_ULong,
                        "System.Single" => VectorType.Vector256_Float,
                        "System.Double" => VectorType.Vector256_Double,
                        _ => throw new Exception("Invalid template parameter for Vector256"),
                    },
                "512" => templateParameterType switch
                {
                    "System.Byte" => VectorType.Vector512_Byte,
                    "System.SByte" => VectorType.Vector512_SByte,
                    "System.Int16" => VectorType.Vector512_Short,
                    "System.UInt16" => VectorType.Vector512_UShort,
                    "System.Int32" => VectorType.Vector512_Int,
                    "System.UInt32" => VectorType.Vector512_UInt,
                    "System.Int64" => VectorType.Vector512_Long,
                    "System.UInt64" => VectorType.Vector512_ULong,
                    "System.Single" => VectorType.Vector512_Float,
                    "System.Double" => VectorType.Vector512_Double,
                    _ => throw new Exception("Invalid template parameter for Vector512"),
                },
                    _ => throw new Exception("Invalid vector length"),
                };
                return s_vectorTypes.FirstOrDefault(v => v.VectorType == parsedVectorType);
            }
            else if (typeName.Contains("System.Numerics"))
            {
                parsedVectorType = typeName switch
                {
                    "System.Numerics.Vector2" => VectorType.Vector2,
                    "System.Numerics.Vector3" => VectorType.Vector3,
                    "System.Numerics.Vector4" => VectorType.Vector4,
                    _ => throw new Exception("Invalid vector type Vector"),
                };
                return s_vectorTypes.FirstOrDefault(v => v.VectorType == parsedVectorType);
            }
            else
            {
                var parsedPrimitiveType = typeName switch
                {
                    "System.Void" => Primitive.Void,
                    "System.Boolean" => Primitive.Boolean,
                    "System.Byte" => Primitive.Byte,
                    "System.SByte" => Primitive.SByte,
                    "System.Int16" => Primitive.Short,
                    "System.UInt16" => Primitive.UShort,
                    "System.Int32" => Primitive.Int,
                    "System.UInt32" => Primitive.UInt,
                    "System.Int64" => Primitive.Long,
                    "System.UInt64" => Primitive.ULong,
                    "System.Single" => Primitive.Float,
                    "System.Double" => Primitive.Double,
                    _ => throw new Exception("Invalid typename parameter"),
                };
                return types.FirstOrDefault(t => t.PrimitiveType == parsedPrimitiveType);
            }
        }

        /// <summary>
        ///  Get the number of elements for Vector type.
        /// </summary>
        /// <param name="vectorType"></param>
        /// <returns></returns>
        public static int GetElementCount(VectorType vectorType)
        {
            switch (vectorType)
            {
                case VectorType.Vector64_Byte: return Vector64<byte>.Count;
                case VectorType.Vector64_SByte: return Vector64<sbyte>.Count;
                case VectorType.Vector64_Short: return Vector64<short>.Count;
                case VectorType.Vector64_UShort: return Vector64<ushort>.Count;
                case VectorType.Vector64_Int: return Vector64<int>.Count;
                case VectorType.Vector64_UInt: return Vector64<uint>.Count;
                case VectorType.Vector64_Long: return Vector64<long>.Count;
                case VectorType.Vector64_ULong: return Vector64<ulong>.Count;
                case VectorType.Vector64_Float: return Vector64<float>.Count;
                case VectorType.Vector64_Double: return Vector64<double>.Count;

                case VectorType.Vector128_Byte: return Vector128<byte>.Count;
                case VectorType.Vector128_SByte: return Vector128<sbyte>.Count;
                case VectorType.Vector128_Short: return Vector128<short>.Count;
                case VectorType.Vector128_UShort: return Vector128<ushort>.Count;
                case VectorType.Vector128_Int: return Vector128<int>.Count;
                case VectorType.Vector128_UInt: return Vector128<uint>.Count;
                case VectorType.Vector128_Long: return Vector128<long>.Count;
                case VectorType.Vector128_ULong: return Vector128<ulong>.Count;
                case VectorType.Vector128_Float: return Vector128<float>.Count;
                case VectorType.Vector128_Double: return Vector128<double>.Count;

                case VectorType.Vector256_Byte: return Vector256<byte>.Count;
                case VectorType.Vector256_SByte: return Vector256<sbyte>.Count;
                case VectorType.Vector256_Short: return Vector256<short>.Count;
                case VectorType.Vector256_UShort: return Vector256<ushort>.Count;
                case VectorType.Vector256_Int: return Vector256<int>.Count;
                case VectorType.Vector256_UInt: return Vector256<uint>.Count;
                case VectorType.Vector256_Long: return Vector256<long>.Count;
                case VectorType.Vector256_ULong: return Vector256<ulong>.Count;
                case VectorType.Vector256_Float: return Vector256<float>.Count;
                case VectorType.Vector256_Double: return Vector256<double>.Count;

                case VectorType.Vector512_Byte: return Vector512<byte>.Count;
                case VectorType.Vector512_SByte: return Vector512<sbyte>.Count;
                case VectorType.Vector512_Short: return Vector512<short>.Count;
                case VectorType.Vector512_UShort: return Vector512<ushort>.Count;
                case VectorType.Vector512_Int: return Vector512<int>.Count;
                case VectorType.Vector512_UInt: return Vector512<uint>.Count;
                case VectorType.Vector512_Long: return Vector512<long>.Count;
                case VectorType.Vector512_ULong: return Vector512<ulong>.Count;
                case VectorType.Vector512_Float: return Vector512<float>.Count;
                case VectorType.Vector512_Double: return Vector512<double>.Count;

                default: return 0;
            }
        }

        /// <summary>
        ///     Returns true if this ValueType can be converted to <paramref name="toType"/> implicitely.
        /// </summary>
        public bool CanConvertImplicit(ValueType toType)
        {
            if (PrimitiveType == toType.PrimitiveType)
            {
                return true;
            }

            List<Primitive> toTypes = null;
            if (!implicitConversions.TryGetValue(this.PrimitiveType, out toTypes))
            {
                return false;
            }
            return toTypes.Contains(toType.PrimitiveType);
        }

        /// <summary>
        ///     Returns true if this ValueType can be converted to <paramref name="toType"/> explicitely.
        /// </summary>
        public bool CanConvertExplicit(ValueType toType)
        {
            if (PrimitiveType == toType.PrimitiveType)
            {
                // if fromType and toType are same, no need of explicit convert
                return false;
            }

            List<Primitive> toTypes = null;
            if (!explicitConversions.TryGetValue(this.PrimitiveType, out toTypes))
            {
                return false;
            }
            return toTypes.Contains(toType.PrimitiveType);
        }

        /// <summary>
        ///     Returns true if this ValueType can be converted to <paramref name="toType"/> implicitely or explicitely.
        /// </summary>
        public bool CanConvert(ValueType toType)
        {
            return CanConvertImplicit(toType) || CanConvertExplicit(toType);
        }

        // https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/numeric-conversions#implicit-numeric-conversions
        private static readonly Dictionary<Primitive, List<Primitive>> implicitConversions = new Dictionary<Primitive, List<Primitive>>()
        {
            {Primitive.SByte,  new () { Primitive.Short, Primitive.Int,    Primitive.Long,   Primitive.Float,  Primitive.Double, Primitive.Decimal } },
            {Primitive.Byte,   new () { Primitive.Short, Primitive.UShort, Primitive.Int,    Primitive.UInt,   Primitive.Long,  Primitive.ULong, Primitive.Float, Primitive.Double, Primitive.Decimal } },
            {Primitive.Short,  new () { Primitive.Int,   Primitive.Long,   Primitive.Float,  Primitive.Double, Primitive.Decimal } },
            {Primitive.UShort, new () { Primitive.Int,   Primitive.UInt,   Primitive.Long,   Primitive.ULong,  Primitive.Float, Primitive.Double, Primitive.Decimal} },
            {Primitive.Int,    new () { Primitive.Long,  Primitive.Float,  Primitive.Double, Primitive.Decimal } },
            {Primitive.UInt,   new () { Primitive.Long,  Primitive.ULong,  Primitive.Float,  Primitive.Double, Primitive.Decimal } },
            {Primitive.Long,   new () { Primitive.Float, Primitive.Double, Primitive.Decimal} },
            {Primitive.Float,  new () { Primitive.Double} },
        };

        // https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/numeric-conversions#explicit-numeric-conversions
        private static readonly Dictionary<Primitive, List<Primitive>> explicitConversions = new Dictionary<Primitive, List<Primitive>>()
        {
            {Primitive.SByte,  new () { Primitive.Byte,  Primitive.UShort,  Primitive.UInt,  Primitive.ULong } },
            {Primitive.Byte,   new () { Primitive.SByte } },
            {Primitive.Short,  new () { Primitive.SByte, Primitive.Byte,   Primitive.UShort, Primitive.UInt, Primitive.ULong} },
            {Primitive.UShort, new () { Primitive.SByte, Primitive.Byte, Primitive.Short} },
            {Primitive.Int,     new () { Primitive.SByte, Primitive.Byte, Primitive.Short, Primitive.UShort, Primitive.UInt, Primitive.ULong} },
            {Primitive.UInt,    new () { Primitive.SByte, Primitive.Byte, Primitive.Short, Primitive.UShort, Primitive.Int} },
            {Primitive.Long,    new () { Primitive.SByte, Primitive.Byte, Primitive.Short, Primitive.UShort, Primitive.Int, Primitive.UInt, Primitive.ULong } },
            {Primitive.ULong,   new () { Primitive.SByte, Primitive.Byte, Primitive.Short, Primitive.UShort, Primitive.Int, Primitive.UInt, Primitive.Long } },
            {Primitive.Float,   new () { Primitive.SByte, Primitive.Byte, Primitive.Short, Primitive.UShort, Primitive.Int, Primitive.UInt, Primitive.Long, Primitive.ULong, Primitive.Decimal } },
            {Primitive.Double,  new () { Primitive.SByte, Primitive.Byte, Primitive.Short, Primitive.UShort, Primitive.Int, Primitive.UInt, Primitive.Long, Primitive.ULong, Primitive.Float, Primitive.Decimal } },
            {Primitive.Decimal, new () { Primitive.SByte, Primitive.Byte, Primitive.Short, Primitive.UShort, Primitive.Int, Primitive.UInt, Primitive.Long, Primitive.ULong, Primitive.Float, Primitive.Double } },
        };

        public static ValueType CreateStructType(string typeName)
        {
            var structType = new ValueType(Primitive.Struct, typeName, SyntaxKind.None);
            structType._structTypeName = typeName;
            structType._variableNameHint = typeName.ToLower().Replace(".", "_").ToLower();
            return structType;
        }

        public static List<ValueType> GetTypes()
        {
            return types;
        }

        public static List<ValueType> GetVectorTypes()
        {
            return s_vectorTypes;
        }

        public static ValueType GetRandomType()
        {
            return types[PRNG.Next(types.Count)];
        }

        public override bool Equals(object obj)
        {
            ValueType otherType = (ValueType)obj;
            bool result = otherType.isVectorType ? (VectorType == otherType.VectorType) : (PrimitiveType == otherType.PrimitiveType);
            return result &&
                TypeKind == otherType.TypeKind &&
                _structTypeName == otherType._structTypeName;
        }

        public override int GetHashCode()
        {
            int hashCode = PrimitiveType.GetHashCode() ^ VectorType.GetHashCode() ^ TypeKind.GetHashCode();
            if (_structTypeName != null)
            {
                hashCode ^= _structTypeName.GetHashCode();
            }
            return hashCode;
        }

        public static ValueType ForPrimitive(Primitive primitiveType)
        {
            return types.First(t => t.PrimitiveType == primitiveType);
        }

        private static ValueType voidType = new ValueType(Primitive.Void, "void", SyntaxKind.VoidKeyword);
        public static ValueType ForVoid()
        {
            return voidType;
        }

        public string VariableNameHint()
        {
            return _variableNameHint;
        }

        public static IReadOnlyList<Type> AllExceptions =>
            typeof(Exception).Assembly.GetTypes()
                .Where(x => x.IsSubclassOf(typeof(Exception)))
                .Where(n => n.FullName.StartsWith("System.") && n.FullName.LastIndexOf(".") == 6).ToList();

        public bool IsVectorType { get => isVectorType; private set => isVectorType = value; }

        public override string ToString()
        {
            return _displayText;
        }
    }
}
