using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Generic;
using System.Linq;

namespace Antigen.Tree
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
    }

    public struct Operator
    {

        //public string Name => Oper.ToString();



        public OpFlags Flags;
        public Primitive InputTypes;
        public Primitive ReturnType;
        public SyntaxKind Oper;
        private readonly string renderText;
        private readonly string sampleOperation;

        public bool HasFlag(OpFlags flag)
        {
            bool val = (Flags & flag) != 0;
            return val;
        }

        public bool HasReturnType(Primitive valueType)
        {
            bool val = (ReturnType & valueType) != 0;
            return val;
        }

        private static readonly List<Operator> operators = new List<Operator>()
        {
            new Operator(SyntaxKind.UnaryPlusExpression,                "+",    "+i",  Primitive.Numeric, Primitive.Numeric,          OpFlags.Unary | OpFlags.Math),
            new Operator(SyntaxKind.UnaryMinusExpression,               "-",    "-i",  Primitive.Numeric, Primitive.Numeric,          OpFlags.Unary | OpFlags.Math),
            new Operator(SyntaxKind.PreIncrementExpression,             "++",   "++i", Primitive.Numeric | Primitive.Char, Primitive.Numeric| Primitive.Char,           OpFlags.Unary | OpFlags.Math | OpFlags.IncrementDecrement),
            new Operator(SyntaxKind.PreDecrementExpression,             "--",   "--i", Primitive.Numeric| Primitive.Char, Primitive.Numeric| Primitive.Char,           OpFlags.Unary | OpFlags.Math | OpFlags.IncrementDecrement),
            new Operator(SyntaxKind.PostIncrementExpression,            "++",   "i++", Primitive.Numeric| Primitive.Char, Primitive.Numeric| Primitive.Char,          OpFlags.Unary | OpFlags.Math | OpFlags.IncrementDecrement),
            new Operator(SyntaxKind.PostDecrementExpression,            "--",   "i--", Primitive.Numeric | Primitive.Char, Primitive.Numeric| Primitive.Char,          OpFlags.Unary | OpFlags.Math | OpFlags.IncrementDecrement),
            new Operator(SyntaxKind.LogicalNotExpression,               "!",    "!i",  Primitive.Boolean, Primitive.Boolean,         OpFlags.Unary | OpFlags.Logical),
            new Operator(SyntaxKind.BitwiseNotExpression,               "~",    "~i",  Primitive.Integer | Primitive.Char, Primitive.Integer,         OpFlags.Unary | OpFlags.Math | OpFlags.Bitwise),
            //new Operator(SyntaxKind.TypeOfExpression,        "typeof(i)",           OpFlags.Unary),

            new Operator(SyntaxKind.AddExpression,                      "+",    "i+j",   Primitive.Numeric, Primitive.Numeric,           OpFlags.Binary | OpFlags.Math | OpFlags.String),
            new Operator(SyntaxKind.AddExpression,                      "+",    "i concat j",   Primitive.String, Primitive.String,           OpFlags.Binary | OpFlags.String),
            new Operator(SyntaxKind.SubtractExpression,                 "-",    "i-j",  Primitive.Numeric, Primitive.Numeric,           OpFlags.Binary | OpFlags.Math),
            new Operator(SyntaxKind.MultiplyExpression,                 "*",    "i*j",  Primitive.Numeric, Primitive.Numeric,           OpFlags.Binary | OpFlags.Math),
            new Operator(SyntaxKind.DivideExpression,                   "/",    "i/j",   Primitive.Numeric, Primitive.Numeric,          OpFlags.Binary | OpFlags.Math | OpFlags.Divide),
            new Operator(SyntaxKind.ModuloExpression,                   "%",    "i%j",    Primitive.Numeric, Primitive.Numeric,        OpFlags.Binary | OpFlags.Math),
            new Operator(SyntaxKind.LeftShiftExpression,                "<<",   "i<<j", /*TODO-future: different for lhs, rhs*/  Primitive.SignedInteger | Primitive.Char, Primitive.SignedInteger,            OpFlags.Binary | OpFlags.Math | OpFlags.Shift),
            new Operator(SyntaxKind.RightShiftExpression,               ">>",   "i>>j",  Primitive.SignedInteger| Primitive.Char, Primitive.SignedInteger,            OpFlags.Binary | OpFlags.Math | OpFlags.Shift),

            new Operator(SyntaxKind.SimpleAssignmentExpression,         "=",      "i=j",  Primitive.Any | Primitive.Struct, Primitive.Any | Primitive.Struct,    OpFlags.Binary | OpFlags.Math | OpFlags.Assignment),
            new Operator(SyntaxKind.AddAssignmentExpression,            "+=",     "i+=j",  Primitive.Numeric | Primitive.String, Primitive.Numeric | Primitive.String,     OpFlags.Binary | OpFlags.Math | OpFlags.Assignment | OpFlags.String),
            new Operator(SyntaxKind.SubtractAssignmentExpression,       "-=",    "i-=j", Primitive.Numeric, Primitive.Numeric,     OpFlags.Binary | OpFlags.Math | OpFlags.Assignment),
            new Operator(SyntaxKind.MultiplyAssignmentExpression,       "*=",    "i*=j",  Primitive.Numeric, Primitive.Numeric,    OpFlags.Binary | OpFlags.Math | OpFlags.Assignment),
            new Operator(SyntaxKind.DivideAssignmentExpression,         "/=",    "i/=j", Primitive.Numeric, Primitive.Numeric,     OpFlags.Binary | OpFlags.Math | OpFlags.Divide | OpFlags.Assignment),
            new Operator(SyntaxKind.ModuloAssignmentExpression,         "%=",    "i%=j", Primitive.Numeric, Primitive.Numeric,    OpFlags.Binary | OpFlags.Math | OpFlags.Assignment),
            new Operator(SyntaxKind.LeftShiftAssignmentExpression,      "<<=",   "i<<=j", Primitive.Integer, Primitive.Integer,   OpFlags.Binary | OpFlags.Math | OpFlags.Shift | OpFlags.Assignment),
            new Operator(SyntaxKind.RightShiftAssignmentExpression,     ">>=",  "i>>=j", Primitive.Integer, Primitive.Integer,    OpFlags.Binary | OpFlags.Math | OpFlags.Shift | OpFlags.Assignment),

            new Operator(SyntaxKind.LogicalAndExpression,               "&&",   "i&&j", Primitive.Boolean,Primitive.Boolean,            OpFlags.Binary | OpFlags.Logical),
            new Operator(SyntaxKind.LogicalOrExpression,                "||",   "i||j",  Primitive.Boolean,Primitive.Boolean,           OpFlags.Binary | OpFlags.Logical),

            //TODO: below can also be logical as per https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/operators/boolean-logical-operators
            new Operator(SyntaxKind.BitwiseAndExpression,               "&",    "i&j",   Primitive.Integer | Primitive.Char, Primitive.Integer,           OpFlags.Binary | OpFlags.Math | OpFlags.Bitwise),
            new Operator(SyntaxKind.BitwiseOrExpression,                "|",    "i|j",    Primitive.Integer | Primitive.Char, Primitive.Integer,          OpFlags.Binary | OpFlags.Math | OpFlags.Bitwise),
            new Operator(SyntaxKind.ExclusiveOrExpression,              "^",    "i^j",    Primitive.Integer | Primitive.Char, Primitive.Integer,         OpFlags.Binary | OpFlags.Math | OpFlags.Bitwise),

            new Operator(SyntaxKind.AndAssignmentExpression,            "&=",   "i&=j",   Primitive.Integer,Primitive.Integer,         OpFlags.Binary | OpFlags.Math | OpFlags.Bitwise | OpFlags.Assignment),
            new Operator(SyntaxKind.OrAssignmentExpression,             "|=",   "i|=j",   Primitive.Integer,Primitive.Integer,        OpFlags.Binary | OpFlags.Math | OpFlags.Bitwise | OpFlags.Assignment),
            new Operator(SyntaxKind.ExclusiveOrAssignmentExpression,    "^=",   "i^=j", Primitive.Integer,Primitive.Integer,   OpFlags.Binary | OpFlags.Math | OpFlags.Bitwise | OpFlags.Assignment),

            new Operator(SyntaxKind.LessThanExpression,                 "<",    "i<j",    Primitive.Numeric  | Primitive.Char, Primitive.Boolean,         OpFlags.Binary | OpFlags.Comparison),
            new Operator(SyntaxKind.LessThanOrEqualExpression,          "<=",   "i<=j",  Primitive.Numeric | Primitive.Char, Primitive.Boolean,        OpFlags.Binary | OpFlags.Comparison),
            new Operator(SyntaxKind.GreaterThanExpression,              ">",    "i>j",    Primitive.Numeric | Primitive.Char, Primitive.Boolean,       OpFlags.Binary | OpFlags.Comparison),
            new Operator(SyntaxKind.GreaterThanOrEqualExpression,       ">=",   "i>=j", Primitive.Numeric | Primitive.Char, Primitive.Boolean,        OpFlags.Binary | OpFlags.Comparison),
            new Operator(SyntaxKind.EqualsExpression,                   "==",   "i==j",     Primitive.Any, Primitive.Boolean,         OpFlags.Binary | OpFlags.Comparison | OpFlags.String),
            new Operator(SyntaxKind.NotEqualsExpression,                "!=",   "i!=j",      Primitive.Any, Primitive.Boolean,         OpFlags.Binary | OpFlags.Comparison | OpFlags.String)
        };

        private Operator(SyntaxKind oper, string operatorText, string operation, Primitive inputTypes, Primitive outputType, OpFlags flags)
        {
            Oper = oper;
            renderText = operatorText;
            sampleOperation = operation;
            InputTypes = inputTypes;
            ReturnType = outputType;
            Flags = flags;
        }

        public static List<Operator> GetOperators()
        {
            return operators;
        }

        public static Operator ForSyntaxKind(SyntaxKind operKind)
        {
            return operators.First(o => o.Oper == operKind);
        }

        public override string ToString()
        {
            return renderText;
        }
    }
}
