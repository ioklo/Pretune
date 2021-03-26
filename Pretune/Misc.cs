using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Pretune
{
    static class Misc
    {
        
        public static bool HasPretuneAttribute(TypeDeclarationSyntax typeDecl, SemanticModel model, string name)
        {
            foreach (var attrList in typeDecl.AttributeLists)
            {
                foreach (var attr in attrList.Attributes)
                {
                    var symbolInfo = model.GetSymbolInfo(attr);
                    if (symbolInfo.Symbol is IMethodSymbol methodSymbol) // attribute constructor..
                        if (IsPretuneAttribute(methodSymbol.ContainingType, name))
                            return true;

                    // fallback
                    if (attr.ToString() == name)
                        return true;
                }
            }

            return false;
        }        

        public static bool IsPretuneAttribute(INamedTypeSymbol attributeClass, string name)
        {
            if (attributeClass == null) return false;

            return attributeClass.ToString() == $"Pretune.{name}Attribute" ||
                attributeClass.Name == name;
        }

        public static IEnumerable<MemberSymbol> EnumerateInstanceMembers(ITypeSymbol typeSymbol)
        {
            foreach (var member in typeSymbol.GetMembers())
            {
                if (member is IFieldSymbol fieldMember)
                {
                    if (member.IsStatic) continue;

                    if (fieldMember.AssociatedSymbol is IPropertySymbol associatedMember)
                    {
                        yield return new PropertyMemberSymbol(associatedMember);
                    }
                    else
                    {
                        yield return new FieldMemberSymbol(fieldMember);
                    }
                }
                else
                {
                    continue;
                }
            }
        }        
    }
}
