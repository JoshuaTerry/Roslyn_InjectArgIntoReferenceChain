using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Roslyn_InjectArgIntoReferenceChain
{
    public sealed class ReferenceWriter : CSharpSyntaxRewriter
    {   
        private readonly HashSet<string> methodNames = null;
        private readonly string parmTypeName;
        private readonly string parmName;

        public ReferenceWriter(HashSet<string> methodNames, string parmTypeName, string parmName)
        {
            this.methodNames = methodNames;
            this.parmTypeName = parmTypeName;
            this.parmName = parmName;
        }

        // Normalize the whitespace, otherwise the writer will reformat the code
        public override SyntaxNode VisitNamespaceDeclaration(NamespaceDeclarationSyntax node) =>
            base.VisitNamespaceDeclaration(node).NormalizeWhitespace();

        // Add method parameter to qualifying method
        public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node) =>
            NeedsMethodParameter(node) ? base.VisitMethodDeclaration(node.PrependParameter(CreateParameter()).NormalizeWhitespace()) : base.VisitMethodDeclaration(node);

        // Determine if a method needs parameter added to signature
        private bool NeedsMethodParameter(MethodDeclarationSyntax node) =>
            node.ParameterList != null && !node.ParameterList.Parameters.Any(predicate => predicate.Type.ToString().EndsWith(parmTypeName)) && methodNames.Contains(node.Identifier.ValueText);

        // Add argument to qualifying object creations:  new MyClass() => new MyClass(parmName);
        public override SyntaxNode VisitObjectCreationExpression(ObjectCreationExpressionSyntax node) =>
            NeedsObjectCreationParmeter(node) ? base.VisitObjectCreationExpression(node.PrependArgument(CreateArgument()).NormalizeWhitespace()) : base.VisitObjectCreationExpression(node);

        // Determine if Object Creation needs argument added to signature
        private bool NeedsObjectCreationParmeter(ObjectCreationExpressionSyntax node) =>
            methodNames.Contains(node.Type.ToString()) && node.ArgumentList?.Arguments.Count == 0;

        // Add method parameter to qualifying member invocations:  Foo.Bar() => Foo.Bar(parmName)
         public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            if (node.Expression is MemberAccessExpressionSyntax || node.Expression is IdentifierNameSyntax)
            {
                var invocationName = node.Expression is MemberAccessExpressionSyntax ? (node.Expression as MemberAccessExpressionSyntax).Name.Identifier.ValueText : (node.Expression as IdentifierNameSyntax).Identifier.ValueText;
                 
                // If the invocation is one we need to change
                if (methodNames.Contains(invocationName))
                {
                    // Create a new method Argument List or Prepend your argument to the existing Argument List
                    if (node.ArgumentList == null || !node.ArgumentList.Arguments.Any(x => x.ToString() == parmName))
                    {
                        var arg = SyntaxFactory.Argument(SyntaxFactory.ParseName(parmName));
                        var args = node.ArgumentList == null ? SyntaxFactory.SeparatedList<ArgumentSyntax>(new[] { arg }) : SyntaxFactory.SeparatedList<ArgumentSyntax>(node.ArgumentList.Arguments.ToArray().Prepend(arg));

                        return SyntaxFactory.InvocationExpression(node.Expression, SyntaxFactory.ArgumentList(args));
                    }
                }
            } 

            return base.VisitInvocationExpression(node);
        }

        private ArgumentSyntax CreateArgument() =>
           SyntaxFactory.Argument(SyntaxFactory.ParseName(parmName));
        private ParameterSyntax CreateParameter() =>
           SyntaxFactory.Parameter(SyntaxFactory.Identifier($"{parmTypeName} {parmName}"));  
    }
}
