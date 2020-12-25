using Antigen.Tree;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Reflection;

namespace Antigen
{
    public class OptionsBase
    {
    }

    public class ConfigOptions : OptionsBase
    {
        public const string WeightSuffix = "Weight";

        // Expression weights
        public double LiteralWeight = 0.25;
        public double VariableWeight = 0.5;
        public double BinaryOpWeight = 1;

        // Statement weights
        public double VariableDeclarationWeight = 0.5;
        public double IfElseStatementWeight = 1;
        public double AssignStatementWeight = 1;

        // Type weights
        public double BooleanWeight = 1;
        public double ByteWeight = 1;
        public double CharWeight = 1;
        public double DecimalWeight = 1;
        public double DoubleWeight = 1;
        public double Int16Weight = 1;
        public double Int32Weight = 1;
        public double Int64Weight = 1;
        public double SByteWeight = 1;
        public double SingleWeight = 1;
        public double StringWeight = 1;
        public double UInt16Weight = 1;
        public double UInt32Weight = 1;
        public double UInt64Weight = 1;

        // Operator weights
        public double UnaryPlusWeight = 1;
        public double UnaryMinusWeight = 1;
        public double PreIncrementWeight = 1;
        public double PreDecrementWeight = 1;
        public double PostIncrementWeight = 1;
        public double PostDecrementWeight = 1;
        public double LogicalNotWeight = 1;
        public double BitwiseNotWeight = 1;
        public double TypeOfWeight = 1;

        public double AddWeight = 1;
        public double SubtractWeight = 1;
        public double MultiplyWeight = 1;
        public double DivideWeight = 1;
        public double ModuloWeight = 1;
        public double LeftShiftWeight = 1;
        public double RightShiftWeight = 1;

        public double SimpleAssignmentWeight = 1;
        public double AddAssignmentWeight = 1;
        public double SubtractAssignmentWeight = 1;
        public double MultiplyAssignmentWeight = 1;
        public double DivideAssignmentWeight = 1;
        public double ModuloAssignmentWeight = 1;
        public double LeftShiftAssignmentWeight = 1;
        public double RightShiftAssignmentWeight = 1;

        public double LogicalAndWeight = 1;
        public double LogicalOrWeight = 1;

        public double BitwiseAndWeight = 1;
        public double BitwiseOrWeight = 1;
        public double ExclusiveOrWeight = 1;

        public double AndAssignmentWeight = 1;
        public double OrAssignmentWeight = 1;
        public double ExclusiveOrAssignmentWeight = 1;

        public double LessThanWeight = 1;
        public double LessThanOrEqualWeight = 1;
        public double GreaterThanWeight = 1;
        public double GreaterThanOrEqualWeight = 1;
        public double EqualsWeight = 1;
        public double NotEqualsWeight = 1;

        public double Lookup(Tree.ValueType type)
        {
            string str = Enum.GetName(typeof(Microsoft.CodeAnalysis.SpecialType), type.DataType);
            str = str.Replace("System_", "");
            return Lookup(str + WeightSuffix);
        }

        public double Lookup(Operator oper)
        {
            string str = Enum.GetName(typeof(SyntaxKind), oper.Oper);
            str = str.Replace("Expression", "");
            return Lookup(str + WeightSuffix);
        }

        public double Lookup(StmtKind stmt)
        {
            string str = Enum.GetName(typeof(StmtKind), stmt);
            return Lookup(str + WeightSuffix);
        }

        public double Lookup(ExprKind expr)
        {
            string str = Enum.GetName(typeof(ExprKind), expr);
            str = str.Replace("Expression", "");
            return Lookup(str + WeightSuffix);
        }

        private double Lookup(string str)
        {
            FieldInfo target = typeof(ConfigOptions).GetField(str, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

            if (target == null)
            {
                Console.WriteLine("ERROR: didn't find weight for {0}; using 0 instead", str);
                return 0;
            }

            return (double)target.GetValue(this);
        }
    }
}
