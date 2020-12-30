using System;
using System.Collections.Generic;
using System.Linq;
using Antigen.Statements;

namespace Antigen.Tree
{
    public class AstUtils
    {
        private List<Weights<ExprKind>> AllExpressions = new List<Weights<ExprKind>>();
        private List<Weights<ExprKind>> AllNonNumericExpressions = new List<Weights<ExprKind>>();
        private List<Weights<ExprKind>> AllStructExpressions = new List<Weights<ExprKind>>();
        private List<Weights<StmtKind>> AllStatements = new List<Weights<StmtKind>>();
        private List<Weights<ValueType>> AllTypes = new List<Weights<ValueType>>();
        private List<Weights<ValueType>> AllStatementsWithCFStmts = new List<Weights<ValueType>>();
        private List<Weights<ValueType>> AllTerminalExpressions = new List<Weights<ValueType>>();
        private List<Weights<ValueType>> AllTerminalStatements = new List<Weights<ValueType>>();
        private List<Weights<ValueType>> AllTerminalStatementsWithCFStmts = new List<Weights<ValueType>>();
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
            foreach (ValueType type in ValueType.GetTypes())
            {
                AllTypes.Add(new Weights<ValueType>(type, Options.Lookup(type)));
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
                // For binary operation, there is no operator that don't have assign flag and that returns char or string
                // Hence do not choose binary expression if return is expected to be string
                if (expr != ExprKind.BinaryOpExpression)
                {
                    AllNonNumericExpressions.Add(new Weights<ExprKind>(expr, Options.Lookup(expr)));
                }

                if (expr == ExprKind.VariableExpression)
                {
                    AllStructExpressions.Add(new Weights<ExprKind>(expr, Options.Lookup(expr)));
                }
            }

            // Initialize operators
            foreach (Operator oper in Operator.GetOperators())
            {
                AllOperators.Add(new Weights<Operator>(oper, Options.Lookup(oper)));
            }
        }

        #region Random type methods
        public ValueType GetRandomExprType()
        {
            // Select all appropriate types
            IEnumerable<Weights<ValueType>> types =
                                        from z in AllTypes
                                        select z;

            // Do a weighted random choice.
            return PRNG.WeightedChoice(types);
        }

        public ValueType GetRandomExprType(Primitive valueType)
        {
            // Select all appropriate types
            IEnumerable<Weights<ValueType>> types =
                                        from z in AllTypes
                                        where z.Data.AllowedPrimitive(valueType)
                                        select z;

            // Do a weighted random choice.
            return PRNG.WeightedChoice(types);
        }

        #endregion

        #region Random expression methods

        public ExprKind GetRandomExpression()
        {
            IEnumerable<Weights<ExprKind>> exprs =
                from z in AllExpressions
                select z;

            // Do a weighted random choice.
            return PRNG.WeightedChoice(exprs);
        }

        public ExprKind GetRandomExpressionReturningPrimitive(Primitive returnPrimitiveType)
        {
            IEnumerable<Weights<ExprKind>> exprs;
            // Select all appropriate expressions
            if (returnPrimitiveType == Primitive.Char)
            {
                exprs = from z in AllNonNumericExpressions
                        select z;
            }
            else if (returnPrimitiveType == Primitive.Struct)
            {
                exprs = from z in AllStructExpressions
                        select z;
            }
            else
            {
                exprs = from z in AllExpressions
                        select z;
            }

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

        public Operator GetRandomBinaryOperator(Primitive returnPrimitiveType)
        {
            // Select all appropriate operators
            IEnumerable<Weights<Operator>> ops =
                                        from z in AllOperators
                                        where z.Data.HasFlag(OpFlags.Binary) && !z.Data.HasFlag(OpFlags.Assignment) && z.Data.HasReturnType(returnPrimitiveType)
                                        select z;

            // Do a weighted random choice.
            return PRNG.WeightedChoice(ops);
        }

        public Operator GetRandomComparisonOperator()
        {
            // Select all appropriate operators
            IEnumerable<Weights<Operator>> ops =
                                        from z in AllOperators
                                        where z.Data.HasFlag(OpFlags.Comparison)
                                        where z.Weight != 0
                                        select z;

            // Do a weighted random choice.
            return PRNG.WeightedChoice(ops);
        }

        public Operator GetRandomLogicalOperator()
        {
            // Select all appropriate operators
            IEnumerable<Weights<Operator>> ops =
                                        from z in AllOperators
                                        where z.Data.HasFlag(OpFlags.Logical)
                                        where z.Weight != 0
                                        select z;

            // Do a weighted random choice.
            return PRNG.WeightedChoice(ops);
        }

        public Operator GetRandomUnaryOperator()
        {
            // Select all appropriate operators
            IEnumerable<Weights<Operator>> ops =
                                        from z in AllOperators
                                        where z.Data.HasFlag(OpFlags.Unary)
                                        select z;

            // Do a weighted random choice.
            return PRNG.WeightedChoice(ops);
        }

        public Operator GetRandomAssignmentOperator()
        {
            // Select all appropriate operators
            IEnumerable<Weights<Operator>> ops =
                                        from z in AllOperators
                                        where z.Data.HasFlag(OpFlags.Assignment)
                                        select z;

            // Do a weighted random choice.
            return PRNG.WeightedChoice(ops);
        }

        public Operator GetRandomStringOperator()
        {
            // Select all appropriate operators
            IEnumerable<Weights<Operator>> ops =
                                        from z in AllOperators
                                        where z.Data.HasFlag(OpFlags.String)
                                        select z;

            // Do a weighted random choice.
            return PRNG.WeightedChoice(ops);
        }

        internal LoopControlParameters GetForBoundParameters()
        {
            LoopControlParameters Ret = new LoopControlParameters();

            Ret.IsInitInLoopHeader = PRNG.Decide(Options.LoopParametersRemovalProbability);
            Ret.IsStepInLoopHeader = PRNG.Decide(Options.LoopParametersRemovalProbability);
            Ret.IsBreakCondInLoopHeader = PRNG.Decide(Options.LoopParametersRemovalProbability);

            Ret.IsLoopInvariantVariableUsed = PRNG.Decide(Options.UseLoopInvariantVariableProbability);
            Ret.IsBreakCondAtEndOfLoopBody = Options.AllowLoopCondAtEnd ? PRNG.Decide(0.5) : false;
            Ret.IsForwardLoop = PRNG.Decide(Options.LoopForwardProbability);
            Ret.IsLoopStartFromInvariant = PRNG.Decide(Options.LoopStartFromInvariantProbabilty);
            Ret.LoopInductionChangeFactor = PRNG.Next(1, 5);
            Ret.LoopInitValueVariation = PRNG.Next(0, Ret.LoopInductionChangeFactor);
            Ret.IsStepBeforeBreakCondition = PRNG.Decide(Options.LoopStepPreBreakCondProbability);

            // Generate operator for break condition in a forward loop
            // Depending on the loop type/condition, we will later flip the operator in LoopStatement
            int operatorChoice = PRNG.Next(5);
            switch (operatorChoice)
            {
                case 0:
                case 1:
                    Ret.LoopBreakOperator = Operator.ForSyntaxKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.GreaterThanExpression);
                    break;
                case 2:
                case 3:
                    Ret.LoopBreakOperator = Operator.ForSyntaxKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.GreaterThanOrEqualExpression);
                    break;
                case 4:
                    Ret.LoopBreakOperator = Operator.ForSyntaxKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.EqualsExpression);
                    // variation doesn't guarantee rounding which is needed with == operator. So make invariant as 0.
                    // eg. ChangeFactor = 2, __loopvar = 3; __loopvar != (3 * 2); __loopvar += 2; We will break in 3 iterations without invariation
                    // eg. ChangeFactor = 2, __loopvar = 3 + 1; __loopvar != (3 * 2); __loopvar += 2; We will go to infinite loop because condition is never true
                    Ret.LoopInitValueVariation = 0;
                    break;
            }
            return Ret;
        }
        #endregion

    }
}
