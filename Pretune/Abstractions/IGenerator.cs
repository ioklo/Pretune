using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Pretune.Abstractions
{
    struct GeneratorResult
    {
        public ImmutableArray<BaseTypeSyntax> BaseTypes { get; }
        public ImmutableArray<MemberDeclarationSyntax> MemberDecls { get; }

        public GeneratorResult(ImmutableArray<BaseTypeSyntax> baseTypes, ImmutableArray<MemberDeclarationSyntax> MemberDecls)
        {
            this.BaseTypes = baseTypes;
            this.MemberDecls = MemberDecls;
        }
    }

    // input: class symbol (semantic)
    // output: member decls (syntax)
    interface IGenerator
    {
        bool ShouldApply(TypeDeclarationSyntax typeDecl, SemanticModel model);
        GeneratorResult Generate(ITypeSymbol typeSymbol);
    }
}