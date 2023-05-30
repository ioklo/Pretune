using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Pretune
{
    class MemberSymbolEqualityComparer : IEqualityComparer<MemberSymbol>
    {
        public static readonly MemberSymbolEqualityComparer Default = new MemberSymbolEqualityComparer();
        MemberSymbolEqualityComparer() { }

        public bool Equals([AllowNull] MemberSymbol x, [AllowNull] MemberSymbol y)
        {
            if (x == null && y == null) return true;
            if (x == null || y == null) return false;

            return x.Equals(y);
        }

        public int GetHashCode([DisallowNull] MemberSymbol obj)
        {
            return obj.GetHashCode();
        }
    }

    abstract class MemberSymbol : IEquatable<MemberSymbol>
    {
        public abstract ITypeSymbol GetTypeSymbol();
        public abstract ImmutableArray<AttributeData> GetAttributes();
        public bool IsNullableType()
        {
            var typeSymbol = GetTypeSymbol();
            return typeSymbol.NullableAnnotation == NullableAnnotation.Annotated;
        }

        protected abstract ISymbol GetSymbol();
        
        public string GetName()
        {
            return GetSymbol().Name;
        }

        public TypeSyntax GetFieldTypeSyntax()
        {
            var symbol = GetSymbol();

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

        public override bool Equals(object? obj) => obj is MemberSymbol memberSymbol && Equals(memberSymbol);
        public override int GetHashCode()
        {
            var symbol = GetSymbol();
            return SymbolEqualityComparer.Default.GetHashCode(symbol);
        }

        public bool Equals([AllowNull] MemberSymbol other)
        {
            if (other == null) return false;

            var symbol = GetSymbol();
            var otherSymbol = other.GetSymbol();

            return SymbolEqualityComparer.Default.Equals(symbol, otherSymbol);
        }
    }

    class FieldMemberSymbol : MemberSymbol
    {
        IFieldSymbol fieldSymbol;

        public FieldMemberSymbol(IFieldSymbol fieldSymbol) { this.fieldSymbol = fieldSymbol; }

        protected override ISymbol GetSymbol() => fieldSymbol;        

        public override ITypeSymbol GetTypeSymbol()
        {
            return fieldSymbol.Type;
        }

        public override ImmutableArray<AttributeData> GetAttributes()
        {
            return fieldSymbol.GetAttributes();
        }
    }

    class PropertyMemberSymbol : MemberSymbol
    {
        IPropertySymbol propSymbol;
        public PropertyMemberSymbol(IPropertySymbol propSymbol) { this.propSymbol = propSymbol; }

        public override ITypeSymbol GetTypeSymbol() => propSymbol.Type;
        protected override ISymbol GetSymbol() => propSymbol;
        public override ImmutableArray<AttributeData> GetAttributes()
        {
            return propSymbol.GetAttributes();
        }
    }

}