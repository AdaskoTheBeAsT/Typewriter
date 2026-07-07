using Typewriter.Abstractions;

namespace Typewriter.Engine;

internal sealed class ProjectMetadataIndex
{
    private readonly Lazy<SourceFileDependencyIndex> _dependencyIndex;

    private ProjectMetadataIndex(
        ProjectMetadata metadata,
        IReadOnlyDictionary<string, TypeMetadata> typesByFullName,
        IReadOnlyDictionary<string, MethodMetadata> methodsByFullName,
        IReadOnlyDictionary<string, PropertyMetadata> propertiesByFullName)
    {
        TypesByFullName = typesByFullName;
        MethodsByFullName = methodsByFullName;
        PropertiesByFullName = propertiesByFullName;
        _dependencyIndex = new Lazy<SourceFileDependencyIndex>(
            valueFactory: () => SourceFileDependencyIndex.Build(metadata: metadata),
            mode: LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public IReadOnlyDictionary<string, TypeMetadata> TypesByFullName { get; }

    public IReadOnlyDictionary<string, MethodMetadata> MethodsByFullName { get; }

    public IReadOnlyDictionary<string, PropertyMetadata> PropertiesByFullName { get; }

    public static ProjectMetadataIndex Create(ProjectMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(argument: metadata);

        return new ProjectMetadataIndex(
            metadata: metadata,
            typesByFullName: metadata.Types
                .GroupBy(keySelector: type => type.FullName, comparer: StringComparer.Ordinal)
                .ToDictionary(keySelector: group => group.Key, elementSelector: group => group.First(), comparer: StringComparer.Ordinal),
            methodsByFullName: metadata.Types
                .SelectMany(selector: type => type.Methods)
                .GroupBy(keySelector: method => method.FullName, comparer: StringComparer.Ordinal)
                .ToDictionary(keySelector: group => group.Key, elementSelector: group => group.First(), comparer: StringComparer.Ordinal),
            propertiesByFullName: metadata.Types
                .SelectMany(selector: type => type.Properties)
                .GroupBy(keySelector: property => property.FullName, comparer: StringComparer.Ordinal)
                .ToDictionary(keySelector: group => group.Key, elementSelector: group => group.First(), comparer: StringComparer.Ordinal));
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

        private static IEnumerable<TypeMetadata> EnumerateNestedTypes(TypeMetadata type) =>
            type.NestedClasses
                .Concat(second: type.NestedRecords)
                .Concat(second: type.NestedStructs)
                .Concat(second: type.NestedEnums)
                .Concat(second: type.NestedInterfaces);

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

                foreach (var typeArgument in current.TypeArguments)
                {
                    pending.Push(item: typeArgument);
                }

                foreach (var tupleElement in current.TupleElements)
                {
                    pending.Push(item: tupleElement.Type);
                }
            }
        }
    }
}
