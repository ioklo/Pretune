using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Pretune.Abstractions;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Pretune.Generators
{
    class IEquatableGenerator : IGenerator
    {
        public bool ShouldApply(ITypeSymbol typeSymbol)
        {
            return Misc.HasPretuneAttribute(typeSymbol, "ImplementIEquatable");
        }

        MemberDeclarationSyntax GenerateEquals(ITypeSymbol typeSymbol, string typeName)
        {
            var equalsExps = new List<ExpressionSyntax>();

            var nullCheckExp = ParseExpression("other != null");
            if (nullCheckExp == null) throw new PretuneGeneralException();
            equalsExps.Add(nullCheckExp);

            foreach (var field in Misc.EnumerateFields(typeSymbol))
            {
                var fieldName = field.Name;
                var type = Misc.GetFieldTypeSyntax(field);

                var exp = ParseExpression($"System.Collections.Generic.EqualityComparer<{type}>.Default.Equals({fieldName}, other.{fieldName})");
                if (exp == null)
                    throw new PretuneGeneralException();

                equalsExps.Add(exp);
            }
            // fold
            var returnExp = equalsExps.Aggregate((e1, e2) => BinaryExpression(SyntaxKind.LogicalAndExpression, e1, e2));
            var equalsDecl = ParseMemberDeclaration($"public bool Equals({typeName}? other) {{ return {returnExp}; }}");

            if (equalsDecl == null) throw new PretuneGeneralException();
            return equalsDecl;
        }

        MemberDeclarationSyntax GenerateGetHashCode(ITypeSymbol typeSymbol)
        {
            void AddStatements(List<StatementSyntax> stmts, List<ISymbol> fields, int startIndex, int count)
            {
                for (int i = startIndex; i < startIndex + count; i++)
                {
                    var stmt = ExpressionStatement(
                        InvocationExpression(
                            MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                IdentifierName("hashCode"),
                                IdentifierName("Add")
                            )
                        ).WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(IdentifierName(fields[i].Name)))))
                    );

                    stmts.Add(stmt);
                }
            }

            var stmts = new List<StatementSyntax>();
            
            var hashCodeDecl = ParseStatement("var hashCode = new System.HashCode();");
            if (hashCodeDecl == null) throw new PretuneGeneralException();
            stmts.Add(hashCodeDecl);

            var fields = Misc.EnumerateFields(typeSymbol).ToList();
            AddStatements(stmts, fields, 0, fields.Count);

            var retStmt = ReturnStatement(
                InvocationExpression(
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        IdentifierName("hashCode"),
                        IdentifierName("ToHashCode"))));

            stmts.Add(retStmt);


            var getHashCodeDecl = MethodDeclaration(
                PredefinedType(Token(SyntaxKind.IntKeyword)),
                Identifier("GetHashCode")
            ).WithModifiers(
                TokenList(new[] { Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.OverrideKeyword) })
            ).WithBody(
                Block(stmts)
            );

            return getHashCodeDecl;
        }

        public GeneratorResult Generate(ITypeSymbol typeSymbol)
        {
            // 베이스 한개(IEquatable<T>), 멤버 세개
            // public override bool Equals(object? obj) => Equals(obj as T);
            // public bool Equals(T obj);
            // public override int GetHashCode();
            if (typeSymbol.TypeKind == TypeKind.Class)
            {
                var typeName = typeSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
                var equatableType = ParseTypeName($"System.IEquatable<{typeName}?>");

                var objEqualsDecl = ParseMemberDeclaration($"public override bool Equals(object? obj) => Equals(obj as {typeName});");
                if (objEqualsDecl == null) throw new PretuneGeneralException();

                //
                var equalsDecl = GenerateEquals(typeSymbol, typeName);

                var getHashCodeDecl = GenerateGetHashCode(typeSymbol);

                return new GeneratorResult(
                    ImmutableArray.Create<BaseTypeSyntax>(SimpleBaseType(equatableType)),
                    ImmutableArray.Create<MemberDeclarationSyntax>(objEqualsDecl, equalsDecl, getHashCodeDecl));
            }

            throw new PretuneGeneralException();
        }
    }
}
