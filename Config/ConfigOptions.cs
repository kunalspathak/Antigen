using Antigen.Tree;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Xml;

namespace Antigen.Config
{
    public class OptionsBase
    {
    }

    public class ConfigOptions : OptionsBase
    {
        public const string WeightSuffix = "Weight";

        // Expression weights
        public double LiteralWeight = 0.025;
        public double VariableWeight = 0.3;
        public double BinaryOpWeight = 1;
        public double MethodCallWeight = 0.23;
        public double AssignWeight = 0.4;

        // Statement weights
        public double VariableDeclarationWeight = 0.03; //TODO: Reduce this and add aliases when we see this.
        public double IfElseStatementWeight = 0.2;
        public double AssignStatementWeight = 0.6;
        public double ForStatementWeight = 0.3;
        public double DoWhileStatementWeight = 0.2;
        public double WhileStatementWeight = 0.3;
        public double TryCatchFinallyStatementWeight = 0.4;
        public double SwitchStatementWeight = 0.1;

        // Type weights
        public double BooleanWeight = 0.3;
        public double ByteWeight = 0.4;
        public double CharWeight = 0.03;
        public double DecimalWeight = 0.5;
        public double DoubleWeight = 0.4;
        public double Int16Weight = 0.4;
        public double Int32Weight = 0.45;
        public double Int64Weight = 0.48;
        public double SByteWeight = 0.3;
        public double SingleWeight = 0.6;
        public double StringWeight = 0.03;
        public double UInt16Weight = 0.4;
        public double UInt32Weight = 0.45;
        public double UInt64Weight = 0.6;

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

        public double AddWeight = 0.9;
        public double SubtractWeight = 0.5;
        public double MultiplyWeight = 0.5;
        public double DivideWeight = 0.4;
        public double ModuloWeight = 0.5;
        public double LeftShiftWeight = 0.6;
        public double RightShiftWeight = 0.4;

        public double SimpleAssignmentWeight = 0.9;
        public double AddAssignmentWeight = 0.5;
        public double SubtractAssignmentWeight = 0.6;
        public double MultiplyAssignmentWeight = 0.5;
        public double DivideAssignmentWeight = 0.5;
        public double ModuloAssignmentWeight = 0.3;
        public double LeftShiftAssignmentWeight = 0.5;
        public double RightShiftAssignmentWeight = 0.6;

        public double LogicalAndWeight = 0.5;
        public double LogicalOrWeight = 0.4;

        public double BitwiseAndWeight = 0.45;
        public double BitwiseOrWeight = 0.35;
        public double ExclusiveOrWeight = 0.45;

        public double AndAssignmentWeight = 0.54;
        public double OrAssignmentWeight = 0.59;
        public double ExclusiveOrAssignmentWeight = 0.68;

        public double LessThanWeight = 0.6;
        public double LessThanOrEqualWeight = 0.6;
        public double GreaterThanWeight = 0.51;
        public double GreaterThanOrEqualWeight = 0.51;
        public double EqualsWeight = 0.8;
        public double NotEqualsWeight = 0.8;

        // Config options
        // Probablity of removing loop parameters -- see comments on BoundParameters in ForStatement 
        public double LoopParametersRemovalProbability = 0.1;

        // Probability of having forward loop whose induction variable always increases
        public double LoopForwardProbability = 0.7;

        //Probabilty whether loop induction variable should start from loop invariant value or +/- 3
        public double LoopStartFromInvariantProbabilty = 0.5;

        //Probabilty whether loop step should happen pre or post break condition
        public double LoopStepPreBreakCondProbability = 0.5;

        // Probability of usage of array.length vs. loopinvariant
        public double UseLoopInvariantVariableProbability = 1.0; // Always have 1.0 for now to stop making .length as invariant because that could lead to long loops

        // This will put loop condition at the end of the loop body. 
        // Always use ContinueStatementWeight = 0 if this is true (see lessmath_no_continue.xml), otherwise there is a chance of infinite loop here.
        // This is a quick fix for now.  We need to come up with a better solution for this for IE11.
        public bool AllowLoopCondAtEnd = false;

        // number of testcases to create
        public long NumTestCases = 1;

        // max number of statements in a block
        public int MaxStatements = 5;

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
