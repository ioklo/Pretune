using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Pretune.Abstractions;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Pretune.Generators
{
    class INotifyPropertyChangedGenerator : IGenerator
    {
        IIdentifierConverter identifierConverter;
        public INotifyPropertyChangedGenerator(IIdentifierConverter identifierConvert)
        {
            this.identifierConverter = identifierConvert;
        }

        public bool ShouldApply(ITypeSymbol typeSymbol)
        {
            foreach (var attributeData in typeSymbol.GetAttributes())
                if (attributeData.AttributeClass != null && attributeData.AttributeClass.Name == "ImplementINotifyPropertyChanged")
                    return true;

            return false;
        }

        public GeneratorResult Generate(ITypeSymbol typeSymbol)
        {
            var memberDecls = new List<MemberDeclarationSyntax>();

            var eventDecl = ParseMemberDeclaration("public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;");
            if (eventDecl == null)
                throw new PretuneGeneralException("internal error");
            memberDecls.Add(eventDecl);

            // 프로퍼티 별로 순회한다
            foreach (var field in Misc.EnumerateFields(typeSymbol))
            {
                string memberName = field.Name;
                string propertyName = identifierConverter.ConvertMemberToProperty(memberName);
                string typeName = Misc.GetFieldTypeSyntax(field).ToString();

                var propDeclText = @$"
public {typeName} {propertyName}
{{
    get => {memberName};
    set
    {{
        if (!System.Collections.Generic.EqualityComparer<{typeName}>.Default.Equals({memberName}, value))
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
            
            var baseType = ParseTypeName("System.ComponentModel.INotifyPropertyChanged");

            return new GeneratorResult(
                ImmutableArray.Create<BaseTypeSyntax>(SimpleBaseType(baseType)),
                memberDecls.ToImmutableArray());
        }
    }
}
