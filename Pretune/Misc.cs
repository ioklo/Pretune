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
        public static TypeSyntax GetFieldTypeSyntax(ISymbol symbol)
        {
            var syntaxRef = symbol.DeclaringSyntaxReferences.FirstOrDefault();
            if (syntaxRef == null)
                throw new PretuneGeneralException();

            var node = syntaxRef.GetSyntax();            
            
            if (node is VariableDeclaratorSyntax varDeclarator)
            {
                // FieldDeclSyntax -> VarDeclarationSyntax -> VarDeclaratorSyntax
                var varDecl = varDeclarator.Parent as VariableDeclarationSyntax;
                if (varDecl == null)
                    throw new PretuneGeneralException();

                return varDecl.Type;
            }
            else if (node is PropertyDeclarationSyntax propDecl)
            {
                return propDecl.Type;
            }

            throw new PretuneGeneralException();
        }

        public static bool HasPretuneAttribute(ISymbol symbol, string name)
        {
            return symbol.GetAttributes().Any(attrData => IsPretuneAttribute(attrData, name));
        }

        public static bool IsPretuneAttribute(AttributeData attrData, string name)
        {
            if (attrData.AttributeClass == null) return false;

            return attrData.AttributeClass.ToString() == $"Pretune.{name}Attribute" ||
                attrData.AttributeClass.Name == name;
        }

        public static IEnumerable<ISymbol> EnumerateFields(ITypeSymbol typeSymbol)
        {
            foreach (var member in typeSymbol.GetMembers())
            {
                if (member is IFieldSymbol fieldMember)
                {
                    if (fieldMember.AssociatedSymbol is IPropertySymbol associatedMember)
                    {
                        yield return associatedMember;
                    }
                    else
                    {
                        yield return fieldMember;
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
