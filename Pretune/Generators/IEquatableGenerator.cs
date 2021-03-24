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
        public bool ShouldApply(TypeDeclarationSyntax typeDecl, SemanticModel model)
        {
            if (!(typeDecl is ClassDeclarationSyntax) && !(typeDecl is StructDeclarationSyntax))
                return false;

            return Misc.HasPretuneAttribute(typeDecl, model, "ImplementIEquatable");
        }

        StatementSyntax GenerateTestStmtNonNullableMember(ISymbol field)
        {
            // if (!member.Equals(other.member)) return false;
            var fieldName = field.Name;

            return IfStatement(
                PrefixUnaryExpression(
                    SyntaxKind.LogicalNotExpression,
                    InvocationExpression(MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        IdentifierName(fieldName),
                        IdentifierName("Equals")
                    )).WithArgumentList(ArgumentList(SingletonSeparatedList<ArgumentSyntax>(Argument(MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        IdentifierName("other"),
                        IdentifierName(fieldName)
                    )))))
                ),
                ReturnStatement(LiteralExpression(SyntaxKind.FalseLiteralExpression))
            );
        }

        StatementSyntax GenerateTestStmtNullableMember(ISymbol field)
        {
            var fieldName = field.Name;

            return IfStatement(
    BinaryExpression(
        SyntaxKind.NotEqualsExpression,
        IdentifierName(fieldName),
        LiteralExpression(
            SyntaxKind.NullLiteralExpression)),
    Block(
        SingletonList<StatementSyntax>(
            IfStatement(
                PrefixUnaryExpression(
                    SyntaxKind.LogicalNotExpression,
                    InvocationExpression(
                        MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            IdentifierName("ns"),
                            IdentifierName("Equals")))
                    .WithArgumentList(
                        ArgumentList(
                            SingletonSeparatedList<ArgumentSyntax>(
                                Argument(
                                    MemberAccessExpression(
                                        SyntaxKind.SimpleMemberAccessExpression,
                                        IdentifierName("other"),
                                        IdentifierName(fieldName))))))),
                ReturnStatement(
                    LiteralExpression(
                        SyntaxKind.FalseLiteralExpression))))))
.WithElse(
    ElseClause(
        IfStatement(
            BinaryExpression(
                SyntaxKind.NotEqualsExpression,
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    IdentifierName("other"),
                    IdentifierName(fieldName)),
                LiteralExpression(
                    SyntaxKind.NullLiteralExpression)),
            ReturnStatement(
                LiteralExpression(
                    SyntaxKind.FalseLiteralExpression)))));
        }

        MemberDeclarationSyntax GenerateClassEquals(ITypeSymbol typeSymbol, string typeName)
        {
            // statement로 
            var stmts = new List<StatementSyntax>();

            // 1. null test
            var nullTestStmt = ParseStatement("if (other != null) return false;");
            if (nullTestStmt == null) throw new PretuneGeneralException();
            stmts.Add(nullTestStmt);

            // 2. foreach(field in symbol)
            foreach (var field in Misc.EnumerateInstanceFields(typeSymbol))
            {
                StatementSyntax stmt;

                if (!Misc.IsNullableType(field))
                    stmt = GenerateTestStmtNonNullableMember(field);
                else
                    stmt = GenerateTestStmtNullableMember(field);                

                stmts.Add(stmt);
            }

            // 3. return true;
            var retStmt = ReturnStatement(LiteralExpression(SyntaxKind.TrueLiteralExpression));
            stmts.Add(retStmt);

            var equalsDecl = MethodDeclaration(
                PredefinedType(Token(SyntaxKind.BoolKeyword)),
                Identifier("Equals")
            ).WithModifiers(
                TokenList(Token(SyntaxKind.PublicKeyword))
            ).WithParameterList(
                ParameterList(SingletonSeparatedList<ParameterSyntax>(
                    Parameter(
                        Identifier("other")
                    ).WithType(
                        NullableType(IdentifierName(typeName))
                    )
                ))
            ).WithBody(
                Block(stmts)
            );

            return equalsDecl;
        }

        MemberDeclarationSyntax GenerateStructEquals(ITypeSymbol typeSymbol, string typeName)
        {
            var equalsExps = new List<ExpressionSyntax>();

            foreach (var field in Misc.EnumerateInstanceFields(typeSymbol))
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
            var equalsDecl = ParseMemberDeclaration($"public bool Equals({typeName} other) {{ return {returnExp}; }}");

            if (equalsDecl == null) throw new PretuneGeneralException();
            return equalsDecl;
        }

        StatementSyntax GenerateHashCodeAddNonNullableMember(string memberName)
        {
            // hashCode.Add(this.x.GetHashCode());

            return ExpressionStatement(
                InvocationExpression(MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        IdentifierName("hashCode"),
                        IdentifierName("Add")
                )).WithArgumentList(
                    ArgumentList(SingletonSeparatedList<ArgumentSyntax>(Argument(
                        InvocationExpression(MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                ThisExpression(),
                                IdentifierName(memberName)
                            ),
                            IdentifierName("GetHashCode")
                        ))
                    )))
                )
            );
        }

        StatementSyntax GenerateHashCodeAddNullableMember(string memberName)
        {
            // hashCode.Add(this.x == null ? 0 : this.x.GetHashCode());

            return ExpressionStatement(
                InvocationExpression(MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    IdentifierName("hashCode"),
                    IdentifierName("Add")
                )).WithArgumentList(
                    ArgumentList(SingletonSeparatedList<ArgumentSyntax>(Argument(
                        ConditionalExpression(
                            BinaryExpression(
                                SyntaxKind.EqualsExpression,
                                MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    ThisExpression(),
                                    IdentifierName(memberName)
                                ),
                                LiteralExpression(SyntaxKind.NullLiteralExpression)
                            ),
                            LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(0)),
                            InvocationExpression(MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    ThisExpression(),
                                    IdentifierName(memberName)
                                ),
                                IdentifierName("GetHashCode")
                            ))
                        )
                    )))
                )
            );
        }

        MemberDeclarationSyntax GenerateGetHashCode(ITypeSymbol typeSymbol)
        {   
            var stmts = new List<StatementSyntax>();
            
            // 1. construct hashCode 
            var hashCodeDecl = ParseStatement("var hashCode = new System.HashCode();");
            if (hashCodeDecl == null) throw new PretuneGeneralException();
            stmts.Add(hashCodeDecl);

            // 2. foreach fields hashcode.Add(...); 
            foreach (var field in Misc.EnumerateInstanceFields(typeSymbol))
            {
                StatementSyntax stmt;
                if (!Misc.IsNullableType(field))
                    stmt = GenerateHashCodeAddNonNullableMember(field.Name);
                else
                    stmt = GenerateHashCodeAddNullableMember(field.Name);

                stmts.Add(stmt);
            }

            // 3. retStmt
            var retStmt = ReturnStatement(
                InvocationExpression(
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        IdentifierName("hashCode"),
                        IdentifierName("ToHashCode"))));
            stmts.Add(retStmt);

            // public override int GetHashCode() { ... }
            return MethodDeclaration(
                PredefinedType(Token(SyntaxKind.IntKeyword)),
                Identifier("GetHashCode")
            ).WithModifiers(
                TokenList(new[] { Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.OverrideKeyword) })
            ).WithBody(
                Block(stmts)
            );
        }

        // class를 
        GeneratorResult GenerateClass(ITypeSymbol typeSymbol)
        {
            var typeName = typeSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
            var equatableType = ParseTypeName($"System.IEquatable<{typeName}>");

            var objEqualsDecl = ParseMemberDeclaration($"public override bool Equals(object? obj) => Equals(obj as {typeName});");
            if (objEqualsDecl == null) throw new PretuneGeneralException();

            var equalsDecl = GenerateClassEquals(typeSymbol, typeName);

            var getHashCodeDecl = GenerateGetHashCode(typeSymbol);

            return new GeneratorResult(
                ImmutableArray.Create<BaseTypeSyntax>(SimpleBaseType(equatableType)),
                ImmutableArray.Create<MemberDeclarationSyntax>(objEqualsDecl, equalsDecl, getHashCodeDecl));
        }

        public GeneratorResult Generate(ITypeSymbol typeSymbol)
        {
            // 베이스 한개(IEquatable<T>), 멤버 세개
            // public override bool Equals(object? obj) => Equals(obj as T);
            // public bool Equals(T obj);
            // public override int GetHashCode();
            if (typeSymbol.TypeKind == TypeKind.Class)
            {
                return GenerateClass(typeSymbol);
            }
            else if (typeSymbol.TypeKind == TypeKind.Struct)
            {
                var typeName = typeSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
                var equatableType = ParseTypeName($"System.IEquatable<{typeName}>");

                var objEqualsDecl = ParseMemberDeclaration($"public override bool Equals(object? obj) => obj is {typeName} other && Equals(other);");
                if (objEqualsDecl == null) throw new PretuneGeneralException();

                var equalsDecl = GenerateStructEquals(typeSymbol, typeName);

                var getHashCodeDecl = GenerateGetHashCode(typeSymbol);

                return new GeneratorResult(
                    ImmutableArray.Create<BaseTypeSyntax>(SimpleBaseType(equatableType)),
                    ImmutableArray.Create<MemberDeclarationSyntax>(objEqualsDecl, equalsDecl, getHashCodeDecl));
            }

            throw new PretuneGeneralException();
        }
    }
}
