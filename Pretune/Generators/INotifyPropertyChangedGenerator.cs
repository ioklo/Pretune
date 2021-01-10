using Microsoft.CodeAnalysis;
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
    class INotifyPropertyChangedGenerator : IGenerator
    {
        IIdentifierConverter identifierConverter;
        public INotifyPropertyChangedGenerator(IIdentifierConverter identifierConvert)
        {
            this.identifierConverter = identifierConvert;
        }

        public bool ShouldApply(TypeDeclarationSyntax typeDecl, SemanticModel model)
        {
            if (!(typeDecl is ClassDeclarationSyntax))
                return false;

            return Misc.HasPretuneAttribute(typeDecl, model, "ImplementINotifyPropertyChanged");
        }

        IEnumerable<IFieldSymbol> GetTargetFields(ITypeSymbol typeSymbol)
        {
            return typeSymbol.GetMembers().OfType<IFieldSymbol>()
                .Where(field => field.AssociatedSymbol == null);
        }

        ICollection<(IPropertySymbol Prop, AttributeData DependsOnAttr)> GetDependsOnProperties(ITypeSymbol typeSymbol)
        {
            var autoProps = typeSymbol.GetMembers()
                .OfType<IFieldSymbol>()
                .Select(field => field.AssociatedSymbol)
                .OfType<IPropertySymbol>()
                .ToHashSet(SymbolEqualityComparer.Default);

            return typeSymbol.GetMembers().OfType<IPropertySymbol>()
                .Where(prop =>
                {
                    return prop.DeclaredAccessibility == Accessibility.Public &&
                        !autoProps.Contains(prop);
                })
                .SelectMany(prop => 
                    prop.GetAttributes().Where(attrData => attrData.AttributeClass != null && Misc.IsPretuneAttribute(attrData.AttributeClass, "DependsOn"))
                                        .Select(attrData => (Prop: prop, DependsOnAttr: attrData))
                ).ToList();
        }

        Dictionary<IFieldSymbol, List<IPropertySymbol>> GetDependsOnInfos(ITypeSymbol typeSymbol, List<IFieldSymbol> fields)
        {
            var fieldsByName = fields.ToDictionary(field => field.Name);

            var dict = new Dictionary<IFieldSymbol, List<IPropertySymbol>>(SymbolEqualityComparer.Default);
            foreach (var info in GetDependsOnProperties(typeSymbol))
            {
                // DependsOn("fieldName", "fieldName", ...)
                foreach(var typedConstant in info.DependsOnAttr.ConstructorArguments[0].Values)
                {
                    if (typedConstant.Value is string fieldName)
                    {
                        if (!fieldsByName.TryGetValue(fieldName, out var field)) continue;
                        
                        if (!dict.TryGetValue(field, out var props))
                        {
                            props = new List<IPropertySymbol>();
                            dict.Add(field, props);
                        }

                        props.Add(info.Prop);
                    }
                }
            }

            return dict;
        }

        StatementSyntax CreatePropertyChangedInvocation(string propName)
        {
            return ParseStatement(@$"PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(""{propName}""));");
        }

        MemberDeclarationSyntax CreatePropertyDeclaration(string typeName, string propertyName, string memberName, ICollection<StatementSyntax> extraInvocations)
        {
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
            {string.Join(Environment.NewLine, extraInvocations)}
        }}
    }}
}}";
            var propDecl = ParseMemberDeclaration(propDeclText);
            if (propDecl == null) throw new PretuneGeneralException("internal error");

            return propDecl;
        }

        public GeneratorResult Generate(ITypeSymbol typeSymbol)
        {
            var fields = GetTargetFields(typeSymbol).ToList();
            var dependsOnInfos = GetDependsOnInfos(typeSymbol, fields);

            var memberDecls = new List<MemberDeclarationSyntax>();

            var eventDecl = ParseMemberDeclaration("public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;");
            if (eventDecl == null)
                throw new PretuneGeneralException("internal error");
            memberDecls.Add(eventDecl);            
            
            foreach (var field in fields)
            {
                string memberName = field.Name;
                string propertyName = identifierConverter.ConvertMemberToProperty(memberName);
                string typeName = Misc.GetFieldTypeSyntax(field).ToString();

                ICollection<StatementSyntax> extraInvocations;
                if (dependsOnInfos.TryGetValue(field, out var props))
                    extraInvocations = props.Select(prop => CreatePropertyChangedInvocation(prop.Name)).ToList();
                else
                    extraInvocations = Array.Empty<StatementSyntax>();

                var propDecl = CreatePropertyDeclaration(typeName, propertyName, memberName, extraInvocations);
                memberDecls.Add(propDecl);
            }
            
            var baseType = ParseTypeName("System.ComponentModel.INotifyPropertyChanged");

            return new GeneratorResult(
                ImmutableArray.Create<BaseTypeSyntax>(SimpleBaseType(baseType)),
                memberDecls.ToImmutableArray());
        }
    }
}
