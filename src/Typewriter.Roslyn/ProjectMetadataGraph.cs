using Typewriter.Abstractions;

namespace Typewriter.Roslyn;

/// <summary>
/// Retains local project payloads without copying the transitive metadata into every
/// ancestor. Only the requested public view is flattened.
/// </summary>
internal sealed class ProjectMetadataGraph
{
    private readonly Lock _mergeLock = new();
    private readonly bool _hasDiagnostics;
    private ProjectMetadata? _mergedMetadata;

    public ProjectMetadataGraph(
        ProjectMetadata localMetadata,
        IReadOnlyList<ProjectMetadataGraph> references)
    {
        LocalMetadata = localMetadata;
        References = references;
        HasErrors = localMetadata.Diagnostics.Any(predicate: diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            || references.Any(predicate: reference => reference.HasErrors);
        _hasDiagnostics = localMetadata.Diagnostics.Count > 0
            || references.Any(predicate: reference => reference._hasDiagnostics);
    }

    public ProjectMetadata LocalMetadata { get; }

    public IReadOnlyList<ProjectMetadataGraph> References { get; }

    public bool HasErrors { get; }

    public ProjectMetadata GetMetadata(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (References.Count == 0)
        {
            return LocalMetadata;
        }

        lock (_mergeLock)
        {
            return _mergedMetadata ??= Merge(cancellationToken: cancellationToken);
        }
    }

    private static void PushReferences(
        Stack<ProjectMetadataGraph> pending,
        IReadOnlyList<ProjectMetadataGraph> references)
    {
        for (var index = references.Count - 1; index >= 0; index--)
        {
            pending.Push(item: references[index]);
        }
    }

    private static string GetIdentity(string fullName, string assemblyName) =>
        string.Concat(str0: fullName, str1: "\u001F", str2: assemblyName);

    private ProjectMetadata Merge(CancellationToken cancellationToken)
    {
        var sourceFiles = new Dictionary<string, SourceFileMetadata>(comparer: StringComparer.OrdinalIgnoreCase);
        var types = new Dictionary<string, TypeMetadata>(comparer: StringComparer.Ordinal);
        var delegates = new Dictionary<string, DelegateMetadata>(comparer: StringComparer.Ordinal);
        var visited = new HashSet<ProjectMetadataGraph>(comparer: ReferenceEqualityComparer.Instance);
        var pending = new Stack<ProjectMetadataGraph>();
        pending.Push(item: this);
        while (pending.TryPop(result: out var current))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!visited.Add(item: current))
            {
                continue;
            }

            foreach (var sourceFile in current.LocalMetadata.SourceFiles)
            {
                sourceFiles.TryAdd(key: Path.GetFullPath(path: sourceFile.Path), value: sourceFile);
            }

            foreach (var type in current.LocalMetadata.Types)
            {
                types.TryAdd(key: GetIdentity(fullName: type.FullName, assemblyName: type.AssemblyName), value: type);
            }

            foreach (var @delegate in current.LocalMetadata.Delegates)
            {
                delegates.TryAdd(key: GetIdentity(fullName: @delegate.FullName, assemblyName: @delegate.AssemblyName), value: @delegate);
            }

            PushReferences(pending: pending, references: current.References);
        }

        return new ProjectMetadata(
            ProjectPath: LocalMetadata.ProjectPath,
            SourceFiles: sourceFiles.Values.OrderBy(keySelector: sourceFile => sourceFile.Path, comparer: StringComparer.OrdinalIgnoreCase).ToArray(),
            Types: types.Values
                .OrderBy(keySelector: type => type.FullName, comparer: StringComparer.Ordinal)
                .ThenBy(keySelector: type => type.AssemblyName, comparer: StringComparer.Ordinal)
                .ToArray(),
            Diagnostics: GetDiagnostics(cancellationToken: cancellationToken))
        {
            Delegates = delegates.Values
                .OrderBy(keySelector: type => type.FullName, comparer: StringComparer.Ordinal)
                .ThenBy(keySelector: type => type.AssemblyName, comparer: StringComparer.Ordinal)
                .ToArray(),
        };
    }

    private IReadOnlyList<GenerationDiagnostic> GetDiagnostics(CancellationToken cancellationToken)
    {
        if (!_hasDiagnostics)
        {
            return [];
        }

        // Diagnostics historically preserve reference-path multiplicity. Do not use
        // the metadata visited set here, but prune diagnostic-free subgraphs.
        var diagnostics = new List<GenerationDiagnostic>();
        var pending = new Stack<ProjectMetadataGraph>();
        pending.Push(item: this);
        while (pending.TryPop(result: out var current))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!current._hasDiagnostics)
            {
                continue;
            }

            diagnostics.AddRange(collection: current.LocalMetadata.Diagnostics);
            PushReferences(pending: pending, references: current.References);
        }

        return diagnostics.ToArray();
    }
}
