using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Pretune.Abstractions;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Pretune.Generators
{   
    class ConstructorGenerator : IGenerator
    {
        IIdentifierConverter identifierConverter;

        public ConstructorGenerator(IIdentifierConverter identifierConverter)
        {
            this.identifierConverter = identifierConverter;
        }

        public bool ShouldApply(TypeDeclarationSyntax typeDecl, SemanticModel model)
        {
            if (!(typeDecl is ClassDeclarationSyntax) && !(typeDecl is StructDeclarationSyntax))
                return false;

            return Misc.HasPretuneAttribute(typeDecl, model, "AutoConstructor");
        }

        public GeneratorResult Generate(ITypeSymbol typeSymbol)
        {
            // constructor ingredients
            var parameters = new List<SyntaxNodeOrToken>();
            var statements = new List<StatementSyntax>();

            foreach (var field in Misc.EnumerateFields(typeSymbol))
            {
                var memberName = field.Name;
                var paramName = identifierConverter.ConvertMemberToParam(memberName);

                if (parameters.Count != 0)
                    parameters.Add(Token(SyntaxKind.CommaToken));

                var parameter = Parameter(Identifier(paramName))
                    .WithType(Misc.GetFieldTypeSyntax(field));

                parameters.Add(parameter);

                var statement = ExpressionStatement(
                    AssignmentExpression(
                        SyntaxKind.SimpleAssignmentExpression,
                        MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            ThisExpression(),
                            IdentifierName(memberName)
                        ),
                        IdentifierName(paramName)
                    )
                );

                statements.Add(statement);
            }

            var constructorDecl = ConstructorDeclaration(typeSymbol.Name)
                .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
                .WithParameterList(ParameterList(SeparatedList<ParameterSyntax>(parameters)))
                .WithBody(Block(statements));

            return new GeneratorResult(
                ImmutableArray<BaseTypeSyntax>.Empty, 
                ImmutableArray.Create<MemberDeclarationSyntax>(constructorDecl));
        }
    }
}
