using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
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
        public CompilationUnitSyntax? CompilationUnitSyntax { get; private set; }
        Frame frame;
        public bool NeedSave { get; private set; }

        public SyntaxWalker()
        {
            CompilationUnitSyntax = null;
            frame = new Frame();
            NeedSave = false;
        }

        public override void VisitCompilationUnit(CompilationUnitSyntax node)
        {
            // CompilationUnitSyntax = CompilationUnit(frame.)
            base.VisitCompilationUnit(node); // collect members

            CompilationUnitSyntax = CompilationUnit(
                node.Externs, node.Usings, node.AttributeLists, frame.Members).NormalizeWhitespace();
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
            bool bAutoConstructor = false, bImplementINotifyPropertyChanged = false;
            foreach(var attrList in node.AttributeLists)
            {
                foreach (var attr in attrList.Attributes)
                {
                    var name = attr.Name.ToString();

                    if (name == "AutoConstructor")
                        bAutoConstructor = true;
                    else if (name == "ImplementINotifyPropertyChanged")
                        bImplementINotifyPropertyChanged = true;
                }
            }

            if (!bAutoConstructor && !bImplementINotifyPropertyChanged) return;
            NeedSave = true;

            // 1. body없는 get; set; property 목록 얻어오기
            // 2. fieldDeclaration
            var members = new List<(TypeSyntax, SyntaxToken)>();
            foreach(var member in node.Members)
            {
                switch(member)
                {
                    case PropertyDeclarationSyntax propertyMember:
                        if (propertyMember.AccessorList != null)
                        {
                            if (propertyMember.AccessorList.Accessors.All(accessor => accessor.Body == null && accessor.ExpressionBody == null))
                            {
                                members.Add((propertyMember.Type, propertyMember.Identifier));
                            }
                        }
                        break;

                    case FieldDeclarationSyntax fieldMember:
                        foreach (var variable in fieldMember.Declaration.Variables)
                            members.Add((fieldMember.Declaration.Type, variable.Identifier));
                        break;
                }
            }

            var memberDecls = new List<MemberDeclarationSyntax>();                

            if (bAutoConstructor)
            {
                // parameters
                var parameters = new List<SyntaxNodeOrToken>();
                var statements = new List<StatementSyntax>();

                foreach (var member in members)
                {
                    var memberName = member.Item2.ToString();

                    if (memberName.Length == 0)
                        throw new PretuneGeneralException("internal error");

                    string paramName = "@" + char.ToLower(memberName[0]) + memberName.Substring(1);

                    if (parameters.Count != 0)
                        parameters.Add(Token(SyntaxKind.CommaToken));

                    var parameter = Parameter(Identifier(paramName)).WithType(member.Item1);
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

                var constructorDecl = ConstructorDeclaration(node.Identifier)
                    .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
                    .WithParameterList(ParameterList(SeparatedList<ParameterSyntax>(parameters)))
                    .WithBody(Block(statements));

                memberDecls.Add(constructorDecl);
            }

            if (bImplementINotifyPropertyChanged)
            {
                var eventDecl = ParseMemberDeclaration("public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;");
                if (eventDecl == null)
                    throw new PretuneGeneralException("internal error");
                memberDecls.Add(eventDecl);

                // 프로퍼티 별로 순회한다
                foreach (var member in members)
                {
                    var memberName = member.Item2.ToString();

                    if (memberName.Length == 0)
                        throw new PretuneGeneralException("internal error");

                    string propertyName = char.ToUpper(memberName[0]) + memberName.Substring(1);
                    string typeName = member.Item1.ToString();

                    var propDeclText = @$"
public {typeName} {propertyName}
{{
    get => {memberName};
    set
    {{
        if (!{memberName}.Equals(value))
        {{
            {memberName} = value;
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(""{propertyName}""));
        }}
    }}
}}";
                    var propDecl = ParseMemberDeclaration(propDeclText);
                    if (propDecl == null) throw new PretuneGeneralException("internal error");

                    memberDecls.Add(propDecl);
                }
            }

            var classDecl = ClassDeclaration(node.Identifier)
                .WithTypeParameterList(node.TypeParameterList)
                .WithModifiers(node.Modifiers)
                .WithMembers(List(memberDecls));

            frame.AddMember(classDecl);
                
        }
    }
}

