using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Roslyn_InjectArgIntoReferenceChain
{
    public static class Extensions
    {
        // Return the fully qualified namespace for a reference Symbol
        public static string GetFullNamespace(this ISymbol symbol)
        {
            string result = string.Empty;
            if (symbol.ContainingNamespace != null && !string.IsNullOrEmpty(symbol.ContainingNamespace.Name))
            {
                string restOfResult = symbol.ContainingNamespace.GetFullNamespace();
                result = symbol.ContainingNamespace.Name;

                if (!string.IsNullOrEmpty(restOfResult))
                    result = restOfResult + '.' + result;
            }
            return result;
        }
        // Return the fully qualified class name for a reference Symbol
        public static string GetQualifiedClassName(this ISymbol symbol) =>
            (symbol != null && symbol.ContainingSymbol != null && symbol.ContainingType.TypeKind == TypeKind.Class) ? GetFullNamespace(symbol) + $".{symbol.ContainingType.Name}" : string.Empty;
        
        // Prepend a method parameter to the beginning of a method signature 
        public static MethodDeclarationSyntax PrependParameter(this MethodDeclarationSyntax node, ParameterSyntax parameterSyntax)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));

            return node.WithParameterList(SyntaxFactory.ParameterList(
                new SeparatedSyntaxList<ParameterSyntax>()
                    .Add(parameterSyntax)
                    .AddRange(node.ParameterList.Parameters)));
        }
        // Prepend an argument to an object creation argument list
        public static ObjectCreationExpressionSyntax PrependArgument(this ObjectCreationExpressionSyntax node, ArgumentSyntax newArgumentSyntax)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));

            return node.WithArgumentList(PrependArgument(newArgumentSyntax, node.ArgumentList?.Arguments));
        }
        // Prepend an argument to the beginning of an argument list
        private static ArgumentListSyntax PrependArgument(ArgumentSyntax newArgumentSyntax, IEnumerable<ArgumentSyntax> existingArgumentSyntaxes)
        {
            existingArgumentSyntaxes = existingArgumentSyntaxes ?? Enumerable.Empty<ArgumentSyntax>();

            return SyntaxFactory.ArgumentList()
                .AddArguments(newArgumentSyntax)
                .AddArguments(existingArgumentSyntaxes.ToArray());
        }
    }
}
