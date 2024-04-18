using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Typewriter.Configuration;
using Typewriter.Metadata.Interfaces;

namespace Typewriter.Metadata.Roslyn
{
    public class RoslynAttributeMetadata : IAttributeMetadata
    {
        private readonly INamedTypeSymbol _symbol;
        private readonly string _name;
        private readonly string _value;

        private RoslynAttributeMetadata(AttributeData a, Settings settings)
        {
            Settings = settings;
            var declaration = a.ToString();
            var index = declaration.IndexOf("(", StringComparison.Ordinal);

            _symbol = a.AttributeClass;
            _name = _symbol.Name;

            if (index > -1)
            {
                _value = declaration.Substring(index + 1, declaration.Length - index - 2);

                // Trim {} from params
                if (_value.EndsWith("\"}", StringComparison.OrdinalIgnoreCase))
                {
                    _value = _value.Remove(_value.LastIndexOf("{\"", StringComparison.Ordinal), 1);
                    _value = _value.TrimEnd('}');
                }
                else if (_value.EndsWith("}", StringComparison.OrdinalIgnoreCase))
                {
                    _value = _value.Remove(_value.LastIndexOf("{", StringComparison.Ordinal), 1);
                    _value = _value.TrimEnd('}');
                }
            }

            if (_name.EndsWith("Attribute", StringComparison.OrdinalIgnoreCase))
            {
                _name = _name.Substring(0, _name.Length - 9);
            }

            Arguments = a.ConstructorArguments.Concat(a.NamedArguments.Select(p => p.Value))
                .Select(p => new RoslynAttributeArgumentMetadata(p, Settings));
        }

        public Settings Settings { get; }

        public string DocComment => _symbol.GetDocumentationCommentXml();

        public string Name => _name;

        public string FullName => _symbol.ToDisplayString();

        public string AssemblyName => _symbol.ContainingAssembly?.Name;

        public ITypeMetadata Type => RoslynTypeMetadata.FromTypeSymbol(_symbol, Settings);

        public string Value => _value;

        public IEnumerable<IAttributeArgumentMetadata> Arguments { get; }

        public static IEnumerable<IAttributeMetadata> FromAttributeData(IEnumerable<AttributeData> attributes, Settings settings)
        {
            return attributes.Select(a => new RoslynAttributeMetadata(a, settings));
        }
    }
}