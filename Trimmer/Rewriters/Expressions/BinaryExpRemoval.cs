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
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;


namespace Antigen.Trimmer.Rewriters.Expressions
{
    public class BinaryExpRemoval : SyntaxRewriter
    {
        public override SyntaxNode VisitBinaryExpression(BinaryExpressionSyntax node)
        {
            if (currId++ == id || removeAll)
            {
                return ParseExpression("1+2").SyntaxTree.GetRoot();
            }

            return base.VisitBinaryExpression(node);
        }
    }
}
