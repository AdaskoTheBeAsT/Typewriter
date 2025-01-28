using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Typewriter.Configuration;
using Typewriter.Metadata.Interfaces;

namespace Typewriter.Metadata.Roslyn
{
    public class RoslynTypeMetadata : ITypeMetadata
    {
        private readonly ITypeSymbol _symbol;
        private readonly bool _isNullable;
        private readonly bool _isTask;

        public RoslynTypeMetadata(ITypeSymbol symbol, bool isNullable, bool isTask, Settings settings)
        {
            _symbol = symbol;
            _isNullable = isNullable;
            _isTask = isTask;
            Settings = settings;
        }

        public Settings Settings { get; }

        public string DocComment => _symbol.GetDocumentationCommentXml();

        public string Name => _symbol.GetName() + (IsNullable ? "?" : string.Empty);

        public string FullName => _symbol.GetFullName() + (IsNullable ? "?" : string.Empty);

        public string AssemblyName => _symbol.ContainingAssembly?.Name;

        public bool IsAbstract => (_symbol as INamedTypeSymbol)?.IsAbstract ?? false;

        public bool IsGeneric => (_symbol as INamedTypeSymbol)?.TypeParameters.Any() ?? false;

        public bool IsStatic => (_symbol as INamedTypeSymbol)?.IsStatic ?? false;

        public bool IsDefined => _symbol.Locations.Any(l => l.IsInSource);

        public bool IsValueTuple => _symbol.Name.Equals(string.Empty, StringComparison.OrdinalIgnoreCase) &&
                                    string.Equals(_symbol.BaseType?.Name, "ValueType", StringComparison.OrdinalIgnoreCase) &&
                                    string.Equals(_symbol.BaseType.ContainingNamespace.Name, "System", StringComparison.OrdinalIgnoreCase);

        public string Namespace => _symbol.GetNamespace();

        public ITypeMetadata Type => this;

        public string DefaultValue { get; set; }

        public ITypeMetadata ElementType { 
            get {
                if( _symbol is IArrayTypeSymbol arrayTypeSymbol ) {
                    return FromTypeSymbol(arrayTypeSymbol.ElementType, Settings);
                }
                return null;
            }
        }

        public IEnumerable<string> FileLocations => _symbol.Locations.Select(l => l.SourceTree.FilePath);

        public IEnumerable<IAttributeMetadata> Attributes => RoslynAttributeMetadata.FromAttributeData(_symbol.GetAttributes(), Settings);

        public IClassMetadata BaseClass => RoslynClassMetadata.FromNamedTypeSymbol(_symbol.BaseType, Settings);

        public IClassMetadata ContainingClass => RoslynClassMetadata.FromNamedTypeSymbol(_symbol.ContainingType, Settings);

        public IEnumerable<IConstantMetadata> Constants => RoslynConstantMetadata.FromFieldSymbols(_symbol.GetMembers().OfType<IFieldSymbol>(), Settings);

        public IEnumerable<IDelegateMetadata> Delegates => RoslynDelegateMetadata.FromNamedTypeSymbols(_symbol.GetMembers().OfType<INamedTypeSymbol>().Where(s => s.TypeKind == TypeKind.Delegate), Settings);

        public IEnumerable<IEventMetadata> Events => RoslynEventMetadata.FromEventSymbols(_symbol.GetMembers().OfType<IEventSymbol>(), Settings);

        public IEnumerable<IFieldMetadata> Fields => RoslynFieldMetadata.FromFieldSymbols(_symbol.GetMembers().OfType<IFieldSymbol>(), Settings);

        public IEnumerable<IInterfaceMetadata> Interfaces => RoslynInterfaceMetadata.FromNamedTypeSymbols(_symbol.Interfaces, null, Settings);

        public IEnumerable<IMethodMetadata> Methods => RoslynMethodMetadata.FromMethodSymbols(_symbol.GetMembers().OfType<IMethodSymbol>(), Settings);

        public IEnumerable<IPropertyMetadata> Properties => RoslynPropertyMetadata.FromPropertySymbol(_symbol.GetMembers().OfType<IPropertySymbol>(), Settings);

        public IEnumerable<IStaticReadOnlyFieldMetadata> StaticReadOnlyFields =>
            RoslynStaticReadOnlyFieldMetadata.FromFieldSymbols(_symbol.GetMembers().OfType<IFieldSymbol>(), Settings);

        public IEnumerable<IClassMetadata> NestedClasses => RoslynClassMetadata.FromNamedTypeSymbols(_symbol.GetMembers().OfType<INamedTypeSymbol>().Where(s => s.TypeKind == TypeKind.Class), null, Settings);

        public IEnumerable<IEnumMetadata> NestedEnums => RoslynEnumMetadata.FromNamedTypeSymbols(_symbol.GetMembers().OfType<INamedTypeSymbol>().Where(s => s.TypeKind == TypeKind.Enum), Settings);

        public IEnumerable<IInterfaceMetadata> NestedInterfaces => RoslynInterfaceMetadata.FromNamedTypeSymbols(_symbol.GetMembers().OfType<INamedTypeSymbol>().Where(s => s.TypeKind == TypeKind.Interface), null, Settings);

        public IEnumerable<IFieldMetadata> TupleElements
        {
            get
            {
#pragma warning disable CC0004 // Catch block cannot be empty
                try
                {
                    if (_symbol is INamedTypeSymbol n && n.Name == string.Empty && string.Equals(
                            n.BaseType?.Name,
                            "ValueType",
                            StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(
                            n.BaseType.ContainingNamespace.Name,
                            "System",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        var property = n.GetType().GetProperty(nameof(TupleElements));
                        if (property != null)
                        {
                            var value = property.GetValue(_symbol);
                            var tupleElements = value as IEnumerable<IFieldSymbol>;

                            return RoslynFieldMetadata.FromFieldSymbols(tupleElements, Settings);
                        }
                    }
                }
                catch
                {
                    // noop
                }
#pragma warning restore CC0004 // Catch block cannot be empty

                return Array.Empty<IFieldMetadata>();
            }
        }

        public IEnumerable<ITypeMetadata> TypeArguments
        {
            get
            {
                if (_symbol is INamedTypeSymbol namedTypeSymbol)
                {
                    return FromTypeSymbols(namedTypeSymbol.TypeArguments, Settings);
                }

                if (_symbol is IArrayTypeSymbol arrayTypeSymbol)
                {
                    return FromTypeSymbols(new[] { arrayTypeSymbol.ElementType }, Settings);
                }

                return Array.Empty<ITypeMetadata>();
            }
        }

        public IEnumerable<ITypeParameterMetadata> TypeParameters
        {
            get
            {
                if (_symbol is INamedTypeSymbol namedTypeSymbol)
                {
                    return RoslynTypeParameterMetadata.FromTypeParameterSymbols(namedTypeSymbol.TypeParameters);
                }

                return Array.Empty<ITypeParameterMetadata>();
            }
        }

        public bool IsDictionary => _symbol is INamedTypeSymbol namedTypeSymbol && namedTypeSymbol.IsGenericType
            && (namedTypeSymbol.OriginalDefinition.Name.Equals("Dictionary", StringComparison.OrdinalIgnoreCase) ||
                namedTypeSymbol.OriginalDefinition.Name.Equals("IDictionary", StringComparison.OrdinalIgnoreCase))
            && namedTypeSymbol.OriginalDefinition.ContainingNamespace.ToDisplayString().Equals(
                "System.Collections.Generic",
                StringComparison.OrdinalIgnoreCase);

        public bool IsDynamic => _symbol.TypeKind == TypeKind.Dynamic;

        public bool IsEnum => _symbol.TypeKind == TypeKind.Enum;

        public bool IsEnumerable => !_symbol.ToDisplayString().Equals("string", StringComparison.OrdinalIgnoreCase) &&
                                    !_symbol.ToDisplayString().Equals("string?", StringComparison.OrdinalIgnoreCase) &&
                                    (
                                        _symbol.TypeKind == TypeKind.Array ||
                                        _symbol.ToDisplayString().Equals(
                                            "System.Collections.IEnumerable",
                                            StringComparison.OrdinalIgnoreCase) ||
                                        _symbol.AllInterfaces.Any(
                                            i =>
                                                i.ToDisplayString().Equals(
                                                    "System.Collections.IEnumerable",
                                                    StringComparison.OrdinalIgnoreCase)));

        public bool IsNullable => _isNullable || _symbol.NullableAnnotation == NullableAnnotation.Annotated;

        public bool IsTask => _isTask;

        public static ITypeMetadata FromTypeSymbol(ITypeSymbol symbol, Settings settings)
        {
            if (symbol.Name.Equals("Nullable", StringComparison.OrdinalIgnoreCase) &&
                symbol.ContainingNamespace.Name.Equals("System", StringComparison.OrdinalIgnoreCase))
            {
                var type = symbol as INamedTypeSymbol;
                var argument = type?.TypeArguments.FirstOrDefault();

                if (argument != null)
                {
                    return new RoslynTypeMetadata(argument, true, false, settings);
                }
            }
            else if (symbol.Name.Equals("Task", StringComparison.OrdinalIgnoreCase) &&
                     symbol.ContainingNamespace.GetFullName().Equals("System.Threading.Tasks", StringComparison.OrdinalIgnoreCase))
            {
                var type = symbol as INamedTypeSymbol;
                var argument = type?.TypeArguments.FirstOrDefault();

                if (argument != null)
                {
                    if (argument.Name.Equals("Nullable", StringComparison.OrdinalIgnoreCase) &&
                        argument.ContainingNamespace.Name.Equals("System", StringComparison.OrdinalIgnoreCase))
                    {
                        type = argument as INamedTypeSymbol;
                        var innerArgument = type?.TypeArguments.FirstOrDefault();

                        if (innerArgument != null)
                        {
                            return new RoslynTypeMetadata(innerArgument, true, true, settings);
                        }
                    }

                    return new RoslynTypeMetadata(argument, false, true, settings);
                }

                return new RoslynVoidTaskMetadata();
            }
            else if (symbol.BaseType?.SpecialType == SpecialType.System_Enum)
            {
                var result = new RoslynTypeMetadata(symbol, false, false, settings);
                var namedTypeSymbol = symbol as INamedTypeSymbol;

                var symbols = namedTypeSymbol.GetMembers();
                if (symbols.Length == 0)
                {
                    result.DefaultValue = "enum should contain minimum one enum value";
                }
                else
                {
                    result.DefaultValue = $"{namedTypeSymbol.Name}.{symbols[0].Name}";
                }

                return result;
            }

            return new RoslynTypeMetadata(symbol, false, false, settings);
        }

        public static IEnumerable<ITypeMetadata> FromTypeSymbols(IEnumerable<ITypeSymbol> symbols, Settings settings)
        {
            return symbols.Select(item => FromTypeSymbol(item, settings));
        }
    }
}