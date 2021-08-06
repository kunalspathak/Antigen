// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Antigen
{
    public class RslnUtilities
    {

        public static SyntaxTree GetValidSyntaxTree(SyntaxNode treeRoot, bool doValidation = true)
        {
            SyntaxTree validTree = CSharpSyntaxTree.ParseText(treeRoot.ToFullString());

#if DEBUG
            if (doValidation)
            {
                SyntaxTree syntaxTree = treeRoot.SyntaxTree;
                FindTreeDiff(validTree.GetRoot(), syntaxTree.GetRoot());
            }
#else
            // In release, make sure that we didn't end up generating wrong syntax tree,
            // hence parse the text to reconstruct the tree.
#endif
            return validTree;
        }

        /// <summary>
        ///     Method to find diff of generated tree vs. roslyn generated tree by parsing the
        ///     generated code.
        /// </summary>
        /// <param name="expected"></param>
        /// <param name="actual"></param>
        private static void FindTreeDiff(SyntaxNode expected, SyntaxNode actual)
        {
            if ((expected is LiteralExpressionSyntax) || (actual is LiteralExpressionSyntax))
            {
                // ignore
                return;
            }

            if (!expected.IsEquivalentTo(actual))
            {
                var expectedChildNodes = expected.ChildNodes().ToArray();
                var actualChildNodes = actual.ChildNodes().ToArray();

                int expectedCount = expectedChildNodes.Length;
                int actualCount = actualChildNodes.Length;
                if (expectedCount != actualCount)
                {
                    Debug.Assert(false, $"Child nodes mismatch. Expected= {expected}, Actual= {actual}");
                    return;
                }
                for (int ch = 0; ch < expectedCount; ch++)
                {
                    FindTreeDiff(expectedChildNodes[ch], actualChildNodes[ch]);
                }
                return;
            }
        }
    }
}
