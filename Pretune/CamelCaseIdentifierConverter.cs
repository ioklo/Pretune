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

            if (memberIdentifier.StartsWith("@"))
            {
                var id = char.ToLower(memberIdentifier[1]) + memberIdentifier.Substring(2);
                return "@" + id;
            }
            else
            {
                var id = char.ToLower(memberIdentifier[0]) + memberIdentifier.Substring(1);
                var token = ParseToken(id);

                if (token.IsKeyword())
                    return "@" + id;
                else
                    return id;
            }
        }

        public string ConvertMemberToProperty(string memberIdentifier)
        {
            Debug.Assert(0 < memberIdentifier.Length);

            if (memberIdentifier.StartsWith("@"))
            {
                return "@" + char.ToUpper(memberIdentifier[1]) + memberIdentifier.Substring(2);
            }
            else
            {
                return char.ToUpper(memberIdentifier[0]) + memberIdentifier.Substring(1);
            }
        }
    }
}
