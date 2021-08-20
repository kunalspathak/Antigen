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


namespace Antigen
{
    public static class PreGenerated
    {
        /// <summary>
        ///     Returns code related to using directives.
        /// </summary>
        public static List<UsingDirectiveSyntax> UsingDirective =
            new List<UsingDirectiveSyntax>()
            {
                            UsingDirective(IdentifierName("System"))
                            .WithUsingKeyword(Token(TriviaList(new[]{
                            Comment("// Licensed to the .NET Foundation under one or more agreements."),
                            Comment("// The .NET Foundation licenses this file to you under the MIT license."),
                            Comment("// See the LICENSE file in the project root for more information."),
                            Comment("//"),
                            Comment("// This file is auto-generated."),
                            Comment("// Seed: " + PRNG.GetSeed()),
                            Comment("//"),
                            }), SyntaxKind.UsingKeyword, TriviaList())),
                            UsingDirective(
                                QualifiedName(
                                    QualifiedName(
                                        IdentifierName("System"),
                                        IdentifierName("Collections")),
                                    IdentifierName("Generic"))),
                            UsingDirective(
                                QualifiedName(
                                    QualifiedName(
                                        IdentifierName("System"),
                                        IdentifierName("Runtime")),
                                    IdentifierName("CompilerServices")))
            };


        private static List<MethodDeclarationSyntax> staticMethods = null;

        /// <summary>
        ///     Returns list of 3 static methods - Main, Log and PrintLog.
        /// </summary>
        public static List<MethodDeclarationSyntax> StaticMethods
        {
            get
            {
                if (staticMethods != null)
                {
                    return staticMethods;
                }

                MethodDeclarationSyntax mainMethod = (MethodDeclarationSyntax)ParseMemberDeclaration(@$"public static void Main(string[] args) {{ }}");
                MethodDeclarationSyntax loggerMethod = (MethodDeclarationSyntax)ParseMemberDeclaration(
    $@"[MethodImpl(MethodImplOptions.NoInlining)]
public static void Log(string varName, object varValue) {{
    toPrint.Add($""{{varName}}={{varValue}}"");
}}");
                MethodDeclarationSyntax printLogMethod = (MethodDeclarationSyntax)ParseMemberDeclaration(
    $@"
public static void PrintLog() {{
    foreach (var entry in toPrint)
    {{
        Console.WriteLine(entry);
    }}
}}
");

                staticMethods = new List<MethodDeclarationSyntax>()
            {
                mainMethod, loggerMethod, printLogMethod
            };
                return staticMethods;
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
    }
}
