namespace Typewriter.Roslyn;

/// <summary>
/// Shares dirty-path membership and ancestor discovery between project manifests.
/// </summary>
internal sealed class MetadataDirtyPathIndex
{
    private readonly Dictionary<string, int> _paths = new(comparer: StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _directories = new(comparer: StringComparer.OrdinalIgnoreCase);
    private readonly string[] _orderedPaths;

    public MetadataDirtyPathIndex(IReadOnlyList<string> paths)
    {
        _orderedPaths = paths.ToArray();
        for (var index = 0; index < _orderedPaths.Length; index++)
        {
            var path = _orderedPaths[index];
            _paths.TryAdd(key: path, value: index);
            var directory = (string?)path;
            while (!string.IsNullOrEmpty(value: directory) && _directories.TryAdd(key: directory, value: index))
            {
                directory = Path.GetDirectoryName(path: directory);
            }
        }
    }

    public int Count => _paths.Count;

    public IEnumerable<string> FindRelevantPaths<TFile, TDirectory>(
        IReadOnlyDictionary<string, TFile> sourceFiles,
        IReadOnlyDictionary<string, TFile> shapeFiles,
        IReadOnlyDictionary<string, TDirectory> directories) =>
        FindMatches(paths: _paths, inputs: sourceFiles)
            .Concat(second: FindMatches(paths: _paths, inputs: shapeFiles))

            // Only the first dirty path reaching each tracked directory is needed:
            // validation checks that directory once, preserving its original precedence.
            .Concat(second: FindMatches(paths: _directories, inputs: directories))
            .Distinct()
            .Order()
            .Select(selector: index => _orderedPaths[index]);

    private static IEnumerable<int> FindMatches<T>(
        IReadOnlyDictionary<string, int> paths,
        IReadOnlyDictionary<string, T> inputs)
    {
        // Avoid projects × dirty-paths work when either side is much smaller.
        if (paths.Count <= inputs.Count)
        {
            foreach (var (path, index) in paths)
            {
                if (inputs.ContainsKey(key: path))
                {
                    yield return index;
                }
            }
        }
        else
        {
            foreach (var input in inputs)
            {
                if (paths.TryGetValue(key: input.Key, value: out var index))
                {
                    yield return index;
                }
            }
        }
    }
}
