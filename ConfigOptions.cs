using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Antigen
{
    public class OptionsBase
    {
    }

    public class ConfigOptions : OptionsBase
    {
        public const string WeightSuffix = "Weight";

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

        public double LookupOperator(string str)
        {
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
