using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Typewriter.Abstractions;

namespace Typewriter.Roslyn;

public sealed partial class CSharpProjectMetadataProvider
{
    private sealed class MetadataExtractor
    {
        private readonly Dictionary<INamedTypeSymbol, TypeMetadata> _types = new(comparer: SymbolEqualityComparer.Default);
        private readonly Dictionary<INamedTypeSymbol, IReadOnlyList<EnumValueMetadata>> _enumValues = new(comparer: SymbolEqualityComparer.Default);

        // Lazy reference edges capture this extractor. Keep that ownership graph ordinary:
        // dependent handles in a weak-key table can prolong earlier extraction graphs.
        private readonly ConcurrentDictionary<ITypeSymbol, TypeReferenceCacheNode> _typeReferences = new(comparer: ReferenceEqualityComparer.Instance);
        private readonly ConcurrentDictionary<ITypeSymbol, TypeReferenceCacheNode> _identityTypeReferences = new(comparer: SymbolEqualityComparer.IncludeNullability);
        private readonly ConditionalWeakTable<ISymbol, CachedFullName> _fullNames = [];
        private readonly ConditionalWeakTable<INamedTypeSymbol, CachedFileLocations> _fileLocations = [];
        private readonly ConditionalWeakTable<INamedTypeSymbol, CollectionShape> _collectionShapes = [];
        private readonly Dictionary<SyntaxTree, SemanticModel> _semanticModels = [];
        private readonly Lock _enumValuesLock = new();

        public SourceSymbols GetSourceSymbols(
            CSharpCompilation compilation,
            IEnumerable<SyntaxTree> syntaxTrees,
            CancellationToken cancellationToken)
        {
            var typesByKey = new Dictionary<string, INamedTypeSymbol>(comparer: StringComparer.Ordinal);
            var delegatesByKey = new Dictionary<string, INamedTypeSymbol>(comparer: StringComparer.Ordinal);
            foreach (var syntaxTree in syntaxTrees)
            {
                var semanticModel = GetSemanticModel(compilation: compilation, syntaxTree: syntaxTree);
                var root = syntaxTree.GetRoot(cancellationToken: cancellationToken);
                foreach (var declaration in root.DescendantNodes(
                             descendIntoChildren: static node => node is CompilationUnitSyntax or BaseNamespaceDeclarationSyntax or BaseTypeDeclarationSyntax))
                {
                    switch (declaration)
                    {
                        case BaseTypeDeclarationSyntax typeDeclaration
                            when semanticModel.GetDeclaredSymbol(declarationSyntax: typeDeclaration, cancellationToken: cancellationToken) is INamedTypeSymbol type:
                            AddSymbol(symbolsByKey: typesByKey, symbol: type);
                            break;
                        case DelegateDeclarationSyntax delegateDeclaration
                            when semanticModel.GetDeclaredSymbol(declarationSyntax: delegateDeclaration, cancellationToken: cancellationToken) is INamedTypeSymbol { ContainingType: null } @delegate:
                            AddSymbol(symbolsByKey: delegatesByKey, symbol: @delegate);
                            break;
                        default:
                            break;
                    }
                }
            }

            return new SourceSymbols(
                Types: typesByKey.Values.ToArray(),
                Delegates: delegatesByKey.Values.ToArray());
        }

#pragma warning disable MA0051 // Method is too long
        public bool TryCreateType(
            INamedTypeSymbol symbol,
            Compilation compilation,
            CancellationToken cancellationToken,
            out TypeMetadata metadata)
#pragma warning restore MA0051 // Method is too long
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_types.TryGetValue(key: symbol, value: out var cached))
            {
                metadata = cached;
                return true;
            }

            metadata = null!;
            if (symbol.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Internal)
                || symbol.IsImplicitlyDeclared)
            {
                return false;
            }

            var kind = symbol.TypeKind switch
            {
                Microsoft.CodeAnalysis.TypeKind.Class when symbol.IsRecord => TypeMetadataKind.Record,
                Microsoft.CodeAnalysis.TypeKind.Class => TypeMetadataKind.Class,
                Microsoft.CodeAnalysis.TypeKind.Struct => TypeMetadataKind.Struct,
                Microsoft.CodeAnalysis.TypeKind.Interface => TypeMetadataKind.Interface,
                Microsoft.CodeAnalysis.TypeKind.Enum => TypeMetadataKind.Enum,
                _ => (TypeMetadataKind?)null,
            };

            if (kind is null)
            {
                return false;
            }

            var docComment = GetDocComment(symbol: symbol);
            var members = symbol.GetMembers();
            var typeMembers = symbol.GetTypeMembers();
            var nestedTypes = GetNestedTypes(members: typeMembers, compilation: compilation, cancellationToken: cancellationToken).ToArray();
            metadata = new TypeMetadata(
                Name: symbol.Name,
                FullName: GetFullName(symbol: symbol),
                Namespace: GetNamespace(symbol: symbol),
                Kind: kind.Value,
                Accessibility: MapAccessibility(accessibility: symbol.DeclaredAccessibility),
                Properties: GetProperties(symbol: symbol, members: members).ToArray(),
                Attributes: GetAttributes(attributes: symbol.GetAttributes()).ToArray(),
                BaseTypes: GetBaseTypes(symbol: symbol).ToArray(),
                EnumValues: GetEnumValues(symbol: symbol),
                IsNullableAware: IsNullableAware(symbol: symbol, compilation: compilation))
            {
                Location = GetSourceLocation(symbol: symbol),
                FileLocations = GetFileLocations(symbol: symbol),
                Documentation = docComment?.Summary,
                DocComment = docComment,
                AssemblyName = GetAssemblyName(symbol: symbol),
                IsStatic = symbol.IsStatic,
                IsAbstract = symbol.IsAbstract,
                ContainingTypeFullName = symbol.ContainingType is null ? string.Empty : GetFullName(symbol: symbol.ContainingType),
                Methods = GetMethods(symbol: symbol, members: members).ToArray(),
                Constants = GetConstants(symbol: symbol, members: members).ToArray(),
                Fields = GetFields(symbol: symbol, members: members).ToArray(),
                StaticReadOnlyFields = GetStaticReadOnlyFields(symbol: symbol, members: members).ToArray(),
                Events = GetEvents(symbol: symbol, members: members).ToArray(),
                Delegates = GetDelegates(members: typeMembers, compilation: compilation).ToArray(),
                TypeParameters = GetTypeParameters(symbols: symbol.TypeParameters).ToArray(),
                TypeArguments = symbol.TypeArguments
                    .Zip(
                        second: symbol.TypeArgumentNullableAnnotations,
                        resultSelector: (argument, annotation) => CreateTypeReference(symbol: argument, nullableAnnotation: annotation))
                    .ToArray(),
                NestedClasses = nestedTypes.Where(predicate: type => type.Kind == TypeMetadataKind.Class).ToArray(),
                NestedRecords = nestedTypes.Where(predicate: type => type.Kind == TypeMetadataKind.Record).ToArray(),
                NestedStructs = nestedTypes.Where(predicate: type => type.Kind == TypeMetadataKind.Struct).ToArray(),
                NestedEnums = nestedTypes.Where(predicate: type => type.Kind == TypeMetadataKind.Enum).ToArray(),
                NestedInterfaces = nestedTypes.Where(predicate: type => type.Kind == TypeMetadataKind.Interface).ToArray(),
            };
            _types.Add(key: symbol, value: metadata);
            return true;
        }

        public bool TryCreateDelegate(
            INamedTypeSymbol symbol,
            Compilation compilation,
            out DelegateMetadata metadata)
        {
            metadata = null!;
            if (symbol.TypeKind != Microsoft.CodeAnalysis.TypeKind.Delegate
                || symbol.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Internal)
                || symbol.IsImplicitlyDeclared)
            {
                return false;
            }

            var invokeMethod = symbol.DelegateInvokeMethod;
            var fullName = GetFullName(symbol: symbol);
            var docComment = GetDocComment(symbol: symbol);
            var returnType = invokeMethod is null
                ? CreateVoidTypeReference(compilation: compilation)
                : CreateTypeReference(symbol: invokeMethod.ReturnType, nullableAnnotation: invokeMethod.ReturnNullableAnnotation);
            metadata = new DelegateMetadata(
                Name: symbol.Name,
                FullName: fullName,
                Accessibility: MapAccessibility(accessibility: symbol.DeclaredAccessibility),
                ReturnType: returnType,
                IsGeneric: symbol.IsGenericType,
                Parameters: invokeMethod is null ? [] : GetParameters(method: invokeMethod, methodFullName: fullName).ToArray(),
                Attributes: GetAttributes(attributes: symbol.GetAttributes()).ToArray(),
                ParentTypeFullName: symbol.ContainingType is null ? string.Empty : GetFullName(symbol: symbol.ContainingType))
            {
                AssemblyName = GetAssemblyName(symbol: symbol),
                Location = GetSourceLocation(symbol: symbol),
                Documentation = docComment?.Summary,
                DocComment = docComment,
                TypeParameters = GetTypeParameters(symbols: symbol.TypeParameters).ToArray(),
            };
            return true;
        }

        private static string? FormatConstantValue(object? value)
        {
            return value switch
            {
                null => "null",
                bool boolValue => boolValue ? "true" : "false",
                IFormattable formattable => formattable.ToString(format: null, formatProvider: CultureInfo.InvariantCulture),
                _ => value.ToString(),
            };
        }

        private static HashSet<ITypeSymbol> RentVisitedSymbols() =>
            TypeReferenceVisitedSymbolSets.TryTake(result: out var visitedSymbols)
                ? visitedSymbols
                : new HashSet<ITypeSymbol>(comparer: SymbolEqualityComparer.Default);

        private static void ReturnVisitedSymbols(HashSet<ITypeSymbol> visitedSymbols)
        {
            visitedSymbols.Clear();
            TypeReferenceVisitedSymbolSets.Add(item: visitedSymbols);
        }

        private static bool IsEnum(ITypeSymbol symbol)
        {
            return symbol.TypeKind == Microsoft.CodeAnalysis.TypeKind.Enum;
        }

        private static bool IsValueTuple(ITypeSymbol symbol)
        {
            return symbol is INamedTypeSymbol { IsTupleType: true };
        }

        private static bool IsPrimitive(ITypeSymbol symbol)
        {
            return symbol.SpecialType is SpecialType.System_Boolean
                or SpecialType.System_Byte
                or SpecialType.System_SByte
                or SpecialType.System_Int16
                or SpecialType.System_UInt16
                or SpecialType.System_Int32
                or SpecialType.System_UInt32
                or SpecialType.System_Int64
                or SpecialType.System_UInt64
                or SpecialType.System_Single
                or SpecialType.System_Double
                or SpecialType.System_Decimal
                or SpecialType.System_String
                or SpecialType.System_Char;
        }

        private static string GetDisplayName(ITypeSymbol symbol)
        {
            return symbol switch
            {
                IArrayTypeSymbol arrayType => $"{GetDisplayName(symbol: arrayType.ElementType)}[]",
                INamedTypeSymbol { IsGenericType: true } namedType => namedType.Name,
                _ => symbol.Name,
            };
        }

        private static string GetNamespace(ISymbol? symbol)
        {
            if (symbol?.ContainingNamespace is null || symbol.ContainingNamespace.IsGlobalNamespace)
            {
                return string.Empty;
            }

            return symbol.ContainingNamespace.ToDisplayString();
        }

        private static string GetAssemblyName(ISymbol? symbol)
        {
            return symbol?.ContainingAssembly?.Name ?? string.Empty;
        }

        private static string? GetSpecialTypeName(SpecialType specialType)
        {
            return specialType switch
            {
                SpecialType.System_Boolean => "System.Boolean",
                SpecialType.System_Byte => "System.Byte",
                SpecialType.System_SByte => "System.SByte",
                SpecialType.System_Int16 => "System.Int16",
                SpecialType.System_UInt16 => "System.UInt16",
                SpecialType.System_Int32 => "System.Int32",
                SpecialType.System_UInt32 => "System.UInt32",
                SpecialType.System_Int64 => "System.Int64",
                SpecialType.System_UInt64 => "System.UInt64",
                SpecialType.System_Single => "System.Single",
                SpecialType.System_Double => "System.Double",
                SpecialType.System_Decimal => "System.Decimal",
                SpecialType.System_String => "System.String",
                SpecialType.System_Char => "System.Char",
                SpecialType.System_Object => "System.Object",
                SpecialType.System_Void => "System.Void",
                _ => null,
            };
        }

        private static MetadataAccessibility MapAccessibility(Accessibility accessibility)
        {
            return accessibility switch
            {
                Accessibility.Public => MetadataAccessibility.Public,
                Accessibility.Internal => MetadataAccessibility.Internal,
                Accessibility.Protected => MetadataAccessibility.Protected,
                Accessibility.ProtectedOrInternal => MetadataAccessibility.ProtectedInternal,
                Accessibility.ProtectedAndInternal => MetadataAccessibility.PrivateProtected,
                _ => MetadataAccessibility.Private,
            };
        }

        private static SourceLocation? GetSourceLocation(ISymbol symbol)
        {
            var location = symbol.Locations.FirstOrDefault(predicate: candidate => candidate.IsInSource);
            if (location is null)
            {
                return null;
            }

            var lineSpan = location.GetLineSpan();
            if (string.IsNullOrWhiteSpace(value: lineSpan.Path))
            {
                return null;
            }

            return new SourceLocation(
                Path: Path.GetFullPath(path: lineSpan.Path),
                Line: lineSpan.StartLinePosition.Line + 1,
                Column: lineSpan.StartLinePosition.Character + 1);
        }

        private static DocCommentMetadata? GetDocComment(ISymbol symbol) =>
            DocCommentCache.GetValue(key: symbol, createValueCallback: static cachedSymbol => new CachedDocComment(ReadDocComment(symbol: cachedSymbol))).Value;

        private static DocCommentMetadata? ReadDocComment(ISymbol symbol)
        {
            var documentation = symbol.GetDocumentationCommentXml(
                preferredCulture: CultureInfo.InvariantCulture,
                expandIncludes: true);
            if (string.IsNullOrWhiteSpace(value: documentation))
            {
                return null;
            }

            try
            {
                var document = XDocument.Parse(text: "<doc>" + documentation + "</doc>");
                return new DocCommentMetadata(
                    Summary: NormalizeDocumentation(documentation: GetDocCommentElementContent(element: document.Descendants(name: "summary").FirstOrDefault())) ?? string.Empty,
                    Returns: NormalizeDocumentation(documentation: GetDocCommentElementContent(element: document.Descendants(name: "returns").FirstOrDefault())) ?? string.Empty,
                    Parameters: document.Descendants(name: "param")
                        .Select(
                            selector: element => new ParameterCommentMetadata(
                                Name: element.Attribute(name: "name")?.Value.Trim() ?? string.Empty,
                                Description: NormalizeDocumentation(documentation: GetDocCommentElementContent(element: element)) ?? string.Empty))
                        .ToArray());
            }
            catch (System.Xml.XmlException)
            {
                return new DocCommentMetadata(Summary: NormalizeDocumentation(documentation: documentation) ?? string.Empty, Returns: string.Empty, Parameters: []);
            }
        }

        private static IEnumerable<TypeParameterMetadata> GetTypeParameters(IEnumerable<ITypeParameterSymbol> symbols)
        {
            return symbols.Select(selector: symbol => new TypeParameterMetadata(Name: symbol.Name)
            {
                FullName = symbol.ToDisplayString(format: SymbolDisplayFormat.CSharpErrorMessageFormat),
                Documentation = GetDocComment(symbol: symbol)?.Summary,
            });
        }

        private static string? GetStaticReadOnlyFieldValue(IFieldSymbol field)
        {
            var syntax = field.DeclaringSyntaxReferences
                .Select(selector: reference => reference.GetSyntax())
                .OfType<VariableDeclaratorSyntax>()
                .FirstOrDefault();
            var initializer = syntax?.Initializer?.Value;
            return initializer switch
            {
                null => "The field might be initialized elsewhere (like in a static constructor) or not initialized at all. Typewriter is not able to process such static readonly fields",
                LiteralExpressionSyntax literal when literal.IsKind(kind: SyntaxKind.NullLiteralExpression) => string.Empty,
                LiteralExpressionSyntax literal => literal.Token.ValueText,
                _ => initializer.ToString(),
            };
        }

        private static string? GetPropertyInitializerValue(IPropertySymbol property)
        {
            var syntax = property.DeclaringSyntaxReferences
                .Select(selector: reference => reference.GetSyntax())
                .OfType<PropertyDeclarationSyntax>()
                .FirstOrDefault();
            return GetInitializerValueText(initializer: syntax?.Initializer);
        }

        private static string? GetFieldInitializerValue(IFieldSymbol field)
        {
            var syntax = field.DeclaringSyntaxReferences
                .Select(selector: reference => reference.GetSyntax())
                .OfType<VariableDeclaratorSyntax>()
                .FirstOrDefault();
            return GetInitializerValueText(initializer: syntax?.Initializer);
        }

        private static string? GetInitializerValueText(EqualsValueClauseSyntax? initializer)
        {
            var value = initializer?.Value;
            return value switch
            {
                null => null,
                LiteralExpressionSyntax literal when literal.IsKind(kind: SyntaxKind.NullLiteralExpression) => "null",
                LiteralExpressionSyntax literal => literal.Token.ValueText,
                _ => value.ToString(),
            };
        }

        private static string? GetDocCommentElementContent(XElement? element)
        {
            return element is null
                ? null
                : string.Concat(values: element.Nodes());
        }

        private static string? NormalizeDocumentation(string? documentation)
        {
            if (string.IsNullOrWhiteSpace(value: documentation))
            {
                return null;
            }

            var parts = documentation
                .Split(
                    separator: [' ', '\t', '\r', '\n'],
                    options: StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 0
                ? null
                : string.Join(separator: ' ', value: parts);
        }

        private static string TrimAttributeSuffix(string name)
        {
            const string suffix = "Attribute";
            return name.EndsWith(value: suffix, comparisonType: StringComparison.Ordinal)
                ? name[..^suffix.Length]
                : name;
        }

        private void AddSymbol(
            IDictionary<string, INamedTypeSymbol> symbolsByKey,
            INamedTypeSymbol symbol)
        {
            var key = GetFullName(symbol: symbol) + "`" + symbol.Arity.ToString(provider: CultureInfo.InvariantCulture);
            if (!symbolsByKey.ContainsKey(key: key))
            {
                symbolsByKey[key: key] = symbol;
            }
        }

        private IEnumerable<PropertyMetadata> GetProperties(
            INamedTypeSymbol symbol,
            IEnumerable<ISymbol> members)
        {
            return members.OfType<IPropertySymbol>()
                .Where(predicate: property => !property.IsStatic)
                .Where(predicate: property => property.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal)
                .Where(predicate: property => property.GetMethod is not null)
                .Select(
                    selector: property =>
                    {
                        var fullName = property.ToDisplayString(format: SymbolDisplayFormat.CSharpErrorMessageFormat);
                        var docComment = GetDocComment(symbol: property);
                        return new PropertyMetadata(
                            Name: property.Name,
                            FullName: fullName,
                            Type: CreateTypeReference(symbol: property.Type, nullableAnnotation: property.NullableAnnotation),
                            Accessibility: MapAccessibility(accessibility: property.DeclaredAccessibility),
                            HasGetter: property.GetMethod is not null,
                            HasSetter: property.SetMethod is not null,
                            IsRequired: property.IsRequired,
                            Attributes: GetAttributes(attributes: property.GetAttributes()).ToArray())
                        {
                            ParentTypeFullName = GetFullName(symbol: symbol),
                            AssemblyName = GetAssemblyName(symbol: property),
                            Location = GetSourceLocation(symbol: property),
                            Documentation = docComment?.Summary,
                            DocComment = docComment,
                            IsAbstract = property.IsAbstract,
                            IsIndexer = property.IsIndexer,
                            IsVirtual = property.IsVirtual,
                            Parameters = GetParameters(parameters: property.Parameters, parentFullName: fullName, parentPropertyFullName: fullName).ToArray(),
                            Value = GetPropertyInitializerValue(property: property),
                        };
                    });
        }

        private IEnumerable<MethodMetadata> GetMethods(
            INamedTypeSymbol symbol,
            IEnumerable<ISymbol> members)
        {
            return members.OfType<IMethodSymbol>()
                .Where(predicate: method => method.MethodKind == MethodKind.Ordinary)
                .Where(predicate: method => !method.IsImplicitlyDeclared)
                .Where(predicate: method => method.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal)
                .Select(
                    selector: method =>
                    {
                        var fullName = method.ToDisplayString(format: SymbolDisplayFormat.CSharpErrorMessageFormat);
                        var docComment = GetDocComment(symbol: method);
                        return new MethodMetadata(
                            Name: method.Name,
                            FullName: fullName,
                            ReturnType: CreateTypeReference(symbol: method.ReturnType, nullableAnnotation: method.ReturnNullableAnnotation),
                            Accessibility: MapAccessibility(accessibility: method.DeclaredAccessibility),
                            IsStatic: method.IsStatic,
                            IsAbstract: method.IsAbstract,
                            IsGeneric: method.IsGenericMethod,
                            Parameters: GetParameters(method: method, methodFullName: fullName).ToArray(),
                            Attributes: GetAttributes(attributes: method.GetAttributes()).ToArray(),
                            ParentTypeFullName: GetFullName(symbol: symbol))
                        {
                            AssemblyName = GetAssemblyName(symbol: method),
                            Location = GetSourceLocation(symbol: method),
                            Documentation = docComment?.Summary,
                            DocComment = docComment,
                            TypeParameters = GetTypeParameters(symbols: method.TypeParameters).ToArray(),
                        };
                    });
        }

        private IEnumerable<ParameterMetadata> GetParameters(
            IMethodSymbol method,
            string methodFullName) =>
            GetParameters(parameters: method.Parameters, parentFullName: methodFullName, parentMethodFullName: methodFullName);

        private IEnumerable<ParameterMetadata> GetParameters(
            IEnumerable<IParameterSymbol> parameters,
            string parentFullName,
            string parentMethodFullName = "",
            string parentPropertyFullName = "")
        {
            return parameters.Select(
                selector: parameter =>
                {
                    var defaultValue = parameter.HasExplicitDefaultValue
                        ? FormatConstantValue(value: parameter.ExplicitDefaultValue)
                        : null;
                    var docComment = GetDocComment(symbol: parameter);

                    return new ParameterMetadata(
                        Name: parameter.Name,
                        FullName: parentFullName + "." + parameter.Name,
                        Type: CreateTypeReference(symbol: parameter.Type, nullableAnnotation: parameter.NullableAnnotation),
                        HasDefaultValue: parameter.HasExplicitDefaultValue,
                        DefaultValue: defaultValue,
                        Attributes: GetAttributes(attributes: parameter.GetAttributes()).ToArray(),
                        ParentMethodFullName: parentMethodFullName)
                    {
                        AssemblyName = GetAssemblyName(symbol: parameter),
                        Location = GetSourceLocation(symbol: parameter),
                        Documentation = docComment?.Summary,
                        DocComment = docComment,
                        ParentPropertyFullName = parentPropertyFullName,
                    };
                });
        }

        private IEnumerable<ConstantMetadata> GetConstants(
            INamedTypeSymbol symbol,
            IEnumerable<ISymbol> members)
        {
            if (symbol.TypeKind == Microsoft.CodeAnalysis.TypeKind.Enum)
            {
                return [];
            }

            return members.OfType<IFieldSymbol>()
                .Where(predicate: field => field.HasConstantValue)
                .Where(predicate: field => field.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal)
                .Select(
                    selector: field =>
                    {
                        var docComment = GetDocComment(symbol: field);
                        return new ConstantMetadata(
                            Name: field.Name,
                            FullName: field.ToDisplayString(format: SymbolDisplayFormat.CSharpErrorMessageFormat),
                            Accessibility: MapAccessibility(accessibility: field.DeclaredAccessibility),
                            Type: CreateTypeReference(symbol: field.Type, nullableAnnotation: field.NullableAnnotation),
                            Value: FormatConstantValue(value: field.ConstantValue),
                            Attributes: GetAttributes(attributes: field.GetAttributes()).ToArray(),
                            ParentTypeFullName: GetFullName(symbol: symbol))
                        {
                            AssemblyName = GetAssemblyName(symbol: field),
                            Location = GetSourceLocation(symbol: field),
                            Documentation = docComment?.Summary,
                            DocComment = docComment,
                        };
                    });
        }

        private IEnumerable<FieldMetadata> GetFields(
            INamedTypeSymbol symbol,
            IEnumerable<ISymbol> members)
        {
            if (symbol.TypeKind == Microsoft.CodeAnalysis.TypeKind.Enum)
            {
                return [];
            }

            return members.OfType<IFieldSymbol>()
                .Where(predicate: field => !field.IsImplicitlyDeclared)
                .Where(predicate: field => !field.IsConst)
                .Where(predicate: field => !field.IsStatic)
                .Where(predicate: field => field.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal)
                .Select(
                    selector: field =>
                    {
                        var docComment = GetDocComment(symbol: field);
                        return new FieldMetadata(
                            Name: field.Name,
                            FullName: field.ToDisplayString(format: SymbolDisplayFormat.CSharpErrorMessageFormat),
                            Accessibility: MapAccessibility(accessibility: field.DeclaredAccessibility),
                            Type: CreateTypeReference(symbol: field.Type, nullableAnnotation: field.NullableAnnotation),
                            Attributes: GetAttributes(attributes: field.GetAttributes()).ToArray(),
                            ParentTypeFullName: GetFullName(symbol: symbol))
                        {
                            AssemblyName = GetAssemblyName(symbol: field),
                            Location = GetSourceLocation(symbol: field),
                            Documentation = docComment?.Summary,
                            DocComment = docComment,
                            Value = GetFieldInitializerValue(field: field),
                        };
                    });
        }

        private IEnumerable<StaticReadOnlyFieldMetadata> GetStaticReadOnlyFields(
            INamedTypeSymbol symbol,
            IEnumerable<ISymbol> members)
        {
            if (symbol.TypeKind == Microsoft.CodeAnalysis.TypeKind.Enum)
            {
                return [];
            }

            return members.OfType<IFieldSymbol>()
                .Where(predicate: field => !field.IsImplicitlyDeclared)
                .Where(predicate: field => field.IsStatic)
                .Where(predicate: field => field.IsReadOnly)
                .Where(predicate: field => !field.IsConst)
                .Where(predicate: field => field.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal)
                .Select(
                    selector: field =>
                    {
                        var docComment = GetDocComment(symbol: field);
                        return new StaticReadOnlyFieldMetadata(
                            Name: field.Name,
                            FullName: field.ToDisplayString(format: SymbolDisplayFormat.CSharpErrorMessageFormat),
                            Accessibility: MapAccessibility(accessibility: field.DeclaredAccessibility),
                            Type: CreateTypeReference(symbol: field.Type, nullableAnnotation: field.NullableAnnotation),
                            Value: GetStaticReadOnlyFieldValue(field: field),
                            Attributes: GetAttributes(attributes: field.GetAttributes()).ToArray(),
                            ParentTypeFullName: GetFullName(symbol: symbol))
                        {
                            AssemblyName = GetAssemblyName(symbol: field),
                            Location = GetSourceLocation(symbol: field),
                            Documentation = docComment?.Summary,
                            DocComment = docComment,
                        };
                    });
        }

        private IEnumerable<EventMetadata> GetEvents(
            INamedTypeSymbol symbol,
            IEnumerable<ISymbol> members)
        {
            return members.OfType<IEventSymbol>()
                .Where(predicate: @event => !@event.IsImplicitlyDeclared)
                .Where(predicate: @event => @event.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal)
                .Select(
                    selector: @event =>
                    {
                        var docComment = GetDocComment(symbol: @event);
                        return new EventMetadata(
                            Name: @event.Name,
                            FullName: @event.ToDisplayString(format: SymbolDisplayFormat.CSharpErrorMessageFormat),
                            Accessibility: MapAccessibility(accessibility: @event.DeclaredAccessibility),
                            Type: CreateTypeReference(symbol: @event.Type, nullableAnnotation: @event.NullableAnnotation),
                            Attributes: GetAttributes(attributes: @event.GetAttributes()).ToArray(),
                            ParentTypeFullName: GetFullName(symbol: symbol))
                        {
                            AssemblyName = GetAssemblyName(symbol: @event),
                            Location = GetSourceLocation(symbol: @event),
                            Documentation = docComment?.Summary,
                            DocComment = docComment,
                        };
                    });
        }

        private IEnumerable<DelegateMetadata> GetDelegates(
            IEnumerable<INamedTypeSymbol> members,
            Compilation compilation)
        {
            return members
                .Where(predicate: member => member.TypeKind == Microsoft.CodeAnalysis.TypeKind.Delegate)
                .Select(
                    selector: member => new
                    {
                        HasMetadata = TryCreateDelegate(symbol: member, compilation: compilation, metadata: out var metadata),
                        Metadata = metadata,
                    })
                .Where(predicate: item => item.HasMetadata)
                .Select(selector: item => item.Metadata);
        }

        private IEnumerable<TypeMetadata> GetNestedTypes(
            IEnumerable<INamedTypeSymbol> members,
            Compilation compilation,
            CancellationToken cancellationToken)
        {
            return members
                .Select(
                    selector: member => new
                    {
                        HasMetadata = TryCreateType(symbol: member, compilation: compilation, cancellationToken: cancellationToken, metadata: out var metadata),
                        Metadata = metadata,
                    })
                .Where(predicate: item => item.HasMetadata)
                .Select(selector: item => item.Metadata);
        }

        private IEnumerable<TypeMetadataReference> GetBaseTypes(INamedTypeSymbol symbol)
        {
            if (symbol.BaseType is not null
                && symbol.BaseType.SpecialType is not SpecialType.System_Object
                    and not SpecialType.System_ValueType
                    and not SpecialType.System_Enum)
            {
                yield return CreateTypeReference(symbol: symbol.BaseType, nullableAnnotation: NullableAnnotation.NotAnnotated);
            }

            foreach (var interfaceSymbol in symbol.Interfaces)
            {
                yield return CreateTypeReference(symbol: interfaceSymbol, nullableAnnotation: NullableAnnotation.NotAnnotated);
            }
        }

        private IReadOnlyList<EnumValueMetadata> GetEnumValues(INamedTypeSymbol symbol)
        {
            if (symbol.TypeKind != Microsoft.CodeAnalysis.TypeKind.Enum)
            {
                return [];
            }

            lock (_enumValuesLock)
            {
                if (_enumValues.TryGetValue(key: symbol, value: out var cached))
                {
                    return cached;
                }

                // Publish before reading attributes: an enum-valued attribute can refer
                // back to this enum. References retain this collection, not a copy.
                var values = new List<EnumValueMetadata>();
                _enumValues.Add(key: symbol, value: values);
                foreach (var field in symbol.GetMembers().OfType<IFieldSymbol>().Where(predicate: field => field.HasConstantValue))
                {
                    var docComment = GetDocComment(symbol: field);
                    values.Add(item: new EnumValueMetadata(
                        Name: field.Name,
                        Value: Convert.ToInt64(value: field.ConstantValue, provider: CultureInfo.InvariantCulture))
                    {
                        AssemblyName = GetAssemblyName(symbol: field),
                        Attributes = GetAttributes(attributes: field.GetAttributes()).ToArray(),
                        ParentTypeFullName = GetFullName(symbol: symbol),
                        Location = GetSourceLocation(symbol: field),
                        Documentation = docComment?.Summary,
                        DocComment = docComment,
                    });
                }

                return values;
            }
        }

        private IEnumerable<AttributeMetadata> GetAttributes(IEnumerable<AttributeData> attributes)
        {
            return attributes.Select(
                selector: attribute =>
                {
                    var attributeType = attribute.AttributeClass is null
                        ? null
                        : CreateTypeReference(symbol: attribute.AttributeClass, nullableAnnotation: NullableAnnotation.NotAnnotated);
                    return new AttributeMetadata(
                        Name: TrimAttributeSuffix(name: attribute.AttributeClass?.Name ?? string.Empty),
                        FullName: GetFullName(symbol: attribute.AttributeClass),
                        Arguments: GetAttributeArguments(attribute: attribute).ToArray())
                    {
                        AssemblyName = GetAssemblyName(symbol: attribute.AttributeClass),
                        Type = attributeType,
                    };
                });
        }

        private IEnumerable<AttributeArgumentMetadata> GetAttributeArguments(AttributeData attribute)
        {
            foreach (var argument in attribute.ConstructorArguments)
            {
                yield return CreateAttributeArgument(name: null, constant: argument);
            }

            foreach (var argument in attribute.NamedArguments)
            {
                yield return CreateAttributeArgument(name: argument.Key, constant: argument.Value);
            }
        }

        private AttributeArgumentMetadata CreateAttributeArgument(
            string? name,
            TypedConstant constant)
        {
            return new AttributeArgumentMetadata(Name: name, Value: FormatTypedConstant(constant: constant))
            {
                AssemblyName = GetAssemblyName(symbol: constant.Type),
                Type = constant.Type is null
                    ? null
                    : CreateTypeReference(symbol: constant.Type, nullableAnnotation: NullableAnnotation.NotAnnotated),
                TypeValue = constant.Kind == TypedConstantKind.Type
                    && constant.Value is ITypeSymbol typeSymbol
                        ? CreateTypeReference(symbol: typeSymbol, nullableAnnotation: NullableAnnotation.NotAnnotated)
                        : null,
                Elements = constant.Kind == TypedConstantKind.Array && !constant.IsNull
                    ? constant.Values.Select(selector: item => CreateAttributeArgument(name: null, constant: item)).ToArray()
                    : [],
            };
        }

        private string? FormatTypedConstant(TypedConstant constant)
        {
            if (constant.IsNull)
            {
                return null;
            }

            if (constant.Kind == TypedConstantKind.Type
                && constant.Value is ITypeSymbol typeSymbol)
            {
                return "typeof(" + GetTypeFullName(symbol: typeSymbol) + ")";
            }

            if (constant.Kind == TypedConstantKind.Enum)
            {
                return constant.ToCSharpString();
            }

            if (constant.Kind == TypedConstantKind.Array)
            {
                return string.Join(
                    separator: ", ",
                    values: constant.Values.Select(selector: FormatTypedConstant));
            }

            return Convert.ToString(value: constant.Value, provider: CultureInfo.InvariantCulture);
        }

        private string GetTypeFullName(ITypeSymbol symbol)
        {
            if (symbol is IArrayTypeSymbol arrayType)
            {
                return GetTypeFullName(symbol: arrayType.ElementType) + "[]";
            }

            if (symbol is INamedTypeSymbol { IsGenericType: true } namedType)
            {
                var arguments = string.Join(separator: ", ", values: namedType.TypeArguments.Select(selector: GetTypeFullName));
                return GetFullName(symbol: namedType) + "<" + arguments + ">";
            }

            return GetFullName(symbol: symbol);
        }

        private TypeMetadataReference CreateTypeReference(
            ITypeSymbol symbol,
            NullableAnnotation nullableAnnotation)
        {
            var annotationIndex = (int)nullableAnnotation;
            TypeReferenceCacheNode? cacheNode = null;
            if (annotationIndex >= 0 && annotationIndex < 3)
            {
                cacheNode = _typeReferences.GetOrAdd(key: symbol, valueFactory: static _ => new TypeReferenceCacheNode());
                var cached = Volatile.Read(location: ref cacheNode.ByAnnotation[annotationIndex]);
                if (cached is not null)
                {
                    return cached;
                }
            }

            var visitedSymbols = RentVisitedSymbols();
            TypeMetadataReference reference;
            try
            {
                reference = CreateTypeReference(
                    symbol: symbol,
                    nullableAnnotation: nullableAnnotation,
                    visitedSymbols: visitedSymbols);
            }
            finally
            {
                ReturnVisitedSymbols(visitedSymbols: visitedSymbols);
            }

            if (cacheNode is not null)
            {
                Volatile.Write(location: ref cacheNode.ByAnnotation[annotationIndex], value: reference);
            }

            return reference;
        }

        private TypeMetadataReference CreateTypeReference(
            ITypeSymbol symbol,
            NullableAnnotation nullableAnnotation,
            ISet<ITypeSymbol> visitedSymbols)
        {
            if (!visitedSymbols.Add(item: symbol))
            {
                return CreateShallowTypeReference(
                    symbol: symbol,
                    nullableAnnotation: nullableAnnotation);
            }

            try
            {
                return CreateTypeReferenceCore(
                    symbol: symbol,
                    nullableAnnotation: nullableAnnotation,
                    visitedSymbols: visitedSymbols);
            }
            finally
            {
                visitedSymbols.Remove(item: symbol);
            }
        }

        private TypeMetadataReference CreateTypeReferenceCore(
            ITypeSymbol symbol,
            NullableAnnotation nullableAnnotation,
            ISet<ITypeSymbol> visitedSymbols)
        {
            if (symbol is INamedTypeSymbol namedType
                && IsNullableValueType(symbol: namedType)
                && namedType.TypeArguments.Length == 1)
            {
                var inner = CreateTypeReference(
                    symbol: namedType.TypeArguments[index: 0],
                    nullableAnnotation: NullableAnnotation.Annotated,
                    visitedSymbols: visitedSymbols);
                return inner with
                {
                    IsNullable = true,
                };
            }

            var elementType = GetElementType(symbol: symbol, visitedSymbols: visitedSymbols);
            var typeArguments = symbol is INamedTypeSymbol genericType
                ? genericType.TypeArguments
                    .Zip(
                        second: genericType.TypeArgumentNullableAnnotations,
                        resultSelector: (argument, annotation) => CreateTypeReference(
                            symbol: argument,
                            nullableAnnotation: annotation,
                            visitedSymbols: visitedSymbols))
                    .ToArray()
                : [];

            return new TypeMetadataReference(
                Name: GetDisplayName(symbol: symbol),
                FullName: GetFullName(symbol: symbol),
                Namespace: GetNamespace(symbol: symbol),
                IsNullable: nullableAnnotation == NullableAnnotation.Annotated,
                IsCollection: elementType is not null,
                IsDictionary: IsDictionary(symbol: symbol),
                IsEnum: IsEnum(symbol: symbol),
                IsPrimitive: IsPrimitive(symbol: symbol),
                IsDateLike: IsDateLike(symbol: symbol),
                ElementType: elementType,
                TypeArguments: typeArguments)
            {
                Arity = symbol is INamedTypeSymbol namedSymbol ? namedSymbol.Arity : 0,
                AssemblyName = GetAssemblyName(symbol: symbol),
                ContainingType = GetContainingTypeReference(symbol: symbol),
                FileLocations = GetFileLocations(symbol: symbol),
                Interfaces = GetInterfaceReferences(symbol: symbol),
                IsValueTuple = IsValueTuple(symbol: symbol),
                TupleElements = GetTupleElements(symbol: symbol, visitedSymbols: visitedSymbols).ToArray(),
                EnumValues = symbol is INamedTypeSymbol enumType
                    ? GetEnumValues(symbol: enumType)
                    : [],
            };
        }

        private IReadOnlyList<string> GetFileLocations(ITypeSymbol symbol)
        {
            if (symbol is not INamedTypeSymbol { IsTupleType: false } namedSymbol
                || namedSymbol.TypeKind is not (Microsoft.CodeAnalysis.TypeKind.Class
                    or Microsoft.CodeAnalysis.TypeKind.Struct
                    or Microsoft.CodeAnalysis.TypeKind.Interface
                    or Microsoft.CodeAnalysis.TypeKind.Enum
                    or Microsoft.CodeAnalysis.TypeKind.Delegate))
            {
                return [];
            }

            return _fileLocations.GetValue(
                key: namedSymbol.OriginalDefinition,
                createValueCallback: static definition => new CachedFileLocations(
                    Value: definition.Locations
                        .Where(predicate: static location => location.IsInSource)
                        .Select(selector: static location => location.GetLineSpan().Path)
                        .Where(predicate: static path => !string.IsNullOrWhiteSpace(value: path))
                        .Select(selector: Path.GetFullPath)
                        .Distinct(comparer: StringComparer.OrdinalIgnoreCase)
                        .ToArray())).Value;
        }

        private IReadOnlyList<TypeMetadataReference> GetInterfaceReferences(ITypeSymbol symbol)
        {
            if (symbol.AllInterfaces.IsEmpty)
            {
                return [];
            }

            // Resolve each edge from its constructed symbol only when it is traversed.
            return new LazyTypeReferenceList(
                valueFactory: () => symbol.AllInterfaces
                    .Select(
                        selector: interfaceSymbol => CreateTypeReference(
                            symbol: interfaceSymbol,
                            nullableAnnotation: NullableAnnotation.NotAnnotated))
                    .ToArray());
        }

        private TypeMetadataReference CreateShallowTypeReference(
            ITypeSymbol symbol,
            NullableAnnotation nullableAnnotation) =>
            new(
                Name: GetDisplayName(symbol: symbol),
                FullName: GetFullName(symbol: symbol),
                Namespace: GetNamespace(symbol: symbol),
                IsNullable: nullableAnnotation == NullableAnnotation.Annotated,
                IsCollection: false,
                IsDictionary: IsDictionary(symbol: symbol),
                IsEnum: IsEnum(symbol: symbol),
                IsPrimitive: IsPrimitive(symbol: symbol),
                IsDateLike: IsDateLike(symbol: symbol),
                ElementType: null,
                TypeArguments: [])
            {
                Arity = symbol is INamedTypeSymbol namedSymbol ? namedSymbol.Arity : 0,
                AssemblyName = GetAssemblyName(symbol: symbol),
                ContainingType = GetContainingTypeReference(symbol: symbol),
                FileLocations = GetFileLocations(symbol: symbol),
                Interfaces = GetInterfaceReferences(symbol: symbol),
                IsValueTuple = IsValueTuple(symbol: symbol),
                EnumValues = symbol is INamedTypeSymbol enumType
                    ? GetEnumValues(symbol: enumType)
                    : [],
            };

        private TypeMetadataReference? GetContainingTypeReference(ITypeSymbol symbol) =>
            symbol is INamedTypeSymbol { ContainingType: { } containingType }
                ? CreateIdentityTypeReference(
                    symbol: containingType,
                    nullableAnnotation: NullableAnnotation.NotAnnotated)
                : null;

        private TypeMetadataReference CreateIdentityTypeReference(
            ITypeSymbol symbol,
            NullableAnnotation nullableAnnotation)
        {
            // Identity references do not depend on a visited-symbol path, unlike
            // collection/tuple references. Repeated generic arguments can share them.
            var annotationIndex = (int)nullableAnnotation;
            var node = _identityTypeReferences.GetOrAdd(key: symbol, valueFactory: static _ => new TypeReferenceCacheNode());
            if (annotationIndex >= 0 && annotationIndex < node.ByAnnotation.Length)
            {
                var cached = Volatile.Read(location: ref node.ByAnnotation[annotationIndex]);
                if (cached is not null)
                {
                    return cached;
                }
            }

            var reference = CreateIdentityTypeReferenceCore(symbol: symbol, nullableAnnotation: nullableAnnotation);
            if (annotationIndex >= 0 && annotationIndex < node.ByAnnotation.Length)
            {
                return Interlocked.CompareExchange(location1: ref node.ByAnnotation[annotationIndex], value: reference, comparand: null) ?? reference;
            }

            return reference;
        }

        private TypeMetadataReference CreateIdentityTypeReferenceCore(
            ITypeSymbol symbol,
            NullableAnnotation nullableAnnotation)
        {
            // Containing types contribute identity and arguments, not collection or interface graphs.
            var elementType = symbol is IArrayTypeSymbol arrayType
                ? CreateIdentityTypeReference(
                    symbol: arrayType.ElementType,
                    nullableAnnotation: arrayType.ElementNullableAnnotation)
                : null;
            var typeArguments = symbol is INamedTypeSymbol namedType
                ? namedType.TypeArguments
                    .Zip(
                        second: namedType.TypeArgumentNullableAnnotations,
                        resultSelector: (argument, annotation) => CreateIdentityTypeReference(
                            symbol: argument,
                            nullableAnnotation: annotation))
                    .ToArray()
                : [];

            return new TypeMetadataReference(
                Name: GetDisplayName(symbol: symbol),
                FullName: GetFullName(symbol: symbol),
                Namespace: GetNamespace(symbol: symbol),
                IsNullable: nullableAnnotation == NullableAnnotation.Annotated,
                IsCollection: elementType is not null,
                IsDictionary: IsDictionary(symbol: symbol),
                IsEnum: IsEnum(symbol: symbol),
                IsPrimitive: IsPrimitive(symbol: symbol),
                IsDateLike: IsDateLike(symbol: symbol),
                ElementType: elementType,
                TypeArguments: typeArguments)
            {
                Arity = symbol is INamedTypeSymbol namedSymbol ? namedSymbol.Arity : 0,
                AssemblyName = GetAssemblyName(symbol: symbol),
                ContainingType = GetContainingTypeReference(symbol: symbol),
                FileLocations = GetFileLocations(symbol: symbol),
            };
        }

        private TypeMetadataReference? GetElementType(
            ITypeSymbol symbol,
            ISet<ITypeSymbol> visitedSymbols)
        {
            if (symbol.SpecialType == SpecialType.System_String)
            {
                return null;
            }

            if (symbol is IArrayTypeSymbol arrayType)
            {
                return CreateTypeReference(
                    symbol: arrayType.ElementType,
                    nullableAnnotation: arrayType.ElementNullableAnnotation,
                    visitedSymbols: visitedSymbols);
            }

            if (symbol is not INamedTypeSymbol namedType)
            {
                return null;
            }

            var enumerable = _collectionShapes.GetValue(key: namedType, createValueCallback: GetCollectionShape).Enumerable;

            if (enumerable is null)
            {
                return null;
            }

            var annotation = enumerable.TypeArgumentNullableAnnotations.Length > 0
                ? enumerable.TypeArgumentNullableAnnotations[index: 0]
                : NullableAnnotation.NotAnnotated;
            return CreateTypeReference(
                symbol: enumerable.TypeArguments[index: 0],
                nullableAnnotation: annotation,
                visitedSymbols: visitedSymbols);
        }

        private bool IsDictionary(ITypeSymbol symbol)
        {
            return symbol is INamedTypeSymbol namedType
                && _collectionShapes.GetValue(key: namedType, createValueCallback: GetCollectionShape).IsDictionary;
        }

        private CollectionShape GetCollectionShape(INamedTypeSymbol symbol)
        {
            INamedTypeSymbol? enumerable = null;
            var isDictionary = false;
            foreach (var candidate in symbol.AllInterfaces.Concat(second: [symbol]))
            {
                var fullName = GetFullName(symbol: candidate.OriginalDefinition);
                if (enumerable is null
                    && candidate.TypeArguments.Length == 1
                    && fullName.Equals(value: "System.Collections.Generic.IEnumerable", comparisonType: StringComparison.Ordinal))
                {
                    enumerable = candidate;
                }

                if (candidate.TypeArguments.Length == 2
                    && (fullName.Equals(value: "System.Collections.Generic.IDictionary", comparisonType: StringComparison.Ordinal)
                        || fullName.Equals(value: "System.Collections.Generic.IReadOnlyDictionary", comparisonType: StringComparison.Ordinal)
                        || fullName.Equals(value: "System.Collections.Generic.Dictionary", comparisonType: StringComparison.Ordinal)))
                {
                    isDictionary = true;
                }
            }

            return new CollectionShape(Enumerable: enumerable, IsDictionary: isDictionary);
        }

        private IEnumerable<FieldMetadata> GetTupleElements(
            ITypeSymbol symbol,
            ISet<ITypeSymbol> visitedSymbols)
        {
            if (symbol is not INamedTypeSymbol { IsTupleType: true } tupleType)
            {
                return [];
            }

            return tupleType.TupleElements.Select(
                selector: field => new FieldMetadata(
                    Name: field.Name,
                    FullName: field.ToDisplayString(format: SymbolDisplayFormat.CSharpErrorMessageFormat),
                    Accessibility: MapAccessibility(accessibility: field.DeclaredAccessibility),
                    Type: CreateTypeReference(
                        symbol: field.Type,
                        nullableAnnotation: field.NullableAnnotation,
                        visitedSymbols: visitedSymbols),
                    Attributes: GetAttributes(attributes: field.GetAttributes()).ToArray(),
                    ParentTypeFullName: GetFullName(symbol: tupleType))
                {
                    AssemblyName = GetAssemblyName(symbol: field),
                });
        }

        private bool IsDateLike(ITypeSymbol symbol)
        {
            var fullName = GetFullName(symbol: symbol);
            return fullName.Equals(value: "System.DateTime", comparisonType: StringComparison.Ordinal)
                || fullName.Equals(value: "System.DateTimeOffset", comparisonType: StringComparison.Ordinal)
                || fullName.Equals(value: "System.DateOnly", comparisonType: StringComparison.Ordinal)
                || fullName.Equals(value: "System.TimeOnly", comparisonType: StringComparison.Ordinal)
                || fullName.Equals(value: "System.TimeSpan", comparisonType: StringComparison.Ordinal)
                || fullName.Equals(value: "NodaTime.Instant", comparisonType: StringComparison.Ordinal)
                || fullName.Equals(value: "NodaTime.LocalDate", comparisonType: StringComparison.Ordinal)
                || fullName.Equals(value: "NodaTime.LocalTime", comparisonType: StringComparison.Ordinal)
                || fullName.Equals(value: "NodaTime.LocalDateTime", comparisonType: StringComparison.Ordinal)
                || fullName.Equals(value: "NodaTime.OffsetDate", comparisonType: StringComparison.Ordinal)
                || fullName.Equals(value: "NodaTime.OffsetTime", comparisonType: StringComparison.Ordinal)
                || fullName.Equals(value: "NodaTime.OffsetDateTime", comparisonType: StringComparison.Ordinal)
                || fullName.Equals(value: "NodaTime.ZonedDateTime", comparisonType: StringComparison.Ordinal);
        }

        private bool IsNullableValueType(INamedTypeSymbol symbol)
        {
            return GetFullName(symbol: symbol.OriginalDefinition)
                .Equals(value: "System.Nullable", comparisonType: StringComparison.Ordinal);
        }

        private bool IsNullableAware(
            INamedTypeSymbol symbol,
            Compilation compilation)
        {
            return symbol.DeclaringSyntaxReferences.Any(
                predicate: reference =>
                {
                    var semanticModel = GetSemanticModel(compilation: compilation, syntaxTree: reference.SyntaxTree);
                    var nullableContext = semanticModel.GetNullableContext(position: reference.Span.Start);
                    return (nullableContext & NullableContext.AnnotationsEnabled) == NullableContext.AnnotationsEnabled;
                });
        }

        private SemanticModel GetSemanticModel(Compilation compilation, SyntaxTree syntaxTree)
        {
            if (!_semanticModels.TryGetValue(key: syntaxTree, value: out var model))
            {
                model = compilation.GetSemanticModel(syntaxTree: syntaxTree);
                _semanticModels.Add(key: syntaxTree, value: model);
            }

            return model;
        }

        private string GetFullName(ISymbol? symbol) =>
            symbol is null
                ? string.Empty
                : _fullNames.GetValue(key: symbol, createValueCallback: cachedSymbol => new CachedFullName(Value: GetFullNameCore(symbol: cachedSymbol))).Value;

        private string GetFullNameCore(ISymbol symbol)
        {
            if (symbol is ITypeSymbol typeSymbol)
            {
                var specialTypeName = GetSpecialTypeName(specialType: typeSymbol.SpecialType);
                if (specialTypeName is not null)
                {
                    return specialTypeName;
                }
            }

            if (symbol.ContainingType is not null)
            {
                return $"{GetFullName(symbol: symbol.ContainingType)}.{symbol.Name}";
            }

            var namespaceName = GetNamespace(symbol: symbol);
            return string.IsNullOrEmpty(value: namespaceName)
                ? symbol.Name
                : $"{namespaceName}.{symbol.Name}";
        }

        private TypeMetadataReference CreateVoidTypeReference(Compilation compilation)
        {
            var voidType = compilation.GetSpecialType(specialType: SpecialType.System_Void);
            return CreateTypeReference(symbol: voidType, nullableAnnotation: NullableAnnotation.NotAnnotated);
        }

        private sealed record CollectionShape(INamedTypeSymbol? Enumerable, bool IsDictionary);

        private sealed record CachedFullName(string Value);

        private sealed record CachedFileLocations(IReadOnlyList<string> Value);
    }
}
