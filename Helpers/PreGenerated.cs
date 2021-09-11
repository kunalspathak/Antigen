// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using Antigen.Statements;

namespace Antigen
{
    public static class PreGenerated
    {
        private static ArbitraryCodeStatement s_usingStmts = null;
        /// <summary>
        ///     Returns code related to using directives.
        /// </summary>
        public static Statement UsingDirective
        {
            get
            {
                if (s_usingStmts != null)
                {
                    return s_usingStmts;
                }

                string usingCode =
@"// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//
// This file is auto-generated.
// Seed: -1
//
                using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
";
                s_usingStmts = new ArbitraryCodeStatement(null, usingCode);
                return s_usingStmts;
            }
        }

        private static ArbitraryCodeStatement s_staticMethods = null;

        /// <summary>
        ///     Returns list of 3 static methods - Main, Log and PrintLog.
        /// </summary>
        public static Statement StaticMethods
        {
            get
            {
                if (s_staticMethods != null)
                {
                    return s_staticMethods;
                }

                StringBuilder staticMethodBuilder = new StringBuilder();

                // Main method
                staticMethodBuilder.AppendLine("public static void Main(string[] args) {  }");

                // Log method
                staticMethodBuilder.AppendLine("[MethodImpl(MethodImplOptions.NoInlining)]");
                staticMethodBuilder.AppendLine("public static void Log(string varName, object varValue) {");
                staticMethodBuilder.AppendLine(@$"     toPrint.Add($""{{varName}}={{varValue}}"");");
                staticMethodBuilder.AppendLine("}");

                // PrintLog method
                staticMethodBuilder.AppendLine("public static void PrintLog() {");
                staticMethodBuilder.AppendLine("foreach (var entry in toPrint) {");
                staticMethodBuilder.AppendLine("Console.WriteLine(entry);");
                staticMethodBuilder.AppendLine("}}");

                s_staticMethods = new ArbitraryCodeStatement(null, staticMethodBuilder.ToString());

                return s_staticMethods;
            }
        }

        private static MemberDeclarationSyntax loggerVarDecl = null;

        /// <summary>
        ///     Returns declaration of logger variable
        /// </summary>
        public static MemberDeclarationSyntax LoggerVariableDecl
        {
            get
            {
                if (loggerVarDecl == null)
                {
                    loggerVarDecl = ParseMemberDeclaration("private static List<string> toPrint = new List<string>();");
                }
                return loggerVarDecl;
            }
        }

        public static InvocationExpressionSyntax LogInvocationExpression = SyntaxFactory.InvocationExpression(IdentifierName("Log"));
    }
}
