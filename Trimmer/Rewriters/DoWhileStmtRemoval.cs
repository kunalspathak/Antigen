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
    public class DoWhileStmtRemoval : SyntaxRewriter
    {
        public override SyntaxNode VisitDoStatement(DoStatementSyntax node)
        {
            if (currId++ == id || removeAll)
            {
                return null;
            }

            return base.VisitDoStatement(node);
        }
    }
}
