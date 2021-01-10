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
