using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Typewriter.Configuration;
using Typewriter.Metadata.Interfaces;

namespace Typewriter.Metadata.Roslyn
{
    public class RoslynPropertyMetadata : IPropertyMetadata
    {
        private readonly IPropertySymbol _symbol;

        private RoslynPropertyMetadata(IPropertySymbol symbol, Settings settings)
        {
            _symbol = symbol;
            Settings = settings;
        }

        public Settings Settings { get; }

        public string DocComment => _symbol.GetDocumentationCommentXml();

        public string Name => _symbol.Name;

        public string FullName => _symbol.ToDisplayString();

        public string AssemblyName => _symbol.ContainingAssembly?.Name;

        public IEnumerable<IAttributeMetadata> Attributes => RoslynAttributeMetadata.FromAttributeData(_symbol.GetAttributes(), Settings);

        public ITypeMetadata Type => RoslynTypeMetadata.FromTypeSymbol(_symbol.Type, Settings);

        public bool IsAbstract => _symbol.IsAbstract;

        public bool IsVirtual => _symbol.IsVirtual;

        public bool HasGetter => _symbol.GetMethod != null && _symbol.GetMethod.DeclaredAccessibility == Accessibility.Public;

        public bool HasSetter => _symbol.SetMethod != null && _symbol.SetMethod.DeclaredAccessibility == Accessibility.Public;

        public static IEnumerable<IPropertyMetadata> FromPropertySymbol(IEnumerable<IPropertySymbol> symbols, Settings settings)
        {
            return symbols.Where(p => p.DeclaredAccessibility == Accessibility.Public && !p.IsStatic)
                .Select(p => new RoslynPropertyMetadata(p, settings));
        }
    }
}