// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Antigen.Trimmer.Rewriters
{
    public class AssignStmtRemoval : SyntaxRewriter
    {
        public override SyntaxNode VisitExpressionStatement(ExpressionStatementSyntax node)
        {
            string assignmentExpr = node.ToFullString();
            if (assignmentExpr.Contains("loopvar", StringComparison.InvariantCultureIgnoreCase) ||
                assignmentExpr.Contains("loopInvariant", StringComparison.InvariantCultureIgnoreCase) ||
                assignmentExpr.Contains("loopSecondaryVar", StringComparison.InvariantCultureIgnoreCase))
            {
                return base.VisitExpressionStatement(node);
            }

            if (currId++ == id || removeAll)
            {
                isAnyNodeVisited = true;

                return null;
            }

            return base.VisitExpressionStatement(node);
        }
    }
}
