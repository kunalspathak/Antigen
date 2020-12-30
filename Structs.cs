// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Antigen.Tree;

namespace Antigen
{
    /// <summary>
    ///     Represents a field present in a struct. This is useful to
    ///     expand the fully qualifier name of a field present inside a
    ///     nested struct.
    /// </summary>
    public struct StructField
    {
        public string FieldName;
        public Tree.ValueType FieldType;

        public StructField(Tree.ValueType type, string name)
        {
            FieldType = type;
            FieldName = name;
        }

        public override string ToString()
        {
            return $"{Enum.GetName(typeof(Primitive), FieldType.PrimitiveType)} {FieldName}";
        }
    }
}
