using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Pretune.Abstractions;
using System;
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
        Dictionary<TypeDeclarationSyntax, List<IGenerator>> typeDeclGeneratorsInfo;

        public CompilationUnitSyntax? CompilationUnitSyntax { get; private set; }
        Frame frame;
        public bool NeedSave { get; private set; }

        public SyntaxWalker(SemanticModel model, Dictionary<TypeDeclarationSyntax, List<IGenerator>> typeDeclGeneratorsInfo)
        {
            this.model = model;
            this.typeDeclGeneratorsInfo = typeDeclGeneratorsInfo;

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

        Frame ExecInNewFrame(Action action)
        {            
            var newFrame = new Frame();

            var prevFrame = frame;
            frame = newFrame;

            try
            {
                action.Invoke();
                return newFrame;
            }
            finally 
            {
                frame = prevFrame;
            }            
        }

        public override void VisitNamespaceDeclaration(NamespaceDeclarationSyntax node)
        {
            var newFrame = ExecInNewFrame(() => base.VisitNamespaceDeclaration(node));

            if (newFrame.Members.Count != 0)
            {
                var namespaceDecl = NamespaceDeclaration(node.Name, node.Externs, node.Usings, newFrame.Members);
                frame.AddMember(namespaceDecl);
            }
        }        

        public override void VisitStructDeclaration(StructDeclarationSyntax node)
        {
            var newFrame = ExecInNewFrame(() => base.VisitStructDeclaration(node));

            var baseTypes = new List<BaseTypeSyntax>();
            var memberDecls = new List<MemberDeclarationSyntax>(newFrame.Members);

            if (typeDeclGeneratorsInfo.TryGetValue(node, out var generators))
            {
                var typeSymbol = model.GetDeclaredSymbol(node);
                if (typeSymbol == null) throw new PretuneGeneralException();

                foreach (var generator in generators)
                {
                    var result = generator.Generate(typeSymbol);

                    baseTypes.AddRange(result.BaseTypes);
                    memberDecls.AddRange(result.MemberDecls);
                }
            }

            if (baseTypes.Count == 0 && memberDecls.Count == 0)
                return;

            NeedSave = true;

            var structDecl = StructDeclaration(node.Identifier)
                .WithTypeParameterList(node.TypeParameterList)
                .WithModifiers(node.Modifiers);

            if (0 < baseTypes.Count)
                structDecl = structDecl.WithBaseList(BaseList(SeparatedList(baseTypes)));

            if (0 < memberDecls.Count)
                structDecl = structDecl.WithMembers(List(memberDecls));

            frame.AddMember(structDecl);
        }

        public override void VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            var newFrame = ExecInNewFrame(() => base.VisitClassDeclaration(node));

            var baseTypes = new List<BaseTypeSyntax>();
            var memberDecls = new List<MemberDeclarationSyntax>(newFrame.Members);

            if (typeDeclGeneratorsInfo.TryGetValue(node, out var generators))
            {
                var typeSymbol = model.GetDeclaredSymbol(node);
                if (typeSymbol == null) throw new PretuneGeneralException();

                foreach (var generator in generators)
                {
                    var result = generator.Generate(typeSymbol);

                    baseTypes.AddRange(result.BaseTypes);
                    memberDecls.AddRange(result.MemberDecls);
                }
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

