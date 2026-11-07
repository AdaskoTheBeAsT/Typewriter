using System.Runtime.CompilerServices;
using Typewriter.Abstractions;

namespace Typewriter.Engine;

internal sealed class ProjectSourceFileIndex
{
    private readonly IReadOnlyList<SourceFileMetadata> _sourceFiles;
    private readonly ProjectSourceFileIndex? _parent;
    private readonly Lazy<Dictionary<string, SourceEntry>> _sourcesByPath;
    private readonly Lazy<Dictionary<DeclarationKey, List<SourceEntry>>> _sourcesByDeclaration;
    private readonly ConditionalWeakTable<SourceFileMetadata, HashSet<DeclarationKey>> _declarationsBySource;
    private readonly ConditionalWeakTable<ProjectMetadata, DeclarationSourceFileIndex> _fallbackSources;

    public ProjectSourceFileIndex(IReadOnlyList<SourceFileMetadata> sourceFiles)
        : this(sourceFiles: sourceFiles, parent: null)
    {
    }

    private ProjectSourceFileIndex(
        IReadOnlyList<SourceFileMetadata> sourceFiles,
        ProjectSourceFileIndex? parent)
    {
        _sourceFiles = sourceFiles;
        _parent = parent;
        _declarationsBySource = parent?._declarationsBySource ?? [];
        _fallbackSources = parent?._fallbackSources ?? [];
        _sourcesByPath = new Lazy<Dictionary<string, SourceEntry>>(valueFactory: CreateSourceMap);
        _sourcesByDeclaration = new Lazy<Dictionary<DeclarationKey, List<SourceEntry>>>(valueFactory: CreateDeclarationMap);
    }

    public ProjectSourceFileIndex WithOverrides(IReadOnlyList<SourceFileMetadata> sourceFiles) =>
        sourceFiles.Count == 0 || ReferenceEquals(objA: sourceFiles, objB: _sourceFiles)
            ? this
            : new ProjectSourceFileIndex(sourceFiles: sourceFiles, parent: this);

    public SourceFileMetadata? Find(TypeMetadata type, string? filePath, SourceFileMetadata? preferredSource = null) =>
        Find(key: DeclarationKey.Create(type: type), filePath: filePath, preferredSource: preferredSource);

    public SourceFileMetadata? Find(DelegateMetadata @delegate, string? filePath, SourceFileMetadata? preferredSource = null) =>
        Find(key: DeclarationKey.Create(@delegate: @delegate), filePath: filePath, preferredSource: preferredSource);

    public SourceFileMetadata GetFallbackSource(ProjectMetadata metadata, string filePath) =>
        _fallbackSources.GetValue(
            key: metadata,
            createValueCallback: static project => new DeclarationSourceFileIndex(metadata: project))
            .GetSource(path: filePath);

    private static HashSet<DeclarationKey> GetDeclarations(SourceFileMetadata source) =>
        source.Types.Select(selector: DeclarationKey.Create)
            .Concat(second: source.Delegates.Select(selector: DeclarationKey.Create))
            .ToHashSet();

    private SourceFileMetadata? Find(DeclarationKey key, string? filePath, SourceFileMetadata? preferredSource)
    {
        // Partial declarations prefer the single source currently being rendered.
        if (preferredSource is not null && Contains(source: preferredSource, key: key))
        {
            return preferredSource;
        }

        if (filePath is not null && TryGetSource(path: filePath, entry: out var located))
        {
            return located.Source;
        }

        return FindFirst(key: key, effectiveIndex: this)?.Source;
    }

    private SourceEntry? FindFirst(DeclarationKey key, ProjectSourceFileIndex effectiveIndex)
    {
        SourceEntry? first = null;
        if (_sourcesByDeclaration.Value.TryGetValue(key: key, value: out var candidates))
        {
            foreach (var candidate in candidates.Select(selector: entry => entry.Source))
            {
                if (effectiveIndex.TryGetSource(path: candidate.Path, entry: out var effective)
                    && ReferenceEquals(objA: candidate, objB: effective.Source))
                {
                    first = effective;
                    break;
                }
            }
        }

        var inherited = _parent?.FindFirst(key: key, effectiveIndex: effectiveIndex);
        return inherited is not null && (first is null || inherited.Value.Order < first.Value.Order)
            ? inherited
            : first;
    }

    private bool Contains(SourceFileMetadata source, DeclarationKey key) =>
        _declarationsBySource.GetValue(key: source, createValueCallback: GetDeclarations).Contains(item: key);

    private bool TryGetSource(string path, out SourceEntry entry) =>
        _sourcesByPath.Value.TryGetValue(key: path, value: out entry)
        || (_parent is not null && _parent.TryGetSource(path: path, entry: out entry));

    private int GetSourceCount() =>
        _parent is null
            ? _sourcesByPath.Value.Count
            : _parent.GetSourceCount()
                + _sourcesByPath.Value.Count(predicate: entry => !_parent.TryGetSource(path: entry.Key, entry: out _));

    private Dictionary<string, SourceEntry> CreateSourceMap()
    {
        var sources = new Dictionary<string, SourceEntry>(comparer: StringComparer.OrdinalIgnoreCase);
        var nextOrder = _parent?.GetSourceCount() ?? 0;
        foreach (var source in _sourceFiles)
        {
            var order = sources.TryGetValue(key: source.Path, value: out var previous)
                || (_parent is not null && _parent.TryGetSource(path: source.Path, entry: out previous))
                    ? previous.Order
                    : nextOrder++;
            sources[source.Path] = new SourceEntry(Source: source, Order: order);
        }

        return sources;
    }

    private Dictionary<DeclarationKey, List<SourceEntry>> CreateDeclarationMap()
    {
        var declarations = new Dictionary<DeclarationKey, List<SourceEntry>>();
        foreach (var entry in _sourcesByPath.Value.Values)
        {
            foreach (var key in _declarationsBySource.GetValue(key: entry.Source, createValueCallback: GetDeclarations))
            {
                if (!declarations.TryGetValue(key: key, value: out var entries))
                {
                    entries = [];
                    declarations.Add(key: key, value: entries);
                }

                entries.Add(item: entry);
            }
        }

        foreach (var entries in declarations.Values)
        {
            entries.Sort(comparison: static (left, right) => left.Order.CompareTo(value: right.Order));
        }

        return declarations;
    }

    private readonly record struct SourceEntry(SourceFileMetadata Source, int Order);

    private readonly record struct DeclarationKey(string FullName, string AssemblyName, TypeMetadataKind? Kind, int Arity)
    {
        public static DeclarationKey Create(TypeMetadata type) =>
            new(FullName: type.FullName, AssemblyName: type.AssemblyName, Kind: type.Kind, Arity: type.TypeParameters.Count);

        public static DeclarationKey Create(DelegateMetadata @delegate) =>
            new(FullName: @delegate.FullName, AssemblyName: @delegate.AssemblyName, Kind: null, Arity: @delegate.TypeParameters.Count);
    }
}
