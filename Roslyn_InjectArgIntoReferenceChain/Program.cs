using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
namespace Roslyn_InjectArgIntoReferenceChain
{
    class Program
    {
        // The initial space in qualifiedName is required for it to render correctly.
        private static NameSyntax qualifiedName = SyntaxFactory.ParseName(" Qualified.Using.Directive.Name");
        private static UsingDirectiveSyntax usingDirective = SyntaxFactory.UsingDirective(qualifiedName);
        static void Main(string[] args)
        {
            string solutionPath = @"Your Solution Path";
            string filePath = @"The Path the originating class reference";
            string typeName = "Fully.Qualified.TypeName for starting reference";
            string methodName = "Starting Reference Method";
            string parmTypeName = "TypeName of Parm for Method Signature";
            string parmName = "Name of parm injected into Method Signature";

            ProcessSolution(solutionPath, filePath, typeName, methodName, parmTypeName, parmName);
        }

        private static void ProcessSolution(string solutionPath, string filePath, string typeName, string methodName, string parmTypeName, string parmName)
        {
            var finder = new ReferenceFinder(solutionPath);
            // This contains the Root Node for your type / method name and a collections of references in the solution to it.  (Gen 1 references)
            var tree = finder.GetReferenceTree(filePath, typeName, methodName);
                        
            // This will give you a collection of the changes from all generations by the File Location
            // This should allow you to make all the changes to a file in single pass and avoid recompiling
            var forest = FindReferences(finder, tree)
                .SelectMany(refTree => refTree.References
                    .Select(refInfo => (Path: string.Intern(refInfo.Location.Document.FilePath),
                              Node: refInfo.Node,
                              ParentMethod: string.Intern(refTree.MethodName),
                              Method: refInfo.Method)))
                .GroupBy(refInfo => refInfo.Path);
             
            // For each file with references to be changed
            foreach (var group in forest)
            {
                // All the changes in the group will be to the same file and thus the same Root Node
                var rootNode = group.Select(x => x.Node.SyntaxTree.GetRoot()).First();
                var writer = new ReferenceWriter(new HashSet<string>(group.Select(x => x.ParentMethod).Concat(group.Select(x => x.Method)).Distinct()), parmTypeName, parmName);
                WriteChangesToFile(group.Key, WriteUsings((writer.Visit(rootNode).SyntaxTree.GetRoot() as CompilationUnitSyntax)).ToFullString());
            } 
        }

        private static IEnumerable<ReferenceTree> FindReferences(ReferenceFinder finder, ReferenceTree woods)
        {
            // Yield the Method Reference itself
            yield return woods;

            // For each reference to this reference
            foreach (var tree in woods.References.AsParallel().WithDegreeOfParallelism(10).Select(x =>
                                                        finder.GetReferenceTree(x.Location.Document.FilePath,
                                                        x.Symbol.GetQualifiedClassName(),
                                                        x.Node is MethodDeclarationSyntax ? (x.Node as MethodDeclarationSyntax).Identifier.ValueText :
                                                        (x.Node as ConstructorDeclarationSyntax).Identifier.ValueText))
                                                    .Distinct())
            {
                // Yield the immediate child references
                yield return tree;

                // Recursively get and yield all referencing methods  
                foreach (var branch in tree.References.AsParallel().WithDegreeOfParallelism(10)
                                    .SelectMany(x => FindReferences(
                                            finder,
                                            finder.GetReferenceTree(
                                                    x.Location.Document.FilePath,
                                                    x.Symbol.GetQualifiedClassName(),
                                                    x.Node is MethodDeclarationSyntax ? (x.Node as MethodDeclarationSyntax).Identifier.ValueText :
                                                    (x.Node as ConstructorDeclarationSyntax).Identifier.ValueText))))
                {
                    yield return branch;
                }
            }
        }
        // Determine if the changes made required the addition of a new Using Directive
        private static SyntaxNode WriteUsings(CompilationUnitSyntax root) =>
            !root.Usings.Select(d => d.Name.ToString()).Any(u => u == qualifiedName.ToString()) ? root?.AddUsings(usingDirective).NormalizeWhitespace() : root;

        // Write changes to file
        private static void WriteChangesToFile(string filePath, string changes) => File.WriteAllText(filePath, changes);
    }
}
