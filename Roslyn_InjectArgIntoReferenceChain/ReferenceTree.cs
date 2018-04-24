using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;

namespace Roslyn_InjectArgIntoReferenceChain
{
    public sealed class ReferenceTree
    {
        public ReferenceTree(string methodName, string path, SyntaxNode node, IEnumerable<(ISymbol Symbol, SyntaxNode Node, ReferenceLocation Location, string Method)> references)
        {
            MethodName = methodName;
            Path = path;
            Node = node;
            References = references;
        }
        public readonly string MethodName;
        public readonly string Path;
        public readonly SyntaxNode Node;
        public IEnumerable<(ISymbol Symbol, SyntaxNode Node, ReferenceLocation Location, string Method)> References { get; set; }
    }
}
