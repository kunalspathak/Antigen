// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Antigen.Tree
{
    public class Node
    {
        protected TestCase _testCase;

        //public virtual void Render(RenderContext renderContext)
        //{

        //}

        public override string ToString()
        {
            return string.Empty; // base.ToString();
        }

        public Node(TestCase tc)
        {
            _testCase = tc;
        }
    }
}
