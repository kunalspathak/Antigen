using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;

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
        Int16 = 0x20,
        Int32 = 0x40,
        Int64 = 0x80,
        SByte = 0x100,
        Single = 0x200,
        String = 0x400,
        UInt16 = 0x800,
        UInt32 = 0x1000,
        UInt64 = 0x2000,

        Numeric = Byte | Decimal | Double | Int16 | Int32 | Int64 | SByte | Single | UInt16 | UInt32 | UInt64,
        SignedInteger = SByte | Int16 | Int32 | Int64,
        UnsignedInteger = Byte | UInt16 | UInt32 | UInt64,
        Integer = SignedInteger | UnsignedInteger,
        FloatingPoint = Single | Double | Decimal,
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
            new ValueType(Primitive.Int16, SpecialType.System_Int16,      SyntaxKind.ShortKeyword),
            new ValueType(Primitive.Int32, SpecialType.System_Int32,      SyntaxKind.IntKeyword),
            new ValueType(Primitive.Int64, SpecialType.System_Int64,      SyntaxKind.LongKeyword),
            new ValueType(Primitive.SByte, SpecialType.System_SByte,      SyntaxKind.SByteKeyword),
            new ValueType(Primitive.Single, SpecialType.System_Single,     SyntaxKind.FloatKeyword),
            new ValueType(Primitive.String, SpecialType.System_String,     SyntaxKind.StringKeyword),
            new ValueType(Primitive.UInt16, SpecialType.System_UInt16,     SyntaxKind.UShortKeyword),
            new ValueType(Primitive.UInt32, SpecialType.System_UInt32,     SyntaxKind.UIntKeyword),
            new ValueType(Primitive.UInt64, SpecialType.System_UInt64,     SyntaxKind.ULongKeyword),
        };

        private ValueType(Primitive valueType, SpecialType dataType, SyntaxKind typeKind)
        {
            PrimitiveType = valueType;
            DataType = dataType;
            TypeKind = typeKind;
        }

        public static List<ValueType> GetTypes()
        {
            return types;
        }

        public override string ToString()
        {
            return Enum.GetName(typeof(Primitive), PrimitiveType);
        }
    }
}
