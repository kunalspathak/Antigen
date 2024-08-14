// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Antigen.Execution
{
    public enum RunOutcome
    {
        Success,
        Timeout,
        AssertionFailure,
        OutputMismatch,
        OtherError,
    }

    internal struct ExecuteResult
    {
        internal string OtherErrorMessage { get; private set; }
        internal string AssertionMessage { get; private set; }
        internal RunOutcome Result { get; private set; }

        internal static ExecuteResult GetSuccessResult()
        {
            return new ExecuteResult(RunOutcome.Success, null);
        }

        internal static ExecuteResult GetOtherErrorResult(string errorMessage)
        {
            return new ExecuteResult(RunOutcome.OtherError, null, errorMessage);
        }

        internal static ExecuteResult GetTimeoutResult()
        {
            return new ExecuteResult(RunOutcome.Timeout, null);
        }

        internal static ExecuteResult GetAssertionFailureResult(string assertionMessage)
        {
            return new ExecuteResult(RunOutcome.AssertionFailure, assertionMessage);
        }

        internal static ExecuteResult GetOutputMismatchResult(string outputDiff)
        {
            ExecuteResult result = new ExecuteResult(RunOutcome.OutputMismatch, null, outputDiff);
            return result;
        }

        private ExecuteResult(RunOutcome result, string assertionMessage, string errorMessage = null)
        {
            Result = result;
            AssertionMessage = assertionMessage;
            OtherErrorMessage = errorMessage;
        }

    }
}
