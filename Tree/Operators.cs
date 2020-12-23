using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Generic;

namespace Antigen.Tree
{
    public struct Operator
    {
        public enum OpFlags
        {
            Comparison = 0x1,
            Binary = 0x2,
            Unary = 0x4,
            Divide = 0x8,
            Shift = 0x10,
            IncrementDecrement = 0x20,
            Math = 0x40,
            Assignment = 0x80,
            Logical = 0x100,
            Bitwise = 0x200,
            String = 0x400,
            Yield = 0x800
        }

        //public string Name => Oper.ToString();

        public OpFlags Flags;
        public SyntaxKind Oper;

        public bool HasFlag(OpFlags flag)
        {
            bool val = (Flags & flag) != 0;
            return val;
        }

        private static readonly List<Operator> operators = new List<Operator>()
        {
            new Operator(SyntaxKind.UnaryPlusExpression,                OpFlags.Unary | OpFlags.Math),
            new Operator(SyntaxKind.UnaryMinusExpression,               OpFlags.Unary | OpFlags.Math),
            new Operator(SyntaxKind.PreIncrementExpression,             OpFlags.Unary | OpFlags.Math | OpFlags.IncrementDecrement),
            new Operator(SyntaxKind.PreDecrementExpression,             OpFlags.Unary | OpFlags.Math | OpFlags.IncrementDecrement),
            new Operator(SyntaxKind.PostIncrementExpression,            OpFlags.Unary | OpFlags.Math | OpFlags.IncrementDecrement),
            new Operator(SyntaxKind.PostDecrementExpression,            OpFlags.Unary | OpFlags.Math | OpFlags.IncrementDecrement),
            new Operator(SyntaxKind.LogicalNotExpression,               OpFlags.Unary),
            new Operator(SyntaxKind.BitwiseNotExpression,               OpFlags.Unary | OpFlags.Math | OpFlags.Bitwise),
            new Operator(SyntaxKind.TypeOfExpression,                   OpFlags.Unary),

            new Operator(SyntaxKind.AddExpression,                      OpFlags.Binary | OpFlags.Math | OpFlags.String),
            new Operator(SyntaxKind.SubtractExpression,                 OpFlags.Binary | OpFlags.Math),
            new Operator(SyntaxKind.MultiplyExpression,                 OpFlags.Binary | OpFlags.Math),
            new Operator(SyntaxKind.DivideExpression,                   OpFlags.Binary | OpFlags.Math | OpFlags.Divide),
            new Operator(SyntaxKind.ModuloExpression,                   OpFlags.Binary | OpFlags.Math),
            new Operator(SyntaxKind.LeftShiftExpression,                OpFlags.Binary | OpFlags.Math | OpFlags.Shift),
            new Operator(SyntaxKind.RightShiftExpression,               OpFlags.Binary | OpFlags.Math | OpFlags.Shift),

            new Operator(SyntaxKind.SimpleAssignmentExpression,         OpFlags.Binary | OpFlags.Math | OpFlags.Assignment),
            new Operator(SyntaxKind.AddAssignmentExpression,            OpFlags.Binary | OpFlags.Math | OpFlags.Assignment | OpFlags.String),
            new Operator(SyntaxKind.SubtractAssignmentExpression,       OpFlags.Binary | OpFlags.Math | OpFlags.Assignment),
            new Operator(SyntaxKind.MultiplyAssignmentExpression,       OpFlags.Binary | OpFlags.Math | OpFlags.Assignment),
            new Operator(SyntaxKind.DivideAssignmentExpression,         OpFlags.Binary | OpFlags.Math | OpFlags.Divide | OpFlags.Assignment),
            new Operator(SyntaxKind.ModuloAssignmentExpression,         OpFlags.Binary | OpFlags.Math | OpFlags.Assignment),
            new Operator(SyntaxKind.LeftShiftAssignmentExpression,      OpFlags.Binary | OpFlags.Math | OpFlags.Shift | OpFlags.Assignment),
            new Operator(SyntaxKind.RightShiftAssignmentExpression,     OpFlags.Binary | OpFlags.Math | OpFlags.Shift | OpFlags.Assignment),

            new Operator(SyntaxKind.LogicalAndExpression,               OpFlags.Binary | OpFlags.Logical),
            new Operator(SyntaxKind.LogicalOrExpression,                OpFlags.Binary | OpFlags.Logical),

            new Operator(SyntaxKind.BitwiseAndExpression,               OpFlags.Binary | OpFlags.Math | OpFlags.Bitwise),
            new Operator(SyntaxKind.BitwiseOrExpression,                OpFlags.Binary | OpFlags.Math | OpFlags.Bitwise),
            new Operator(SyntaxKind.ExclusiveOrExpression,              OpFlags.Binary | OpFlags.Math | OpFlags.Bitwise),
                
            new Operator(SyntaxKind.AndAssignmentExpression,            OpFlags.Binary | OpFlags.Math | OpFlags.Bitwise | OpFlags.Assignment),
            new Operator(SyntaxKind.OrAssignmentExpression,             OpFlags.Binary | OpFlags.Math | OpFlags.Bitwise | OpFlags.Assignment),
            new Operator(SyntaxKind.ExclusiveOrAssignmentExpression,    OpFlags.Binary | OpFlags.Math | OpFlags.Bitwise | OpFlags.Assignment),

            new Operator(SyntaxKind.LessThanExpression,                 OpFlags.Binary | OpFlags.Comparison),
            new Operator(SyntaxKind.LessThanOrEqualExpression,          OpFlags.Binary | OpFlags.Comparison),
            new Operator(SyntaxKind.GreaterThanExpression,              OpFlags.Binary | OpFlags.Comparison),
            new Operator(SyntaxKind.GreaterThanOrEqualExpression,       OpFlags.Binary | OpFlags.Comparison),
            new Operator(SyntaxKind.EqualsExpression,                   OpFlags.Binary | OpFlags.Comparison | OpFlags.String),
            new Operator(SyntaxKind.NotEqualsExpression,                OpFlags.Binary | OpFlags.Comparison | OpFlags.String)
        };

        private Operator(SyntaxKind oper, OpFlags flags)
        {
            Oper = oper;
            Flags = flags;
        }

        public static List<Operator> GetOperators()
        {
            return operators;
        }
    }
}