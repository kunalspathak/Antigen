using Antigen.Tree;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using ValueType = Antigen.Tree.ValueType;

namespace Antigen
{
    public static partial class Helpers
    {
        public static BinaryExpressionSyntax GetBinaryExpression(ExpressionSyntax lhs, Operator op, ExpressionSyntax rhs)
        {
            return BinaryExpression(op.Oper, lhs, rhs);
        }

        public static ParenthesizedExpressionSyntax GetWrappedAndCastedExpression(ValueType fromType, ValueType toType, ExpressionSyntax expr)
        {
            //Debug.Assert(fromType.CanConvert(toType) || toType.PrimitiveType == Primitive.Boolean);
            ParenthesizedExpressionSyntax parenExpr = ParenthesizedExpression(expr);

            //if (fromType.CanConvertExplicit(toType))
            //{
                parenExpr = ParenthesizedExpression(CastExpression(Helpers.GetToken(toType.TypeKind), parenExpr));
            //}
            //else
            //{
            //    Debug.Assert(fromType.CanConvertImplicit(toType) || toType.PrimitiveType == Primitive.Boolean);
            //}

            return parenExpr;
        }

        public static SyntaxList<AttributeListSyntax> NoInlineAttr =>
            SingletonList<AttributeListSyntax>(
                AttributeList(
                    SingletonSeparatedList<AttributeSyntax>(
                        Attribute(
                            IdentifierName("MethodImpl"))
                        .WithArgumentList(
                            AttributeArgumentList(
                                SingletonSeparatedList<AttributeArgumentSyntax>(
                                    AttributeArgument(
                                        MemberAccessExpression(
                                            SyntaxKind.SimpleMemberAccessExpression,
                                            IdentifierName("MethodImplOptions"),
                                            IdentifierName("NoInlining")))))))));

        /// <summary>
        ///     Converts a <see cref="List{TNode}"/> to <see cref="SyntaxList{TNode}"/>.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list"></param>
        /// <returns></returns>
        public static SyntaxList<T> ToSyntaxList<T>(this IList<T> list) where T : SyntaxNode
        {
            return new SyntaxList<T>(list);
        }

        /// <summary>
        ///     Generate log invoke statement
        /// </summary>
        /// <param name="variableName"></param>
        /// <returns></returns>
        public static StatementSyntax GetLogInvokeStatement(string variableName)
        {
            return ExpressionStatement(
                InvocationExpression(
                    IdentifierName("Log"))
                .WithArgumentList(
                    ArgumentList(
                    SeparatedList<ArgumentSyntax>(
                        new SyntaxNodeOrToken[]
                        {
                            Argument(LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(variableName))),
                            Token(SyntaxKind.CommaToken),
                            Argument(GetVariableAccessExpression(variableName))
                        }
             ))));
        }
    }
}
