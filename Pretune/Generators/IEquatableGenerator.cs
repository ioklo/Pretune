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
        // unbounded, ImmutableArray<> => NS1.NS2.X (class)
        // bounded, ImmutableArray<int> => NS1.NS2.X (class symbol)

        Dictionary<INamedTypeSymbol, INamedTypeSymbol> unboundTypeCustomEqComparers;
        Dictionary<INamedTypeSymbol, INamedTypeSymbol> boundTypeCustomEqComparers;

        public IEquatableGenerator()
        {
            unboundTypeCustomEqComparers = new Dictionary<INamedTypeSymbol, INamedTypeSymbol>(SymbolEqualityComparer.Default);
            boundTypeCustomEqComparers = new Dictionary<INamedTypeSymbol, INamedTypeSymbol>(SymbolEqualityComparer.Default);
        }

        // GetCustomEqComparer(ImmutableArray<int>)
        ITypeSymbol? GetCustomEqComparer(ITypeSymbol typeSymbol)
        {
            if (typeSymbol is INamedTypeSymbol namedTypeSymbol)
            {                
                // ImmutableArray<>를 실제로 쓸 수 없다
                if (namedTypeSymbol.IsUnboundGenericType) return null;

                // 1. bound에서 먼저 찾는다
                if (boundTypeCustomEqComparers.TryGetValue(namedTypeSymbol, out var boundTypeComparerType))
                    return boundTypeComparerType;

                // 2. generic이라면 unbound에서도 찾아본다
                if (namedTypeSymbol.IsGenericType)
                {
                    var unboundTypeSymbol = namedTypeSymbol.ConstructUnboundGenericType();
                    if (unboundTypeCustomEqComparers.TryGetValue(unboundTypeSymbol, out var unboundTypeComparerType))
                        return unboundTypeComparerType;
                }
            }

            return null;
        }

        public bool HandleTypeDecl(TypeDeclarationSyntax typeDecl, SemanticModel model)
        {
            if (!(typeDecl is ClassDeclarationSyntax) && !(typeDecl is StructDeclarationSyntax))
                return false;

            var typeSymbol = model.GetDeclaredSymbol(typeDecl);
            if (typeSymbol == null)
                return false;

            foreach(var attr in typeSymbol.GetAttributes())
            {
                if (attr.AttributeClass == null || !Misc.IsPretuneAttribute(attr.AttributeClass, "CustomEqualityComparer"))
                    continue;

                foreach (var arg in attr.ConstructorArguments)
                {
                    // TODO: 아니라면 워닝
                    if (arg.Kind == TypedConstantKind.Array)
                    {
                        foreach (var value in arg.Values)
                        {
                            if (value.Kind != TypedConstantKind.Type) continue;

                            if (value.Value is INamedTypeSymbol namedArg)
                            {
                                // ImmutableArray<> ..
                                if (namedArg.IsUnboundGenericType)
                                    unboundTypeCustomEqComparers.Add(namedArg, typeSymbol);
                                else
                                    boundTypeCustomEqComparers.Add(namedArg, typeSymbol);
                            }
                        }
                    }
                }
            }

            return Misc.HasPretuneAttribute(typeDecl, model, "ImplementIEquatable");
        }

        StatementSyntax? HandleCustomEqualityComparer(MemberSymbol memberSymbol)
        {
            var comparerTypeSymbol = GetCustomEqComparer(memberSymbol.GetTypeSymbol());
            if (comparerTypeSymbol == null) return null;

            // if (!global::MyNamespace.CustomEqualityComparer.Equals(member, other.member)) return false;

            var fieldName = memberSymbol.GetName();
            var typeExp = ParseExpression($"global::{comparerTypeSymbol.ToDisplayString()}");

            return IfStatement(
                PrefixUnaryExpression(
                    SyntaxKind.LogicalNotExpression,
                    InvocationExpression(
                        MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            typeExp,
                            IdentifierName("Equals")
                        )
                    ).WithArgumentList(
                        ArgumentList(SeparatedList<ArgumentSyntax>(new SyntaxNodeOrToken[]{
                            Argument(IdentifierName(fieldName)),
                            Token(SyntaxKind.CommaToken),
                            Argument(MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                IdentifierName("other"),
                                IdentifierName(fieldName)
                            ))
                        }))
                    )
                ),
                ReturnStatement(LiteralExpression(SyntaxKind.FalseLiteralExpression))
            );
        }        

        StatementSyntax GenerateTestStmtNonNullableMember(MemberSymbol memberSymbol)
        {
            // field 타입이 Custom인 경우
            var statement = HandleCustomEqualityComparer(memberSymbol);
            if (statement != null) return statement;

            // if (!member.Equals(other.member)) return false;
            var fieldName = memberSymbol.GetName();

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

        StatementSyntax GenerateTestStmtNullableReferenceTypeMember(MemberSymbol memberSymbol)
        {
            var fieldName = memberSymbol.GetName();
            var nonnullableStmt = GenerateTestStmtNonNullableMember(memberSymbol);

            return IfStatement(
                BinaryExpression(
                    SyntaxKind.NotEqualsExpression,
                    IdentifierName(fieldName),
                    LiteralExpression(SyntaxKind.NullLiteralExpression)
                ),
                Block(SingletonList<StatementSyntax>(nonnullableStmt))
            ).WithElse(ElseClause(
                IfStatement(
                    BinaryExpression(
                        SyntaxKind.NotEqualsExpression,
                        MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            IdentifierName("other"),
                            IdentifierName(fieldName)
                        ),
                        LiteralExpression(SyntaxKind.NullLiteralExpression)
                    ),
                    ReturnStatement(LiteralExpression(SyntaxKind.FalseLiteralExpression))
                )
            ));
        }

        StatementSyntax GenerateTestStmtNullableValueTypeMember(MemberSymbol memberSymbol)
        {
            var memberName = memberSymbol.GetName();

            return IfStatement(
                BinaryExpression(
                    SyntaxKind.LogicalAndExpression,
                    BinaryExpression(
                        SyntaxKind.NotEqualsExpression,
                        IdentifierName(memberName),
                        LiteralExpression(SyntaxKind.NullLiteralExpression)
                    ),
                    BinaryExpression(
                        SyntaxKind.NotEqualsExpression,
                        MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            IdentifierName("other"),
                            IdentifierName(memberName)
                        ),
                        LiteralExpression(SyntaxKind.NullLiteralExpression)
                    )
                ),
                Block(SingletonList<StatementSyntax>(
                    IfStatement(
                        PrefixUnaryExpression(
                            SyntaxKind.LogicalNotExpression,
                            InvocationExpression(
                                MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    MemberAccessExpression(
                                        SyntaxKind.SimpleMemberAccessExpression,
                                        IdentifierName(memberName),
                                        IdentifierName("Value")
                                    ),
                                    IdentifierName("Equals")
                                )
                            ).WithArgumentList(ArgumentList(SingletonSeparatedList<ArgumentSyntax>(Argument(
                                MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    MemberAccessExpression(
                                        SyntaxKind.SimpleMemberAccessExpression,
                                        IdentifierName("other"),
                                        IdentifierName(memberName)
                                    ),
                                    IdentifierName("Value")
                                )
                            ))))
                        ),
                        ReturnStatement(LiteralExpression(SyntaxKind.FalseLiteralExpression))
                    )
                ))
            ).WithElse(ElseClause(
                IfStatement(
                    BinaryExpression(
                        SyntaxKind.LogicalOrExpression,
                        BinaryExpression(
                            SyntaxKind.NotEqualsExpression,
                            IdentifierName(memberName),
                            LiteralExpression(SyntaxKind.NullLiteralExpression)
                        ),
                        BinaryExpression(
                            SyntaxKind.NotEqualsExpression,
                            MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                IdentifierName("other"),
                                IdentifierName(memberName)
                            ),
                            LiteralExpression(SyntaxKind.NullLiteralExpression)
                        )
                    ),
                    ReturnStatement(LiteralExpression(SyntaxKind.FalseLiteralExpression))
                )
            ));
        }

        StatementSyntax GenerateTestStmtNullableMember(MemberSymbol memberSymbol)
        {
            if (memberSymbol.GetTypeSymbol().IsReferenceType)
                return GenerateTestStmtNullableReferenceTypeMember(memberSymbol);
            else
                return GenerateTestStmtNullableValueTypeMember(memberSymbol);
        }

        MemberDeclarationSyntax GenerateClassEquals(ITypeSymbol typeSymbol, string typeName)
        {
            // statement로 
            var stmts = new List<StatementSyntax>();

            // 1. null test
            var nullTestStmt = ParseStatement("if (other == null) return false;");
            if (nullTestStmt == null) throw new PretuneGeneralException();
            stmts.Add(nullTestStmt);

            // 2. foreach(field in symbol)
            foreach (var member in Misc.EnumerateInstanceMembers(typeSymbol))
            {
                var stmt = GenerateTestMemberStmt(member);
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

        StatementSyntax GenerateTestMemberStmt(MemberSymbol member)
        {
            if (!member.IsNullableType())
                return GenerateTestStmtNonNullableMember(member);
            else
                return GenerateTestStmtNullableMember(member);
        }

        MemberDeclarationSyntax GenerateStructEquals(ITypeSymbol typeSymbol, string typeName)
        {
            // statement로 
            var stmts = new List<StatementSyntax>();
            
            // 1. foreach(field in symbol)
            foreach (var member in Misc.EnumerateInstanceMembers(typeSymbol))
            {
                var stmt = GenerateTestMemberStmt(member);
                stmts.Add(stmt);
            }

            // 2. return true;
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
                        IdentifierName(typeName)
                    )
                ))
            ).WithBody(
                Block(stmts)
            );

            return equalsDecl;
        }

        ExpressionSyntax GenerateGetHashCodeCustom(ITypeSymbol comparerTypeSymbol, MemberSymbol memberSymbol)
        {
            var memberName = memberSymbol.GetName();
            var typeExp = ParseExpression($"global::{comparerTypeSymbol.ToDisplayString()}");

            return InvocationExpression(MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                typeExp,
                IdentifierName("GetHashCode")
            )).WithArgumentList(ArgumentList(SingletonSeparatedList<ArgumentSyntax>(Argument(
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    ThisExpression(),
                    IdentifierName(memberName)
                )
            ))));
        }

        StatementSyntax GenerateHashCodeAddNonNullableMember(MemberSymbol memberSymbol)
        {
            var exp = GenerateGetHashCode(memberSymbol);

            // hashCode.Add(this.x.GetHashCode());
            return ExpressionStatement(
                InvocationExpression(MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        IdentifierName("hashCode"),
                        IdentifierName("Add")
                )).WithArgumentList(
                    ArgumentList(SingletonSeparatedList<ArgumentSyntax>(Argument(exp)))
                )
            );
        }

        // this.x.GetHashCode() or global::NS1.CustomComparer.GetHashCode(this.x)
        ExpressionSyntax GenerateGetHashCode(MemberSymbol memberSymbol)
        {
            var comparerTypeSymbol = GetCustomEqComparer(memberSymbol.GetTypeSymbol());
            if (comparerTypeSymbol != null)
                return GenerateGetHashCodeCustom(comparerTypeSymbol, memberSymbol);

            var memberName = memberSymbol.GetName();

            return InvocationExpression(MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    ThisExpression(),
                    IdentifierName(memberName)
                ),
                IdentifierName("GetHashCode")
            ));
        }

        StatementSyntax GenerateHashCodeAddNullableReferenceTypeMember(MemberSymbol memberSymbol)
        {
            var memberName = memberSymbol.GetName();
            // hashCode.Add(this.x == null ? 0 : this.x.GetHashCode());

            var exp = GenerateGetHashCode(memberSymbol);

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
                            exp
                        )
                    )))
                )
            );
        }

        StatementSyntax GenerateHashCodeAddNullableValueTypeMember(MemberSymbol memberSymbol)
        {
            var memberName = memberSymbol.GetName();
            // hashCode.Add(this.x == null ? 0 : this.x.Value.GetHashCode());

            return ExpressionStatement(
                InvocationExpression(
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        IdentifierName("hashCode"),
                        IdentifierName("Add")
                    )
                ).WithArgumentList(
                    ArgumentList(SingletonSeparatedList<ArgumentSyntax>(Argument(
                        ConditionalExpression(
                            BinaryExpression(
                                SyntaxKind.EqualsExpression,
                                MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    ThisExpression(),
                                    IdentifierName(memberName)
                                ),
                                LiteralExpression(
                                    SyntaxKind.NullLiteralExpression
                                )
                            ),
                            LiteralExpression(
                                SyntaxKind.NumericLiteralExpression,
                                Literal(0)
                            ),
                            InvocationExpression(
                                MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    MemberAccessExpression(
                                        SyntaxKind.SimpleMemberAccessExpression,
                                        MemberAccessExpression(
                                            SyntaxKind.SimpleMemberAccessExpression,
                                            ThisExpression(),
                                            IdentifierName(memberName)
                                        ),
                                        IdentifierName("Value")
                                    ),
                                    IdentifierName("GetHashCode")
                                )
                            )
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
            foreach (var member in Misc.EnumerateInstanceMembers(typeSymbol))
            {
                StatementSyntax stmt;
                if (!member.IsNullableType())
                    stmt = GenerateHashCodeAddNonNullableMember(member);
                else
                    stmt = GenerateHashCodeAddNullableMember(member);

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

        StatementSyntax GenerateHashCodeAddNullableMember(MemberSymbol member)
        {
            if (member.GetTypeSymbol().IsReferenceType)
                return GenerateHashCodeAddNullableReferenceTypeMember(member);
            else
                return GenerateHashCodeAddNullableValueTypeMember(member);
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

        GeneratorResult GenerateStruct(ITypeSymbol typeSymbol)
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
                return GenerateStruct(typeSymbol);
            }

            throw new PretuneGeneralException();
        }
    }
}
