using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.MSBuild;

namespace Roslyn_InjectArgIntoReferenceChain
{
    public sealed class ReferenceFinder
    {
        private ConcurrentDictionary<ReferenceLocation, object> processedReferenceLocations = new ConcurrentDictionary<ReferenceLocation, object>();
        public ReferenceFinder(string solutionPath) =>
            Solution = SetSolutionInfo(solutionPath).GetAwaiter().GetResult();
        public ReferenceTree GetReferenceTree(string path, string typeName, string methodName) =>
            new ReferenceTree(methodName, path, GetNodeFromSymbol(typeName), GetReferenceNodes(GetReferenceSymbols(GetTypeSymbols(typeName), methodName)).SelectMany(x => GetSymbolBySyntaxType<MethodDeclarationSyntax>(x).Concat(GetSymbolBySyntaxType<ConstructorDeclarationSyntax>(x))).ToList());
        private SyntaxNode GetNodeFromSymbol(string typeName) =>
            (GetTypeSymbols(typeName).FirstOrDefault() != null) ? (GetTypeSymbols(typeName).First() as ISymbol).DeclaringSyntaxReferences.First()?.SyntaxTree.GetRoot() : null;
        private IEnumerable<INamedTypeSymbol> GetTypeSymbols(string typeName) =>
            Compilations.Values.Select(comp => comp.GetTypeByMetadataName(typeName))                                                //Get NamedTypeSymbols from each project
            .Where(nts => nts != null);                                                                                             //Where the symbol isn't null
        private IEnumerable<ReferencedSymbol> GetReferenceSymbols(IEnumerable<INamedTypeSymbol> symbols, string methodName) =>
            symbols.Concat(symbols.SelectMany(s => s.GetMembers(methodName)))                                                       //Concat the symbol with its member symbols
            .SelectMany(s => SymbolFinder.FindReferencesAsync(s, Solution).Result);                                                 //Get the ReferencedSymbols for each NamedTypeSymbol  
        private IEnumerable<(ReferenceLocation, IEnumerable<SyntaxNode>)> GetReferenceNodes(IEnumerable<ReferencedSymbol> symbols)
           => symbols.SelectMany(x => x.Locations                                                                                   //Get Locations for all the Symbols               
               .Where(y => processedReferenceLocations.TryAdd(y, null)))                                                            //That have not been processed before                                                                             
              .Select(x => (x, x.Location.SourceTree.GetRoot().FindToken(x.Location.SourceSpan.Start).Parent.AncestorsAndSelf()));  //Return Location and all referencing nodes         
        
        // This returns all of the reference information for a particular node type (Method or Constructor)
        private IEnumerable<(ISymbol symbol, SyntaxNode node, ReferenceLocation location, string method)> GetSymbolBySyntaxType<T>((ReferenceLocation Location, IEnumerable<SyntaxNode> Nodes) locationAndNodes) where T : SyntaxNode
        {
            var referencesOfType = locationAndNodes.Nodes.OfType<T>();
            var symbol = referencesOfType.Any() ? Compilations[locationAndNodes.Location.Document.Project.Name].GetSemanticModel(locationAndNodes.Location.Location.SourceTree.GetRoot().SyntaxTree).GetDeclaredSymbol(referencesOfType.First()) : null;

            foreach (var reference in referencesOfType)
            {
                // Both MethodDeclarationSyntax and ConstructorDeclarationSyntax have an Identifier Property that is needed.  The property however is class level and not
                // shared by the inheritance implementations, thus they must be tested individually instead of referred to as a base type.
                var method = (reference is MethodDeclarationSyntax) ? (reference as MethodDeclarationSyntax).Identifier.ValueText : (reference as ConstructorDeclarationSyntax).Identifier.ValueText;
                yield return (symbol, reference, locationAndNodes.Location, method);
            }
        }
        private async Task<Solution> SetSolutionInfo(string path)
        {
            using (var workspace = MSBuildWorkspace.Create())
            {
                var solution = await workspace.OpenSolutionAsync(path);
                var compilations = await Task.WhenAll(solution.Projects.AsParallel().WithDegreeOfParallelism(10).Select(async x => (x, await x.GetCompilationAsync())).AsEnumerable());
                Compilations = compilations.ToDictionary(x => x.x.Name, x => x.Item2);
                return solution;
            }
        }
        private Solution Solution;
        private Dictionary<string, Compilation> Compilations = new Dictionary<string, Compilation>();
    }
}
