﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Antigen.Tree;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Antigen.Statements
{
    // This struct indicates which parameters of the for loop are removed.
    // Note: We only remove __loopvars. In NormalLoops and ComplexLoops, we will still have other 
    // statements as loop conditions and increments.
    public class LoopControlParameters
    {
        public Operator LoopBreakOperator;

        /// <summary>
        /// Valid for ForStatement only
        /// true - IV is initialized inside loop head. for(init; cond; incr)
        /// false - IV is initialized outside loop head. for (; cond; incr)
        /// </summary>
        public bool IsInitInLoopHeader;

        /// <summary>
        /// true - IV's guard condition is inside loop head. for(init; cond; incr)
        /// false - IV's guard condition is outside loop head. for (init; ; incr) or while(expr) { if(cond) break; ...}
        /// </summary>
        public bool IsBreakCondInLoopHeader;

        /// <summary>
        /// Valid for ForStatement only
        /// true - IV's step expression is inside loop head. for(init; cond; incr)
        /// false - IV's step expression is outside loop head. for (init; cond; ) { incr; ...}
        /// </summary>
        public bool IsStepInLoopHeader;

        /// <summary>
        /// true - IV's break condition is at the end of loop body
        /// false - IV's break condition is at the beginning of loop body
        /// </summary>
        public bool IsBreakCondAtEndOfLoopBody;

        /// <summary>
        /// true - IV is incremented pre break condition e.g. ... incr; if(cond) break;
        /// false - IV is incremented post break condition e.g. ... if(cond) break; incr; 
        /// </summary>
        public bool IsStepBeforeBreakCondition;

        /// <summary>
        /// true - loop step increases IV in step expression. i++
        /// false - loop step decreases IV in step expression. i--
        /// </summary>
        public bool IsForwardLoop;

        /// <summary>
        /// Determines loop change factor.if 5 then IV += 5; 
        /// </summary>
        public int LoopInductionChangeFactor;

        /// <summary>
        /// true - loopInvariant is used for loopStart and loopEnd
        /// false - array.length is used for loopStart and loopEnd
        /// </summary>
        public bool IsLoopInvariantVariableUsed;

        /// <summary>
        /// true - Initialize IV = loopInvariant
        /// false - Initialize IV = loopInvariant +/- 3
        /// </summary>
        public bool IsLoopStartFromInvariant;

        /// <summary>
        /// Variation to initialized value of loopvar. 
        /// e.g.g If this is ForwardLoop, ChangeFactor=3 and break condition is 12, then there are 3 possible initialization values for loopvar:
        /// 3,6,9 
        /// 4,7,10
        /// 5,8,11
        /// </summary>
        public int LoopInitValueVariation;
    }

    public class InductionVariable
    {
        private static int NumOfIterations = 3;
        public bool IsPrimary;
        public string Name;
        public LoopControlParameters LoopParameters;

        public bool isLoopInitGenerated = false;
        public bool isLoopStepGenerated = false;
        public bool isLoopBreakGenerated = false;
        public string LoopInvariantName = null;

        // Variables used in ToString() method
        private int __loopStart = -1;
        private int __loopEnd = -1;


        #region Methods

        public override string ToString()
        {
            if (__loopStart == -1) GetLoopStart();
            if (__loopEnd == -1) GetLoopEnd();

            if (IsPrimary)
            {
                if (LoopParameters.IsStepBeforeBreakCondition)
                    return String.Format("({0} = {1} ; {4}, {0} {2} {3}; )", Name, __loopStart, getLoopControlOperator(true), __loopEnd, GetLoopStep());
                else
                    return String.Format("({0} = {1} ; {0} {2} {3}; {4})", Name, __loopStart, getLoopControlOperator(true), __loopEnd, GetLoopStep());
            }
            else
            {
                return String.Format("({0} = {1} ; ; {2})", Name, __loopStart, GetLoopStep());
            }
        }

        /// <summary>
        /// Returns if this induction variable had generated code for init, step and guard check.
        /// </summary>
        /// <returns></returns>
        public bool HasSuccessfullyGenerated()
        {
            if (IsPrimary)
            {
                return isLoopInitGenerated && isLoopStepGenerated && isLoopBreakGenerated;
            }
            else
            {
                return isLoopInitGenerated && isLoopStepGenerated;
            }
        }

        /// <summary>
        /// Returns value of loopinduction variable with which it should start the loop
        /// </summary>
        /// <returns></returns>
        internal ExpressionSyntax GetLoopStart()
        {
            __loopStart = 0;
            // If loop starts with invariant then _loopvar = loopInvariant;
            if (LoopParameters.IsLoopStartFromInvariant)
                return IdentifierName(LoopInvariantName);

            int loopStartValue = (InductionVariable.NumOfIterations * LoopParameters.LoopInductionChangeFactor);

            if (LoopParameters.IsForwardLoop)
                loopStartValue += LoopParameters.LoopInitValueVariation;
            else
                loopStartValue -= LoopParameters.LoopInitValueVariation;

            __loopStart = (LoopParameters.IsForwardLoop ? -1 : 1) * loopStartValue;

            return BinaryExpression(
                LoopParameters.IsForwardLoop ? SyntaxKind.SubtractExpression : SyntaxKind.AddExpression,
                IdentifierName(LoopInvariantName),
                LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(loopStartValue)));
            //return String.Format("{0} {1} {2}", LoopInvariantName, LoopParameters.IsForwardLoop ? "-" : "+", loopStartValue.ToString());
        }

        /// <summary>
        /// Returns value of loopinduction variable with which it should end the loop
        /// </summary>
        /// <returns></returns>
        internal ExpressionSyntax GetLoopEnd()
        {
            int numOfIterations = InductionVariable.NumOfIterations;
            __loopEnd = 0;
            // If loop doesn't start with invariant, it ends with loopInvariant : __loopvar = loopInvariant - N; __loopvar < loopInvariant;
            if (!LoopParameters.IsLoopStartFromInvariant)
                numOfIterations = 0;

            // If we are incr/decr before break condition, we should compente with increasing the break bounds
            if (LoopParameters.IsStepBeforeBreakCondition)
                numOfIterations++;

            // If we have check with <= , we will loop numOfIterations + 1 times, so reduce the numOfIterations by 1
            if (LoopParameters.LoopBreakOperator.Equals(">="))
                numOfIterations--;

            int loopEndValue = numOfIterations * LoopParameters.LoopInductionChangeFactor;
            __loopEnd = (LoopParameters.IsForwardLoop ? 1 : -1) * loopEndValue;

            if (loopEndValue == 0)
                return IdentifierName(LoopInvariantName);
            else
                return BinaryExpression(
                    LoopParameters.IsForwardLoop ? SyntaxKind.AddExpression : SyntaxKind.SubtractExpression,
                    IdentifierName(LoopInvariantName),
                    LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(loopEndValue)));
            //return String.Format("{0} {1} {2}", LoopInvariantName, LoopParameters.IsForwardLoop ? "+" : "-", loopEndValue.ToString());
        }

        /// <summary>
        /// Returns the loop step code for this induction variable. Since this can be used in places other 
        /// than loop control statements, call this only for secondary induction variables.
        /// </summary>
        /// <returns></returns>
        internal ExpressionSyntax GetLoopStepForSecondaryIV()
        {
            Debug.Assert(!IsPrimary, "Try to get loop step for primary induction variable.");
            int selectedChangeFactor = GetRandomInductionChangeFactor();
            if (selectedChangeFactor == 1)
            {
                return PostfixUnaryExpression(
                    LoopParameters.IsForwardLoop ? SyntaxKind.PostIncrementExpression : SyntaxKind.PostDecrementExpression,
                    IdentifierName(Name));
                //return Name + (LoopParameters.IsForwardLoop ? "++" : "--") + ";";
            }
            else
            {
                return AssignmentExpression(
                    LoopParameters.IsForwardLoop ? SyntaxKind.AddAssignmentExpression : SyntaxKind.SubtractAssignmentExpression,
                    IdentifierName(Name),
                    LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(selectedChangeFactor)));
                //return String.Format("{0} {1}= {2};", Name, LoopParameters.IsForwardLoop ? "+" : "-", selectedChangeFactor);
            }
        }

        private int GetRandomInductionChangeFactor()
        {
            if (PRNG.Decide(0.05))
                return 0;
            if (LoopParameters.LoopInductionChangeFactor == 1)
                return 1;
            else
                return PRNG.Next(1, LoopParameters.LoopInductionChangeFactor);
        }

        /// <summary>
        /// Returns the step code for given induction variable
        /// </summary>
        /// <param name="inductionVariable"></param>
        /// <returns></returns>
        internal ExpressionSyntax GetLoopStep()
        {
            if (LoopParameters.LoopInductionChangeFactor == 1)
            {
                return PostfixUnaryExpression(
                    LoopParameters.IsForwardLoop ? SyntaxKind.PostIncrementExpression : SyntaxKind.PostDecrementExpression,
                    IdentifierName(Name));
                //return Name + (LoopParameters.IsForwardLoop ? "++" : "--") + ";";
            }
            else
            {
                return AssignmentExpression(
                    LoopParameters.IsForwardLoop ? SyntaxKind.AddAssignmentExpression : SyntaxKind.SubtractAssignmentExpression,
                    IdentifierName(Name),
                    LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(LoopParameters.LoopInductionChangeFactor)));
                //return String.Format("{0} {1}= {2};", Name, LoopParameters.IsForwardLoop ? "+" : "-", LoopParameters.LoopInductionChangeFactor);
            }
        }


        /// <summary>
        /// Returns the loop IV access code +/- change factor
        /// </summary>
        /// <returns></returns>
        internal ExpressionSyntax GetLoopIVAccess()
        {
            // 5% of time generate '0' for change factor
            return BinaryExpression(
                    LoopParameters.IsForwardLoop ? SyntaxKind.AddExpression : SyntaxKind.SubtractExpression,
                    IdentifierName(Name),
                    LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(GetRandomInductionChangeFactor())));

            //return String.Format("{0} {1} {2}", Name, LoopParameters.IsForwardLoop ? "+" : "-", GetRandomInductionChangeFactor());
        }

        /// <summary>
        /// Returns the guard condition for the loop. Loop will execute as long as this condition is true.
        /// </summary>
        /// <returns></returns>
        internal ExpressionSyntax GetLoopGuardCondition()
        {
            return BinaryExpression(getLoopControlOperator(true).Oper, IdentifierName(Name), GetLoopEnd());
            //return String.Format("({0} {1} {2})", Name, getLoopControlOperator(true), GetLoopEnd());
        }

        /// <summary>
        /// Returns the break condition for the loop. Loop will terminate when this condition is true.
        /// </summary>
        /// <returns></returns>
        internal ExpressionSyntax GetLoopBreakCondition()
        {
            return BinaryExpression(getLoopControlOperator(false).Oper, IdentifierName(Name), GetLoopEnd());
            //return String.Format("({0} {1} {2})", Name, getLoopControlOperator(false), GetLoopEnd());
        }

        /// <summary>
        /// Get the operator required to control the loop based on direction of the loop and guard/break condition
        /// </summary>
        /// <param name="isFetchingForGuardCondition"></param>
        /// <returns></returns>
        private Operator getLoopControlOperator(bool isFetchingForGuardCondition)
        {
            Operator result = LoopParameters.LoopBreakOperator;

            // If break operator is '==' then guard operator is '!='. This is same regardless this is forward/backward loop
            if (result.Oper == SyntaxKind.EqualsExpression && isFetchingForGuardCondition)
                result = Operator.ForSyntaxKind(SyntaxKind.NotEqualsExpression); // "!=";

            // If this is forward loop and we are fetching for break condition, no need to flip the operator
            else if (LoopParameters.IsForwardLoop && !isFetchingForGuardCondition) { }

            // If this is reverse loop and we are fetching for guard condition, no need to flop the operator
            else if (!LoopParameters.IsForwardLoop && isFetchingForGuardCondition) { }

            // If this is reverse loop and we are fetching for break condition OR this is forward loop and we are fetching for guard condition,
            // then flip the operator
            else
                result = flipOperator(result);

            return result;
        }

        private Operator flipOperator(Operator oldOperator)
        {
            Operator newOperator;
            switch (oldOperator.Oper)
            {
                case SyntaxKind.GreaterThanExpression: // ">":
                    newOperator = Operator.ForSyntaxKind(SyntaxKind.LessThanExpression); // "<";
                    break;
                case SyntaxKind.LessThanExpression: // "<":
                    newOperator = Operator.ForSyntaxKind(SyntaxKind.GreaterThanExpression); // ">";
                    break;
                case SyntaxKind.GreaterThanOrEqualExpression: // ">=":
                    newOperator = Operator.ForSyntaxKind(SyntaxKind.LessThanOrEqualExpression); // "<=";
                    break;
                case SyntaxKind.LessThanOrEqualExpression: // "<=":
                    newOperator = Operator.ForSyntaxKind(SyntaxKind.GreaterThanOrEqualExpression); // ">=";
                    break;
                default:
                    newOperator = oldOperator;
                    break;
            }
            return newOperator;
        }

        #endregion
    }

    public class LoopStatement
    {
        private int _nestNum;
        private int _noOfSecondaryInductionVariables;
        private List<InductionVariable> _inductionVariables;

        #region Properties

        public ExpressionSyntax Bounds;

        public bool IsContinueAllowedInLoopBody
        {
            get
            {
                // If all the primary IV has break condition at the end, then continue is not allowed in the loop body as 
                // that might cause infinite loop
                return !PrimaryInductionVariables.All(iv => iv.LoopParameters.IsBreakCondAtEndOfLoopBody);
            }
        }
        public bool IsWrappedInFunction = false;
        public bool IsUseEvalIsWrappedInFunction = false;
        public bool IsSnippetGenerated = false;
        protected List<StatementSyntax> Body = new List<StatementSyntax>();
        //TODO-future: labels for goto
        //public List<string> Labels = new List<string>();
        public List<InductionVariable> InductionVariables
        {
            get
            {
                if (_inductionVariables == null)
                    _inductionVariables = new List<InductionVariable>();
                return _inductionVariables;
            }
            private set { _inductionVariables = value; }
        }

        /// <summary>
        /// Returns list of primary induction variables
        /// </summary>
        public List<InductionVariable> PrimaryInductionVariables
        {
            get
            {
                if (InductionVariables != null)
                {
                    return InductionVariables.Where(iv => iv.IsPrimary).ToList();
                }
                else
                {
                    return new List<InductionVariable>();
                }
            }
        }

        /// <summary>
        /// Returns list of secondary induction variables
        /// </summary>
        public List<InductionVariable> SecondaryInductionVariables
        {
            get
            {
                if (InductionVariables != null)
                {
                    return InductionVariables.Where(iv => !iv.IsPrimary).ToList();
                }
                else
                {
                    return new List<InductionVariable>();
                }
            }
        }

        protected List<string> InductionVariableNamesInitializedOutsideLoop
        {
            get
            {
                if (InductionVariables != null)
                {
                    return InductionVariables.Where(iv => !iv.LoopParameters.IsInitInLoopHeader).Select(iv => iv.Name).ToList();
                }
                else
                {
                    return new List<string>();
                }
            }
        }

        protected bool offlineReduceOnly = false;
        public string LoopVar;
        public int NestNum
        {
            get { return _nestNum; }
            set
            {
                _nestNum = value;
                Debug.Assert(_nestNum > -1, "Trying to set implicit loop var before setting NestNum.");

                // If this is getting reset, clear the previously defined induction variables
                if (_inductionVariables != null)
                    _inductionVariables.Clear();

                InductionVariables.Add(new Statements.InductionVariable()
                {
                    Name = "__loopvar" + _nestNum,
                    LoopParameters = TC.AstUtils.GetForBoundParameters(),
                    IsPrimary = true
                });
                ValidateInductionVariablesParams();
            }
        }
        public Scope LocalScope;
        public int NumOfSecondaryInductionVariables
        {
            get { return _noOfSecondaryInductionVariables; }
            set
            {
                _noOfSecondaryInductionVariables = value;
                for (int i = 0; i < _noOfSecondaryInductionVariables; i++)
                {
                    InductionVariables.Add(new Statements.InductionVariable()
                    {
                        Name = "__loopSecondaryVar" + _nestNum + "_" + i,
                        LoopParameters = TC.AstUtils.GetForBoundParameters(),
                        IsPrimary = PRNG.Decide(0.1)    // Have 10% of extra primary induction variables              
                    });
                }
                ValidateInductionVariablesParams();
            }
        }
        public TestCase TC;

        /// <summary>
        /// Validates the loop parameters set for list of induction variables
        /// </summary>
        private void ValidateInductionVariablesParams()
        {
            for (int i = 0; i < InductionVariables.Count; i++)
            {
                var inductionVariable = InductionVariables[i];
                bool isForStatement = typeof(ForStatement) == this.GetType();

                // We generate loop header init/step only for ForStatement. So set these to 'false' for everything other than ForStatement
                inductionVariable.LoopParameters.IsInitInLoopHeader = isForStatement && inductionVariable.LoopParameters.IsInitInLoopHeader;
                inductionVariable.LoopParameters.IsStepInLoopHeader = isForStatement && inductionVariable.LoopParameters.IsStepInLoopHeader;

                //TODO-future: arrays
                //if (!inductionVariable.LoopParameters.IsLoopInvariantVariableUsed)
                //{
                //    string arrayVariableToAccessForLength = LocalScope.GetRandomArrayObject(SymbolAction.Read);
                //    if (!String.IsNullOrEmpty(arrayVariableToAccessForLength))
                //    {
                //        inductionVariable.LoopInvariantName = String.Format("{0}.length", arrayVariableToAccessForLength);
                //    }
                //}

                if (String.IsNullOrEmpty(inductionVariable.LoopInvariantName))
                    inductionVariable.LoopInvariantName = Constants.LoopInvariantName;
            }
        }

        protected bool HasSuccessfullyGenerated()
        {
            var result = true;
            // Check if IV has generated successfully
            foreach (var inductionVariable in InductionVariables)
            {
                result &= inductionVariable.HasSuccessfullyGenerated();
            }
            return result;
        }
        #endregion

        public void AddToBody(StatementSyntax stmt)
        {
            Body.Add(stmt);
        }

        public LoopStatement(TestCase tc)
        {
            TC = tc;
        }

        public string GetImplicitLoopVar()
        {
            return "__loopvar" + NestNum;
        }

        public virtual List<StatementSyntax> Generate(bool labels)
        {
            return null;
        }

        protected List<StatementSyntax> GetLoopBody()
        {
            // load the value of Property once instead of reading from Property everytime because it queries the list of IVs
            bool isContinueAllowedInLoopBody = IsContinueAllowedInLoopBody;

#if DEBUG
            foreach (StatementSyntax sm in Body)
            {
                if (sm is ContinueStatementSyntax)
                {
                    Debug.Assert(isContinueAllowedInLoopBody, "continue is not allowed in loop since break condition is at the end of loop.");
                }
            }
#endif
            return Body;
        }

        #region Loop induction code generation

        private List<string> generateComments()
        {
            return InductionVariables.Select(iv => iv.ToString()).ToList();
        }

        protected VariableDeclarationSyntax GenerateIVInitCode(bool isInitLoopHead = false)
        {
            List<VariableDeclaratorSyntax> loopInits = new List<VariableDeclaratorSyntax>();

            // Induction variables to be initialized outside the loop
            foreach (var inductionVar in InductionVariables)
            {
                if (this.GetType() != typeof(ForStatement) || // ForStatement may have init code in loop head. For other loops it has to be outside loop
                    inductionVar.LoopParameters.IsInitInLoopHeader == isInitLoopHead)
                {
                    inductionVar.isLoopInitGenerated = true;
                    loopInits.Add(VariableDeclarator(Identifier(inductionVar.Name)).WithInitializer(EqualsValueClause(inductionVar.GetLoopStart())));
                }
            }
            if (loopInits.Count > 0)
            {
                return VariableDeclaration(Helpers.GetToken(SyntaxKind.IntKeyword), SeparatedList(loopInits));
            }
            return null;
        }

        protected SeparatedSyntaxList<ExpressionSyntax> GenerateIVStepCode()
        {
            // Induction variables to be incr/decr in loop body
            List<ExpressionSyntax> loopInits = new List<ExpressionSyntax>();
            List<SyntaxNodeOrToken> finalInits = new List<SyntaxNodeOrToken>();

            for (int i = 0; i < InductionVariables.Count; i++)
            {
                InductionVariable inductionVariable = InductionVariables[i];
                if (inductionVariable.LoopParameters.IsStepInLoopHeader)
                {
                    inductionVariable.isLoopStepGenerated = true;
                    loopInits.Add(inductionVariable.GetLoopStep());
                }
            }

            if (loopInits.Count > 0)
            {
                finalInits.Add(loopInits[0]);
                for (int i = 1; i < loopInits.Count; i++)
                {
                    finalInits.Add(Token(SyntaxKind.CommaToken));
                    finalInits.Add(loopInits[i]);
                }
            }

            return SeparatedList<ExpressionSyntax>(finalInits);
        }

        protected List<StatementSyntax> GenerateIVBreakAndStepCode(bool isCodeForBreakCondAtTheEnd)
        {
            List<StatementSyntax> loopBreaks = new List<StatementSyntax>();
            List<StatementSyntax> loopPreCondSteps = new List<StatementSyntax>();
            List<StatementSyntax> loopPostCondSteps = new List<StatementSyntax>();

            // Generate break and step condition for primary variables
            foreach (var inductionVar in PrimaryInductionVariables)
            {

                if (!inductionVar.LoopParameters.IsBreakCondInLoopHeader && //  the break condition is not in loop header
                    inductionVar.LoopParameters.IsBreakCondAtEndOfLoopBody == isCodeForBreakCondAtTheEnd) //  If decided to add step at end of loop body and this is end of loop body
                {
                    inductionVar.isLoopBreakGenerated = true;
                    loopBreaks.Add(IfStatement(inductionVar.GetLoopBreakCondition(), Block(BreakStatement())));
                }
            }

            // Generate step condition for secondary variables
            foreach (var inductionVar in InductionVariables)
            {
                // Add step condition if step is not in loop header 
                if (!inductionVar.LoopParameters.IsStepInLoopHeader && // the step condition is not in loop header
                    inductionVar.LoopParameters.IsBreakCondAtEndOfLoopBody == isCodeForBreakCondAtTheEnd) //  If decided to add step at end of loop body and this is end of loop body
                {
                    inductionVar.isLoopStepGenerated = true;
                    if (inductionVar.LoopParameters.IsStepBeforeBreakCondition)
                    {
                        loopPreCondSteps.Add(ExpressionStatement(inductionVar.GetLoopStep()));
                    }
                    else
                    {
                        loopPostCondSteps.Add(ExpressionStatement(inductionVar.GetLoopStep()));
                    }
                }
            }
            loopBreaks.InsertRange(0, loopPreCondSteps);
            loopBreaks.AddRange(loopPostCondSteps);
            return loopBreaks;
        }

        protected ExpressionSyntax GenerateIVLoopGuardCode()
        {
            ExpressionSyntax guardCondition = null;

            // Secondary induction variables to be incr/decr in loop body
            foreach (var inductionVariable in PrimaryInductionVariables)
            {
                // Only generate guard condition for primary induction variables and whose LoopHeadCondition = true
                if (inductionVariable.LoopParameters.IsBreakCondInLoopHeader)
                {
                    inductionVariable.isLoopBreakGenerated = true;
                    var currCondition = inductionVariable.GetLoopGuardCondition();
                    if (guardCondition != null)
                    {
                        guardCondition = BinaryExpression(SyntaxKind.LogicalAndExpression, guardCondition, currCondition);
                    }
                    else
                    {
                        guardCondition = currCondition;
                    }
                }
            }
            return guardCondition;
        }

        #endregion

    }
}