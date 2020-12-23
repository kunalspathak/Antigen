using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Generic;

namespace Antigen.Tree
{
    public enum TypeFlags
    {
        Numeric = 0x1,
        Integer = 0x2,
        Decimal = 0x4,
        Char = 0x8,
        String = 0x10,
        Bool = 0x20,
    }

    public enum ValueType
    {
        Boolean,
        Byte,
        Char,
        Decimal,
        Double,
        Int16,
        Int32,
        Int64,
        SByte,
        Single,
        String,
        UInt16,
        UInt32,
        UInt64,
    }

    public struct ExprType
    {
        // TODO: Matrix about implicit and explicit cast

        public bool HasFlag(TypeFlags flag)
        {
            bool val = (Flags & flag) != 0;
            return val;
        }

        public ValueType ValueType;
        private TypeFlags Flags;
        public SpecialType DataType;
        public SyntaxKind TypeKind;

        private static readonly List<ExprType> types = new List<ExprType>()
        {
            new ExprType(ValueType.Boolean, SpecialType.System_Boolean,    SyntaxKind.BoolKeyword,     TypeFlags.Bool),
            new ExprType(ValueType.Byte, SpecialType.System_Byte,       SyntaxKind.ByteKeyword,     TypeFlags.Numeric | TypeFlags.Integer),
            new ExprType(ValueType.Char, SpecialType.System_Char,       SyntaxKind.CharKeyword,     TypeFlags.Char),
            new ExprType(ValueType.Decimal, SpecialType.System_Decimal,    SyntaxKind.DecimalKeyword,  TypeFlags.Numeric | TypeFlags.Decimal),
            new ExprType(ValueType.Double, SpecialType.System_Double,     SyntaxKind.DoubleKeyword,   TypeFlags.Numeric | TypeFlags.Decimal),
            new ExprType(ValueType.Int16, SpecialType.System_Int16,      SyntaxKind.ShortKeyword,    TypeFlags.Numeric | TypeFlags.Integer),
            new ExprType(ValueType.Int32, SpecialType.System_Int32,      SyntaxKind.IntKeyword,      TypeFlags.Numeric | TypeFlags.Integer),
            new ExprType(ValueType.Int64, SpecialType.System_Int64,      SyntaxKind.LongKeyword,     TypeFlags.Numeric | TypeFlags.Integer),
            new ExprType(ValueType.SByte, SpecialType.System_SByte,      SyntaxKind.SByteKeyword,    TypeFlags.Numeric | TypeFlags.Integer),
            new ExprType(ValueType.Single, SpecialType.System_Single,     SyntaxKind.FloatKeyword,    TypeFlags.Numeric | TypeFlags.Decimal),
            new ExprType(ValueType.String, SpecialType.System_String,     SyntaxKind.StringKeyword,   TypeFlags.String),
            new ExprType(ValueType.UInt16, SpecialType.System_UInt16,     SyntaxKind.UShortKeyword,   TypeFlags.Numeric | TypeFlags.Integer),
            new ExprType(ValueType.UInt32, SpecialType.System_UInt32,     SyntaxKind.UIntKeyword,     TypeFlags.Numeric | TypeFlags.Integer),
            new ExprType(ValueType.UInt64, SpecialType.System_UInt64,     SyntaxKind.ULongKeyword,    TypeFlags.Numeric | TypeFlags.Integer),
        };

        private ExprType (ValueType valueType, SpecialType dataType, SyntaxKind typeKind, TypeFlags typeFlags)
        {
            ValueType = valueType;
            DataType = dataType;
            TypeKind = typeKind;
            Flags = typeFlags;
        }

        public static List<ExprType> GetTypes()
        {
            return types;
        }
    }
}
