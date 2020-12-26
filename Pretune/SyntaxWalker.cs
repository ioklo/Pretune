using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Pretune.Abstractions;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Pretune
{
    partial class SyntaxWalker
    {
        class Frame
        {
            // using과 namespace를 보존한다
            public SyntaxList<MemberDeclarationSyntax> Members { get; private set; }

            public Frame()
            {
                Members = List<MemberDeclarationSyntax>();
            }

            public void AddMember(MemberDeclarationSyntax member)
            {
                Members = Members.Add(member);
            }
        }
    }

    partial class SyntaxWalker : CSharpSyntaxWalker
    {
        SemanticModel model;
        ImmutableArray<IGenerator> generators;
        public CompilationUnitSyntax? CompilationUnitSyntax { get; private set; }
        Frame frame;
        public bool NeedSave { get; private set; }

        public SyntaxWalker(SemanticModel model, ImmutableArray<IGenerator> generators)
        {
            this.model = model;
            this.generators = generators;

            CompilationUnitSyntax = null;
            frame = new Frame();
            NeedSave = false;
        }

        public override void VisitCompilationUnit(CompilationUnitSyntax node)
        {
            // CompilationUnitSyntax = CompilationUnit(frame.)
            base.VisitCompilationUnit(node); // collect members

            CompilationUnitSyntax = CompilationUnit(
                node.Externs, node.Usings, node.AttributeLists, frame.Members
            ).NormalizeWhitespace();
        }

        public override void VisitNamespaceDeclaration(NamespaceDeclarationSyntax node)
        {
            var prevFrame = frame;

            frame = new Frame();
            base.VisitNamespaceDeclaration(node);
            var member = NamespaceDeclaration(node.Name, node.Externs, node.Usings, frame.Members);

            frame = prevFrame;
            frame.AddMember(member);
        }

        public override void VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            var typeSymbol = model.GetDeclaredSymbol(node);
            
            if (typeSymbol == null)
                throw new PretuneGeneralException();

            var baseTypes = new List<BaseTypeSyntax>();
            var memberDecls = new List<MemberDeclarationSyntax>();

            foreach (var generator in generators)
            {
                if (!generator.ShouldApply(typeSymbol)) continue;

                var result = generator.Generate(typeSymbol);

                baseTypes.AddRange(result.BaseTypes);
                memberDecls.AddRange(result.MemberDecls);
            }

            if (baseTypes.Count == 0 && memberDecls.Count == 0)
                return;

            NeedSave = true;

            var classDecl = ClassDeclaration(node.Identifier)
                .WithTypeParameterList(node.TypeParameterList)
                .WithModifiers(node.Modifiers);

            if (0 < baseTypes.Count)
                classDecl = classDecl.WithBaseList(BaseList(SeparatedList(baseTypes)));

            if (0 < memberDecls.Count)
                classDecl = classDecl.WithMembers(List(memberDecls));

            frame.AddMember(classDecl);
        }
    }
}

