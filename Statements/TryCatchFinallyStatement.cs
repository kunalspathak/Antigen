// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Antigen.Statements
{
    public class TryCatchFinallyStatement : Statement
    {
        public readonly List<Statement> TryBody;
        public readonly List<Tuple<Type, List<Statement>>> CatchBodies;
        public readonly List<Statement> FinallyBody;

        public TryCatchFinallyStatement(TestCase testCase,
            List<Statement> tryBody, List<Tuple<Type, List<Statement>>> catchBodies, List<Statement> finallyBody) : base(testCase)
        {
            TryBody = tryBody;
            CatchBodies = catchBodies;
            FinallyBody = finallyBody;

            PopulateContent();
        }

        public override string ToString()
        {
            return _contents;
        }

        protected override void PopulateContent()
        {
            StringBuilder strBuilder = new StringBuilder();
            strBuilder.AppendLine("try");
            strBuilder.AppendLine("{");
            strBuilder.AppendLine(string.Join(Environment.NewLine, TryBody));
            strBuilder.AppendLine("}");
            if (CatchBodies != null && CatchBodies.Count > 0)
            {
                foreach (var catchClause in CatchBodies)
                {
                    strBuilder.AppendLine($"catch ({catchClause.Item1})");
                    strBuilder.AppendLine("{");
                    strBuilder.AppendLine(string.Join(Environment.NewLine, catchClause.Item2));
                    strBuilder.AppendLine("}");
                }
            }
            if (FinallyBody != null && FinallyBody.Count > 0)
            {
                strBuilder.AppendLine($"finally");
                strBuilder.AppendLine("{");
                strBuilder.AppendLine(string.Join(Environment.NewLine, FinallyBody));
                strBuilder.AppendLine("}");
            }
            _contents = strBuilder.ToString();
        }
    }
}
