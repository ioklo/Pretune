using Microsoft.CodeAnalysis.CSharp;
using Pretune.Abstractions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Pretune
{
    class CamelCaseIdentifierConverter : IIdentifierConverter
    {
        public string ConvertMemberToParam(string memberIdentifier)
        {
            Debug.Assert(0 < memberIdentifier.Length);
            
            var id = char.ToLower(memberIdentifier[0]) + memberIdentifier.Substring(1);
            var token = ParseToken(id);

            if (token.IsKeyword())
                return "@" + id;
            else
                return id;
        }

        public string ConvertMemberToProperty(string memberIdentifier)
        {
            Debug.Assert(0 < memberIdentifier.Length);

            var id = char.ToUpper(memberIdentifier[0]) + memberIdentifier.Substring(1);
            var token = ParseToken(id);

            if (token.IsKeyword())
                return "@" + id;
            else
                return id;
        }
    }
}
