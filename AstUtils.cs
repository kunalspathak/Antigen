using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Antigen
{
    public class AstUtils
    {
        private List<Weights<Type>> AllExpressions = new List<Weights<Type>>();
        private List<Weights<Type>> AllStatements = new List<Weights<Type>>();
        private List<Weights<Type>> AllStatementsWithCFStmts = new List<Weights<Type>>();
        private List<Weights<Type>> AllTerminalExpressions = new List<Weights<Type>>();
        private List<Weights<Type>> AllTerminalStatements = new List<Weights<Type>>();
        private List<Weights<Type>> AllTerminalStatementsWithCFStmts = new List<Weights<Type>>();
        private List<Weights<Operator>> AllOperators = new List<Weights<Operator>>();

        private ConfigOptions Options;
        private RunOptions RunOptions;
        private TestCase TestCase;
        private static char[] Alphabet = new char[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z' };

        public AstUtils(TestCase tc, ConfigOptions configOptions, RunOptions runOptions)
        {
            Options = configOptions;
            RunOptions = runOptions;
            TestCase = tc;

            // Initialize operators
            foreach (Operator op in Operator.GetOperators())
            {
                AllOperators.Add(new Weights<Operator>(op, Options.LookupOperator(op.Name)));
            }
        }
    }
}
