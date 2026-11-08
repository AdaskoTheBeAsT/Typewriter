using Typewriter.Abstractions;

namespace Typewriter.Engine;

internal sealed class DeclarationSourceFileIndex(ProjectMetadata metadata)
{
    private readonly Lazy<IReadOnlyDictionary<string, SourceFileMetadata>> _files = new(
        valueFactory: () => CreateFiles(metadata: metadata));

    public SourceFileMetadata GetSource(string path) =>
        _files.Value.TryGetValue(key: path, value: out var source)
            ? source
            : new SourceFileMetadata(Path: path, Types: []);

    private static IReadOnlyDictionary<string, SourceFileMetadata> CreateFiles(ProjectMetadata metadata)
    {
        var typesByPath = new Dictionary<string, List<TypeMetadata>>(comparer: StringComparer.OrdinalIgnoreCase);
        foreach (var type in metadata.Types)
        {
            var paths = type.FileLocations
                .Append(element: type.Location?.Path ?? string.Empty)
                .Where(predicate: path => !string.IsNullOrWhiteSpace(value: path))
                .Distinct(comparer: StringComparer.OrdinalIgnoreCase);
            foreach (var path in paths)
            {
                if (!typesByPath.TryGetValue(key: path, value: out var types))
                {
                    types = [];
                    typesByPath.Add(key: path, value: types);
                }

                types.Add(item: type);
            }
        }

        var delegatesByPath = metadata.Delegates
            .Where(predicate: item => !string.IsNullOrWhiteSpace(value: item.Location?.Path))
            .GroupBy(keySelector: item => item.Location!.Path, comparer: StringComparer.OrdinalIgnoreCase)
            .ToDictionary(keySelector: group => group.Key, elementSelector: group => group.ToArray(), comparer: StringComparer.OrdinalIgnoreCase);
        return typesByPath.Keys.Concat(second: delegatesByPath.Keys)
            .Distinct(comparer: StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                keySelector: path => path,
                elementSelector: path => new SourceFileMetadata(
                    Path: path,
                    Types: typesByPath.TryGetValue(key: path, value: out var types) ? types : [])
                {
                    Delegates = delegatesByPath.TryGetValue(key: path, value: out var delegates) ? delegates : [],
                },
                comparer: StringComparer.OrdinalIgnoreCase);
    }
}
