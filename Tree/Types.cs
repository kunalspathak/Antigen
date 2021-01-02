﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Antigen.Tree
{
    //public enum TypeFlags : ulong
    //{
    //    Void = 0x0,
    //    Numeric = Primitive.Byte | Primitive.Decimal | Primitive.Double | Primitive.Int16 | Primitive.Int32 | Primitive.Int64 | Primitive.SByte | Primitive.Single | Primitive.UInt16 | Primitive.UInt32 | Primitive.UInt64,
    //    Integer = Primitive.Byte | Primitive.SByte | Primitive.Int16 | Primitive.Int32 | Primitive.Int64 | Primitive.UInt16 | Primitive.UInt32 | Primitive.UInt64,
    //    Decimal = Primitive.Single | Primitive.Double | Primitive.Decimal,
    //    Char = Primitive.Char,
    //    String = Primitive.String,
    //    Bool = Primitive.Boolean,
    //    Any = Numeric | Char | String | Bool,
    //}

    public enum Primitive : ulong
    {
        Void = 0x0,

        Boolean = 0x1,
        Byte = 0x2,
        Char = 0x4,
        Decimal = 0x8,
        Double = 0x10,
        Short = 0x20,
        Int = 0x40,
        Long = 0x80,
        SByte = 0x100,
        Float = 0x200,
        String = 0x400,
        UShort = 0x800,
        UInt = 0x1000,
        ULong = 0x2000,
        Struct = 0x4000,

        Numeric = Byte | Decimal | Double | Short | Int | Long | SByte | Float | UShort | UInt | ULong,
        SignedInteger = SByte | Short | Int | Long,
        UnsignedInteger = Byte | UShort | UInt | ULong,
        Integer = SignedInteger | UnsignedInteger,
        FloatingPoint = Float | Double | Decimal,
        Any = Numeric | Char | String | Boolean,
    }

    public struct ValueType
    {
        // TODO: Matrix about implicit and explicit cast

        public bool AllowedPrimitive(Primitive primitives)
        {
            bool val = (primitives & PrimitiveType) != 0;
            return val;
        }

        public string TypeName
        {
            get
            {
                Debug.Assert(PrimitiveType == Primitive.Struct);
                return _structTypeName;
            }
        }

        private string _structTypeName;
        public Primitive PrimitiveType;
        //private TypeFlags Flags;
        public SpecialType DataType;
        public SyntaxKind TypeKind;

        private static readonly List<ValueType> types = new List<ValueType>()
        {
            new ValueType(Primitive.Boolean, SpecialType.System_Boolean,    SyntaxKind.BoolKeyword),
            new ValueType(Primitive.Byte, SpecialType.System_Byte,       SyntaxKind.ByteKeyword),
            new ValueType(Primitive.Char, SpecialType.System_Char,       SyntaxKind.CharKeyword),
            new ValueType(Primitive.Decimal, SpecialType.System_Decimal,    SyntaxKind.DecimalKeyword),
            new ValueType(Primitive.Double, SpecialType.System_Double,     SyntaxKind.DoubleKeyword),
            new ValueType(Primitive.Short, SpecialType.System_Int16,      SyntaxKind.ShortKeyword),
            new ValueType(Primitive.Int, SpecialType.System_Int32,      SyntaxKind.IntKeyword),
            new ValueType(Primitive.Long, SpecialType.System_Int64,      SyntaxKind.LongKeyword),
            new ValueType(Primitive.SByte, SpecialType.System_SByte,      SyntaxKind.SByteKeyword),
            new ValueType(Primitive.Float, SpecialType.System_Single,     SyntaxKind.FloatKeyword),
            new ValueType(Primitive.String, SpecialType.System_String,     SyntaxKind.StringKeyword),
            new ValueType(Primitive.UShort, SpecialType.System_UInt16,     SyntaxKind.UShortKeyword),
            new ValueType(Primitive.UInt, SpecialType.System_UInt32,     SyntaxKind.UIntKeyword),
            new ValueType(Primitive.ULong, SpecialType.System_UInt64,     SyntaxKind.ULongKeyword),
        };

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
            var structType = new ValueType(Primitive.Struct, SpecialType.None, SyntaxKind.None);
            structType._structTypeName = typeName;
            return structType;
        }

        private ValueType(Primitive valueType, SpecialType dataType, SyntaxKind typeKind)
        {
            PrimitiveType = valueType;
            DataType = dataType;
            TypeKind = typeKind;
            _structTypeName = null;
        }

        public static List<ValueType> GetTypes()
        {
            return types;
        }

        public override bool Equals(object obj)
        {
            ValueType otherType = (ValueType)obj;
            return PrimitiveType == otherType.PrimitiveType &&
                DataType == otherType.DataType &&
                TypeKind == otherType.TypeKind &&
                _structTypeName == otherType._structTypeName;
        }

        public override int GetHashCode()
        {
            int hashCode = PrimitiveType.GetHashCode() ^ DataType.GetHashCode() ^ TypeKind.GetHashCode();
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

        public string VariableNameHint()
        {
            if (PrimitiveType != Primitive.Struct)
            {
                return Enum.GetName(typeof(SpecialType), DataType).Replace("System_", "").ToLower();
            }
            else
            {
                return TypeName.ToLower().Replace(".", "_").ToLower();
            }
        }

        public override string ToString()
        {
            if (PrimitiveType != Primitive.Struct)
            {
                return Enum.GetName(typeof(Primitive), PrimitiveType);
            }
            else
            {
                return $"struct {_structTypeName}";
            }
        }
    }
}
