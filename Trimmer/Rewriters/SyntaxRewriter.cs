// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Antigen.Trimmer.Rewriters
{
    public class SyntaxRewriter : CSharpSyntaxRewriter
    {
        protected int id = -1;
        protected int currId = 0;
        protected bool removeAll = true;

        public void RemoveAll()
        {
            removeAll = true;
        }

        public void RemoveOneByOne()
        {
            removeAll = false;
        }

        /// <summary>
        ///     Returns total visited in recent call to Visit().
        /// </summary>
        public int TotalVisited => currId;

        public void Reset()
        {
            currId = 0;
        }

        public void UpdateId(int newId)
        {
            id = newId;
        }
    }
}
