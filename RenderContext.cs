// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Antigen
{
    public class RenderContext
    {
        public TextWriter Stream;

        public void Write(string s, params object[] args)
        {
            Stream.Write(s, args);
        }

        public void WriteLine(string s, params object[] args)
        {
            Stream.Write(s, args);
            Stream.WriteLine("");
        }
    }
}
