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

namespace Antigen.Trimmer.Rewriters.Expressions
{
    public class CastExprRemoval : SyntaxRewriter
    {
        public override SyntaxNode VisitCastExpression(CastExpressionSyntax node)
        {
            if (currId++ == id || removeAll)
            {
                isAnyNodeVisited = true;

                var expr = node.ChildNodes().ToList()[1];
                if (expr is ParenthesizedExpressionSyntax parenExpr)
                {
                    return VisitParenthesizedExpression(parenExpr);
                }
                else if (expr is LiteralExpressionSyntax literalExpr)
                {
                    return VisitLiteralExpression(literalExpr);
                }
            }

            return base.VisitCastExpression(node);
        }
    }
}
