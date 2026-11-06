using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Typewriter.Abstractions;

namespace Typewriter.Engine;

internal sealed class ProjectMetadataIndex
{
    private readonly Lazy<SourceFileDependencyIndex> _dependencyIndex;
    private readonly Lazy<IReadOnlyDictionary<object, TypeMetadata>> _parentTypesByItem;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<TypeMetadata>> _typesByHierarchyKey;
    private readonly IReadOnlyDictionary<(string FullName, int Arity), TypeMetadata> _unambiguousNestedTypesByFullNameAndArity;

    private ProjectMetadataIndex(
        ProjectMetadata metadata,
        IReadOnlyDictionary<string, TypeMetadata> typesByFullName,
        IReadOnlyDictionary<string, IReadOnlyList<TypeMetadata>> typesByHierarchyKey,
        IReadOnlyDictionary<(string FullName, int Arity), TypeMetadata> unambiguousNestedTypesByFullNameAndArity,
        IReadOnlyDictionary<string, MethodMetadata> methodsByFullName,
        IReadOnlyDictionary<string, PropertyMetadata> propertiesByFullName)
    {
        Metadata = metadata;
        TypesByFullName = typesByFullName;
        _typesByHierarchyKey = typesByHierarchyKey;
        _unambiguousNestedTypesByFullNameAndArity = unambiguousNestedTypesByFullNameAndArity;
        MethodsByFullName = methodsByFullName;
        PropertiesByFullName = propertiesByFullName;
        _parentTypesByItem = new Lazy<IReadOnlyDictionary<object, TypeMetadata>>(
            valueFactory: () => CreateParentTypeIndex(types: typesByHierarchyKey.Values.SelectMany(selector: types => types)));
        _dependencyIndex = new Lazy<SourceFileDependencyIndex>(
            valueFactory: () => SourceFileDependencyIndex.Build(metadata: metadata),
            mode: LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public ProjectMetadata Metadata { get; }

    public IReadOnlyDictionary<string, TypeMetadata> TypesByFullName { get; }

    public IReadOnlyDictionary<string, MethodMetadata> MethodsByFullName { get; }

    public IReadOnlyDictionary<string, PropertyMetadata> PropertiesByFullName { get; }

    public static ProjectMetadataIndex Create(ProjectMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(argument: metadata);

        var indexedTypes = EnumerateIndexedTypes(types: metadata.Types);
        var allTypes = indexedTypes.Select(selector: static entry => entry.Type).ToArray();
        return new ProjectMetadataIndex(
            metadata: metadata,
            typesByFullName: allTypes
                .GroupBy(keySelector: type => type.FullName, comparer: StringComparer.Ordinal)
                .ToDictionary(keySelector: group => group.Key, elementSelector: group => group.First(), comparer: StringComparer.Ordinal),
            typesByHierarchyKey: indexedTypes
                .GroupBy(keySelector: static entry => entry.HierarchyKey, comparer: StringComparer.Ordinal)
                .ToDictionary(
                    keySelector: static group => group.Key,
                    elementSelector: static group => (IReadOnlyList<TypeMetadata>)group.Select(selector: static entry => entry.Type).ToArray(),
                    comparer: StringComparer.Ordinal),
            unambiguousNestedTypesByFullNameAndArity: indexedTypes
                .Where(predicate: static entry => entry.IsNested)
                .Select(selector: static entry => entry.Type)
                .GroupBy(keySelector: static type => (type.FullName, type.TypeParameters.Count))
                .Where(predicate: static group => group.Count() == 1)
                .ToDictionary(keySelector: static group => group.Key, elementSelector: static group => group.First()),
            methodsByFullName: allTypes
                .SelectMany(selector: type => type.Methods)
                .GroupBy(keySelector: method => method.FullName, comparer: StringComparer.Ordinal)
                .ToDictionary(keySelector: group => group.Key, elementSelector: group => group.First(), comparer: StringComparer.Ordinal),
            propertiesByFullName: allTypes
                .SelectMany(selector: type => type.Properties)
                .GroupBy(keySelector: property => property.FullName, comparer: StringComparer.Ordinal)
                .ToDictionary(keySelector: group => group.Key, elementSelector: group => group.First(), comparer: StringComparer.Ordinal));
    }

    public bool TryResolveType(
        TypeMetadataReference reference,
        [NotNullWhen(returnValue: true)] out TypeMetadata? metadata)
    {
        ArgumentNullException.ThrowIfNull(argument: reference);

        var hierarchyKey = GetHierarchyKey(reference: reference);
        if (_typesByHierarchyKey.TryGetValue(key: hierarchyKey, value: out var candidates)
            && TrySelectCandidate(candidates: candidates, assemblyName: reference.AssemblyName, metadata: out metadata))
        {
            return true;
        }

        if (reference.ContainingType is null
            && _unambiguousNestedTypesByFullNameAndArity.TryGetValue(key: (reference.FullName, reference.Arity), value: out var nestedCandidate)
            && IsAssemblyCompatible(candidate: nestedCandidate, assemblyName: reference.AssemblyName))
        {
            metadata = nestedCandidate;
            return true;
        }

        metadata = null;
        return false;
    }

    public bool TryResolveParentType(
        string fullName,
        object? item,
        [NotNullWhen(returnValue: true)] out TypeMetadata? metadata)
    {
        if (string.IsNullOrWhiteSpace(value: fullName))
        {
            metadata = null;
            return false;
        }

        if (item is not null && _parentTypesByItem.Value.TryGetValue(key: item, value: out metadata))
        {
            return true;
        }

        return TypesByFullName.TryGetValue(key: fullName, value: out metadata);
    }

    /// <summary>
    /// Computes the transitive closure of source files affected by the changed source
    /// files: every changed file plus every file whose declared types reference a type
    /// declared in an affected file. Paths not tracked by this project are ignored.
    /// </summary>
    /// <param name="changedSourcePaths">The full paths of the changed source files.</param>
    /// <returns>The full paths of the affected source files (case-insensitive set).</returns>
    public IReadOnlyCollection<string> GetAffectedSourceFiles(IReadOnlyCollection<string> changedSourcePaths)
    {
        ArgumentNullException.ThrowIfNull(argument: changedSourcePaths);

        return _dependencyIndex.Value.GetAffectedSourceFiles(changedSourcePaths: changedSourcePaths);
    }

    private static IReadOnlyDictionary<object, TypeMetadata> CreateParentTypeIndex(IEnumerable<TypeMetadata> types)
    {
        var parents = new Dictionary<object, TypeMetadata>(comparer: ReferenceEqualityComparer.Instance);
        foreach (var type in types)
        {
            var children = EnumerateNestedTypes(type: type).Cast<object>()
                .Concat(second: type.Properties).Concat(second: type.Methods)
                .Concat(second: type.Constants).Concat(second: type.Fields)
                .Concat(second: type.StaticReadOnlyFields).Concat(second: type.Events)
                .Concat(second: type.Delegates).Concat(second: type.EnumValues);
            foreach (var child in children)
            {
                parents[child] = type;
            }
        }

        return parents;
    }

    private static bool TrySelectCandidate(
        IReadOnlyList<TypeMetadata> candidates,
        string assemblyName,
        [NotNullWhen(returnValue: true)] out TypeMetadata? metadata)
    {
        if (!string.IsNullOrWhiteSpace(value: assemblyName))
        {
            var exactMatch = candidates.FirstOrDefault(
                predicate: candidate => candidate.AssemblyName.Equals(value: assemblyName, comparisonType: StringComparison.Ordinal));
            if (exactMatch is not null)
            {
                metadata = exactMatch;
                return true;
            }
        }

        var compatibleCandidate = candidates.FirstOrDefault(
            predicate: candidate => IsAssemblyCompatible(candidate: candidate, assemblyName: assemblyName));
        if (compatibleCandidate is not null)
        {
            metadata = compatibleCandidate;
            return true;
        }

        metadata = null;
        return false;
    }

    private static bool IsAssemblyCompatible(
        TypeMetadata candidate,
        string assemblyName) =>
        string.IsNullOrWhiteSpace(value: assemblyName)
        || string.IsNullOrWhiteSpace(value: candidate.AssemblyName)
        || candidate.AssemblyName.Equals(value: assemblyName, comparisonType: StringComparison.Ordinal);

    private static string GetHierarchyKey(TypeMetadataReference reference)
    {
        var prefix = reference.ContainingType is not null
            ? $"{GetHierarchyKey(reference: reference.ContainingType)}.{reference.Name}"
            : reference.FullName;
        return FormatHierarchyKey(prefix: prefix, arity: reference.Arity);
    }

    private static string FormatHierarchyKey(
        string prefix,
        int arity) =>
        $"{prefix}`{arity.ToString(provider: CultureInfo.InvariantCulture)}";

    private static IReadOnlyList<IndexedTypeEntry> EnumerateIndexedTypes(IReadOnlyList<TypeMetadata> types)
    {
        var entries = new List<IndexedTypeEntry>();
        var visitedContainingTypes = new HashSet<string>(comparer: StringComparer.Ordinal);

        foreach (var type in types.Where(predicate: static type => string.IsNullOrWhiteSpace(value: type.ContainingTypeFullName)))
        {
            _ = visitedContainingTypes.Add(item: type.FullName);
            var key = FormatHierarchyKey(prefix: type.FullName, arity: type.TypeParameters.Count);
            entries.Add(item: new IndexedTypeEntry(HierarchyKey: key, Type: type, IsNested: false));
            AddNestedTypes(
                parentKey: key,
                nestedTypes: EnumerateNestedTypes(type: type),
                entries: entries,
                visitedContainingTypes: visitedContainingTypes);
        }

        foreach (var type in types.Where(predicate: static type => !string.IsNullOrWhiteSpace(value: type.ContainingTypeFullName)))
        {
            if (visitedContainingTypes.Contains(item: type.ContainingTypeFullName))
            {
                continue;
            }

            _ = visitedContainingTypes.Add(item: type.FullName);
            var key = FormatHierarchyKey(prefix: type.FullName, arity: type.TypeParameters.Count);
            entries.Add(item: new IndexedTypeEntry(HierarchyKey: key, Type: type, IsNested: true));
            AddNestedTypes(
                parentKey: key,
                nestedTypes: EnumerateNestedTypes(type: type),
                entries: entries,
                visitedContainingTypes: visitedContainingTypes);
        }

        return entries;
    }

    private static void AddNestedTypes(
        string parentKey,
        IEnumerable<TypeMetadata> nestedTypes,
        ICollection<IndexedTypeEntry> entries,
        ISet<string> visitedContainingTypes)
    {
        foreach (var nested in nestedTypes)
        {
            _ = visitedContainingTypes.Add(item: nested.FullName);
            var nestedKey = FormatHierarchyKey(prefix: $"{parentKey}.{nested.Name}", arity: nested.TypeParameters.Count);
            entries.Add(item: new IndexedTypeEntry(HierarchyKey: nestedKey, Type: nested, IsNested: true));
            AddNestedTypes(
                parentKey: nestedKey,
                nestedTypes: EnumerateNestedTypes(type: nested),
                entries: entries,
                visitedContainingTypes: visitedContainingTypes);
        }
    }

    private static IEnumerable<TypeMetadata> EnumerateNestedTypes(TypeMetadata type) =>
        type.NestedClasses
            .Concat(second: type.NestedRecords)
            .Concat(second: type.NestedStructs)
            .Concat(second: type.NestedEnums)
            .Concat(second: type.NestedInterfaces);

    private readonly record struct IndexedTypeEntry(
        string HierarchyKey,
        TypeMetadata Type,
        bool IsNested);

    private sealed class SourceFileDependencyIndex
    {
        private readonly IReadOnlyDictionary<string, HashSet<string>> _declaredTypesBySourceFile;
        private readonly IReadOnlyDictionary<string, HashSet<string>> _referencedTypesBySourceFile;

        private SourceFileDependencyIndex(
            IReadOnlyDictionary<string, HashSet<string>> declaredTypesBySourceFile,
            IReadOnlyDictionary<string, HashSet<string>> referencedTypesBySourceFile)
        {
            _declaredTypesBySourceFile = declaredTypesBySourceFile;
            _referencedTypesBySourceFile = referencedTypesBySourceFile;
        }

        public static SourceFileDependencyIndex Build(ProjectMetadata metadata)
        {
            var declaredTypesBySourceFile = new Dictionary<string, HashSet<string>>(comparer: StringComparer.OrdinalIgnoreCase);
            var referencedTypesBySourceFile = new Dictionary<string, HashSet<string>>(comparer: StringComparer.OrdinalIgnoreCase);
            foreach (var sourceFile in metadata.SourceFiles)
            {
                var declaredTypes = new HashSet<string>(comparer: StringComparer.Ordinal);
                var referencedTypes = new HashSet<string>(comparer: StringComparer.Ordinal);
                var visitedReferences = new HashSet<object>(comparer: ReferenceEqualityComparer.Instance);
                foreach (var type in sourceFile.Types)
                {
                    CollectType(type: type, declaredTypes: declaredTypes, referencedTypes: referencedTypes, visitedReferences: visitedReferences);
                }

                foreach (var delegateMetadata in sourceFile.Delegates)
                {
                    _ = declaredTypes.Add(item: delegateMetadata.FullName);
                    CollectDelegate(delegateMetadata: delegateMetadata, referencedTypes: referencedTypes, visitedReferences: visitedReferences);
                }

                var sourcePath = sourceFile.Path;
                declaredTypesBySourceFile[key: sourcePath] = declaredTypes;
                referencedTypesBySourceFile[key: sourcePath] = referencedTypes;
            }

            return new SourceFileDependencyIndex(
                declaredTypesBySourceFile: declaredTypesBySourceFile,
                referencedTypesBySourceFile: referencedTypesBySourceFile);
        }

        public IReadOnlyCollection<string> GetAffectedSourceFiles(IReadOnlyCollection<string> changedSourcePaths)
        {
            var affectedFiles = new HashSet<string>(comparer: StringComparer.OrdinalIgnoreCase);
            var dirtyTypes = new HashSet<string>(comparer: StringComparer.Ordinal);
            foreach (var changedPath in changedSourcePaths)
            {
                if (_declaredTypesBySourceFile.TryGetValue(key: changedPath, value: out var declaredTypes)
                    && affectedFiles.Add(item: changedPath))
                {
                    dirtyTypes.UnionWith(other: declaredTypes);
                }
            }

            var expanded = affectedFiles.Count > 0;
            while (expanded)
            {
                expanded = false;
                foreach (var pair in _referencedTypesBySourceFile)
                {
                    if (affectedFiles.Contains(item: pair.Key) || !pair.Value.Overlaps(other: dirtyTypes))
                    {
                        continue;
                    }

                    _ = affectedFiles.Add(item: pair.Key);
                    if (_declaredTypesBySourceFile.TryGetValue(key: pair.Key, value: out var declaredTypes))
                    {
                        dirtyTypes.UnionWith(other: declaredTypes);
                    }

                    expanded = true;
                }
            }

            return affectedFiles;
        }

        private static void CollectType(
            TypeMetadata type,
            ISet<string> declaredTypes,
            ISet<string> referencedTypes,
            ISet<object> visitedReferences)
        {
            _ = declaredTypes.Add(item: type.FullName);
            CollectReferences(references: type.BaseTypes, referencedTypes: referencedTypes, visitedReferences: visitedReferences);
            CollectReferences(references: type.TypeArguments, referencedTypes: referencedTypes, visitedReferences: visitedReferences);
            CollectAttributes(attributes: type.Attributes, referencedTypes: referencedTypes, visitedReferences: visitedReferences);
            CollectMembers(type: type, referencedTypes: referencedTypes, visitedReferences: visitedReferences);
            foreach (var delegateMetadata in type.Delegates)
            {
                _ = declaredTypes.Add(item: delegateMetadata.FullName);
                CollectDelegate(delegateMetadata: delegateMetadata, referencedTypes: referencedTypes, visitedReferences: visitedReferences);
            }

            foreach (var nestedType in EnumerateNestedTypes(type: type))
            {
                CollectType(type: nestedType, declaredTypes: declaredTypes, referencedTypes: referencedTypes, visitedReferences: visitedReferences);
            }
        }

        private static void CollectMembers(
            TypeMetadata type,
            ISet<string> referencedTypes,
            ISet<object> visitedReferences)
        {
            foreach (var property in type.Properties)
            {
                CollectReference(reference: property.Type, referencedTypes: referencedTypes, visitedReferences: visitedReferences);
                CollectAttributes(attributes: property.Attributes, referencedTypes: referencedTypes, visitedReferences: visitedReferences);
                CollectParameters(parameters: property.Parameters, referencedTypes: referencedTypes, visitedReferences: visitedReferences);
            }

            foreach (var method in type.Methods)
            {
                CollectReference(reference: method.ReturnType, referencedTypes: referencedTypes, visitedReferences: visitedReferences);
                CollectAttributes(attributes: method.Attributes, referencedTypes: referencedTypes, visitedReferences: visitedReferences);
                CollectParameters(parameters: method.Parameters, referencedTypes: referencedTypes, visitedReferences: visitedReferences);
            }

            foreach (var constant in type.Constants)
            {
                CollectReference(reference: constant.Type, referencedTypes: referencedTypes, visitedReferences: visitedReferences);
                CollectAttributes(attributes: constant.Attributes, referencedTypes: referencedTypes, visitedReferences: visitedReferences);
            }

            foreach (var field in type.Fields)
            {
                CollectReference(reference: field.Type, referencedTypes: referencedTypes, visitedReferences: visitedReferences);
                CollectAttributes(attributes: field.Attributes, referencedTypes: referencedTypes, visitedReferences: visitedReferences);
            }

            foreach (var staticReadOnlyField in type.StaticReadOnlyFields)
            {
                CollectReference(reference: staticReadOnlyField.Type, referencedTypes: referencedTypes, visitedReferences: visitedReferences);
                CollectAttributes(attributes: staticReadOnlyField.Attributes, referencedTypes: referencedTypes, visitedReferences: visitedReferences);
            }

            foreach (var @event in type.Events)
            {
                CollectReference(reference: @event.Type, referencedTypes: referencedTypes, visitedReferences: visitedReferences);
                CollectAttributes(attributes: @event.Attributes, referencedTypes: referencedTypes, visitedReferences: visitedReferences);
            }
        }

        private static void CollectDelegate(
            DelegateMetadata delegateMetadata,
            ISet<string> referencedTypes,
            ISet<object> visitedReferences)
        {
            CollectReference(reference: delegateMetadata.ReturnType, referencedTypes: referencedTypes, visitedReferences: visitedReferences);
            CollectAttributes(attributes: delegateMetadata.Attributes, referencedTypes: referencedTypes, visitedReferences: visitedReferences);
            CollectParameters(parameters: delegateMetadata.Parameters, referencedTypes: referencedTypes, visitedReferences: visitedReferences);
        }

        private static void CollectParameters(
            IReadOnlyList<ParameterMetadata> parameters,
            ISet<string> referencedTypes,
            ISet<object> visitedReferences)
        {
            foreach (var parameter in parameters)
            {
                CollectReference(reference: parameter.Type, referencedTypes: referencedTypes, visitedReferences: visitedReferences);
                CollectAttributes(attributes: parameter.Attributes, referencedTypes: referencedTypes, visitedReferences: visitedReferences);
            }
        }

        private static void CollectAttributes(
            IReadOnlyList<AttributeMetadata> attributes,
            ISet<string> referencedTypes,
            ISet<object> visitedReferences)
        {
            foreach (var attribute in attributes)
            {
                _ = referencedTypes.Add(item: attribute.FullName);
                CollectReference(reference: attribute.Type, referencedTypes: referencedTypes, visitedReferences: visitedReferences);
                foreach (var argument in attribute.Arguments)
                {
                    CollectReference(reference: argument.Type, referencedTypes: referencedTypes, visitedReferences: visitedReferences);
                    CollectReference(reference: argument.TypeValue, referencedTypes: referencedTypes, visitedReferences: visitedReferences);
                }
            }
        }

        private static void CollectReferences(
            IReadOnlyList<TypeMetadataReference> references,
            ISet<string> referencedTypes,
            ISet<object> visitedReferences)
        {
            foreach (var reference in references)
            {
                CollectReference(reference: reference, referencedTypes: referencedTypes, visitedReferences: visitedReferences);
            }
        }

        private static void CollectReference(
            TypeMetadataReference? reference,
            ISet<string> referencedTypes,
            ISet<object> visitedReferences)
        {
            if (reference is null)
            {
                return;
            }

            var expandedInterfaces = new HashSet<(string AssemblyName, string FullName, int Arity)>();
            var pending = new Stack<TypeMetadataReference>();
            pending.Push(item: reference);
            while (pending.Count > 0)
            {
                var current = pending.Pop();
                if (!visitedReferences.Add(item: current))
                {
                    continue;
                }

                _ = referencedTypes.Add(item: current.FullName);
                if (current.ElementType is not null)
                {
                    pending.Push(item: current.ElementType);
                }

                // Nested types do not carry their containing type's arguments in TypeArguments.
                if (current.ContainingType is not null)
                {
                    pending.Push(item: current.ContainingType);
                }

                foreach (var typeArgument in current.TypeArguments)
                {
                    pending.Push(item: typeArgument);
                }

                // Recursive contracts can keep constructing larger generic arguments.
                // Expand each declaration once while still visiting every argument.
                if (expandedInterfaces.Add(item: (current.AssemblyName, current.FullName, current.Arity)))
                {
                    foreach (var interfaceType in current.Interfaces)
                    {
                        pending.Push(item: interfaceType);
                    }
                }

                foreach (var tupleElement in current.TupleElements)
                {
                    pending.Push(item: tupleElement.Type);
                }
            }
        }
    }
}
