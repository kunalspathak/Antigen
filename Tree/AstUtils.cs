using System;
using System.Collections.Generic;
using System.Linq;

namespace Antigen.Tree
{
    public class AstUtils
    {
        private List<Weights<ExprKind>> AllExpressions = new List<Weights<ExprKind>>();
        private List<Weights<StmtKind>> AllStatements = new List<Weights<StmtKind>>();
        private List<Weights<ExprType>> AllTypes = new List<Weights<ExprType>>();
        private List<Weights<ExprType>> AllStatementsWithCFStmts = new List<Weights<ExprType>>();
        private List<Weights<ExprType>> AllTerminalExpressions = new List<Weights<ExprType>>();
        private List<Weights<ExprType>> AllTerminalStatements = new List<Weights<ExprType>>();
        private List<Weights<ExprType>> AllTerminalStatementsWithCFStmts = new List<Weights<ExprType>>();
        private List<Weights<Operator>> AllOperators = new List<Weights<Operator>>();

        private ConfigOptions Options;
        private RunOptions RunOptions;
        private TestCase TestCase;

        public AstUtils(TestCase tc, ConfigOptions configOptions, RunOptions runOptions)
        {
            Options = configOptions;
            RunOptions = runOptions;
            TestCase = tc;

            // Initialize types
            foreach (ExprType type in ExprType.GetTypes())
            {
                AllTypes.Add(new Weights<ExprType>(type, Options.Lookup(type)));
            }

            // Initialize statements
            foreach (StmtKind stmt in (StmtKind[])Enum.GetValues(typeof(StmtKind)))
            {
                AllStatements.Add(new Weights<StmtKind>(stmt, Options.Lookup(stmt)));
            }

            // Initialize expressions
            foreach (ExprKind expr in (ExprKind[])Enum.GetValues(typeof(ExprKind)))
            {
                AllExpressions.Add(new Weights<ExprKind>(expr, Options.Lookup(expr)));
            }

            // Initialize operators
            foreach (Operator oper in Operator.GetOperators())
            {
                AllOperators.Add(new Weights<Operator>(oper, Options.Lookup(oper)));
            }
        }

        #region Random type methods
        public ExprType GetRandomType()
        {
            // Select all appropriate statements
            IEnumerable<Weights<ExprType>> types =
                                        from z in AllTypes
                                        select z;

            // Do a weighted random choice.
            return PRNG.WeightedChoice(types);
        }
        #endregion

        #region Random statement methods

        public ExprKind GetRandomExpression()
        {
            // Select all appropriate statements
            IEnumerable<Weights<ExprKind>> exprs =
                                        from z in AllExpressions
                                        select z;

            // Do a weighted random choice.
            return PRNG.WeightedChoice(exprs);
        }

        #endregion

        #region Random statement methods

        public StmtKind GetRandomStatemet()
        {
            // Select all appropriate statements
            IEnumerable<Weights<StmtKind>> stmts =
                                        from z in AllStatements
                                        select z;

            // Do a weighted random choice.
            return PRNG.WeightedChoice(stmts);
        }

        #endregion

        #region Random operator methods

        public Operator GetRandomBinaryOperator()
        {
            // Select all appropriate operators
            IEnumerable<Weights<Operator>> ops =
                                        from z in AllOperators
                                        where z.Data.HasFlag(Operator.OpFlags.Binary) && !z.Data.HasFlag(Operator.OpFlags.Assignment)
                                        select z;

            // Do a weighted random choice.
            return PRNG.WeightedChoice(ops);
        }

        public Operator GetRandomComparisonOperator()
        {
            // Select all appropriate operators
            IEnumerable<Weights<Operator>> ops =
                                        from z in AllOperators
                                        where z.Data.HasFlag(Operator.OpFlags.Comparison)
                                        where z.Weight != 0
                                        select z;

            // If we've disabled all comparison operators, just use another binary operator.
            if (ops.Count() == 0)
            {
                return GetRandomBinaryOperator();
            }
            else
            {
                // Do a weighted random choice.
                return PRNG.WeightedChoice(ops);
            }
        }

        public Operator GetRandomLogicalOperator()
        {
            // Select all appropriate operators
            IEnumerable<Weights<Operator>> ops =
                                        from z in AllOperators
                                        where z.Data.HasFlag(Operator.OpFlags.Logical)
                                        where z.Weight != 0
                                        select z;

            // If we've disabled all comparison operators, just use another binary operator.
            if (ops.Count() == 0)
            {
                return GetRandomBinaryOperator();
            }
            else
            {
                // Do a weighted random choice.
                return PRNG.WeightedChoice(ops);
            }
        }

        public Operator GetRandomUnaryOperator()
        {
            // Select all appropriate operators
            IEnumerable<Weights<Operator>> ops =
                                        from z in AllOperators
                                        where z.Data.HasFlag(Operator.OpFlags.Unary)
                                        select z;

            // Do a weighted random choice.
            return PRNG.WeightedChoice(ops);
        }

        public Operator GetRandomAssignmentOperator()
        {
            // Select all appropriate operators
            IEnumerable<Weights<Operator>> ops =
                                        from z in AllOperators
                                        where z.Data.HasFlag(Operator.OpFlags.Assignment)
                                        select z;

            // Do a weighted random choice.
            return PRNG.WeightedChoice(ops);
        }

        public Operator GetRandomStringOperator()
        {
            // Select all appropriate operators
            IEnumerable<Weights<Operator>> ops =
                                        from z in AllOperators
                                        where z.Data.HasFlag(Operator.OpFlags.String)
                                        select z;

            // Do a weighted random choice.
            return PRNG.WeightedChoice(ops);
        }
        #endregion

    }
}
