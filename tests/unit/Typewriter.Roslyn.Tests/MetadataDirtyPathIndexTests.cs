using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using AwesomeAssertions;
using Xunit;

namespace Typewriter.Roslyn.Tests;

public sealed class MetadataDirtyPathIndexTests
{
    [Theory]
    [InlineData(1, 4096)]
    [InlineData(4096, 1)]
    public void FileIntersectionProbesOnlyTheSmallerSide(int dirtyCount, int inputCount)
    {
        var root = Path.Combine(path1: Path.GetTempPath(), path2: "Typewriter.DirtyPathIndex.Tests");
        var common = Path.Combine(path1: root, path2: "Common.cs");
        var paths = Enumerable.Range(start: 0, count: dirtyCount)
            .Select(selector: index => index == 0 ? common : Path.Combine(path1: root, path2: "Dirty" + index.ToString(provider: CultureInfo.InvariantCulture) + ".cs"))
            .ToArray();
        var inputs = new CountingDictionary<int>(
            values: Enumerable.Range(start: 0, count: inputCount)
                .ToDictionary(
                    keySelector: index => index == 0 ? common : Path.Combine(path1: root, path2: "Input" + index.ToString(provider: CultureInfo.InvariantCulture) + ".cs"),
                    elementSelector: index => index,
                    comparer: StringComparer.OrdinalIgnoreCase));
        var empty = new Dictionary<string, int>(comparer: StringComparer.OrdinalIgnoreCase);
        var dirtyPaths = new MetadataDirtyPathIndex(paths: paths);

        var relevant = dirtyPaths.FindRelevantPaths(sourceFiles: inputs, shapeFiles: empty, directories: empty).ToArray();

        relevant.Should().Equal(common);
        inputs.Operations.Should().Be(expected: Math.Min(val1: dirtyCount, val2: inputCount));
    }

    [Fact]
    public void RelevantPathsPreserveOriginalOrderSpellingAndFirstDirectoryTouch()
    {
        var root = Path.Combine(path1: Path.GetTempPath(), path2: "Typewriter.DirtyPathIndex.Tests");
        var models = Path.Combine(path1: root, path2: "Models");
        var first = Path.Combine(path1: models, path2: "First.cs");
        var shape = Path.Combine(path1: models, path2: "Shape.cs");
        var source = Path.Combine(path1: models, path2: "Source.cs");
        var addedDirectory = Path.Combine(path1: models, path2: "Added");
        var dirtyPaths = new MetadataDirtyPathIndex(
            paths:
            [
                Path.Combine(path1: root, path2: "ModelsSibling", path3: "Unrelated.cs"),
                first,
                shape,
                source,
                Path.Combine(path1: models, path2: "Last.cs"),
                addedDirectory,
            ]);
        var sources = new Dictionary<string, int>(comparer: StringComparer.OrdinalIgnoreCase) { [source.ToUpperInvariant()] = 1 };
        var shapes = new Dictionary<string, int>(comparer: StringComparer.OrdinalIgnoreCase) { [shape] = 1 };
        var directories = new Dictionary<string, int>(comparer: StringComparer.OrdinalIgnoreCase) { [models] = 1, [addedDirectory] = 1 };

        var relevant = dirtyPaths.FindRelevantPaths(sourceFiles: sources, shapeFiles: shapes, directories: directories).ToArray();

        relevant.Should().Equal(first, shape, source, addedDirectory);
    }

    [Fact]
    public void DirectoryIntersectionDoesNotRescanEveryDirtyPath()
    {
        const int DirtyCount = 4096;
        var root = Path.Combine(path1: Path.GetTempPath(), path2: "Typewriter.DirtyPathIndex.Tests");
        var paths = Enumerable.Range(start: 0, count: DirtyCount)
            .Select(selector: index => Path.Combine(path1: root, path2: index.ToString(provider: CultureInfo.InvariantCulture), path3: "Model.cs"))
            .ToArray();
        var dirtyPaths = new MetadataDirtyPathIndex(paths: paths);
        var inputs = new CountingDictionary<int>(values: new Dictionary<string, int>(comparer: StringComparer.OrdinalIgnoreCase) { [root] = 1 });
        var empty = new Dictionary<string, int>(comparer: StringComparer.OrdinalIgnoreCase);

        var relevant = dirtyPaths.FindRelevantPaths(sourceFiles: empty, shapeFiles: empty, directories: inputs).ToArray();

        relevant.Should().Equal(paths[0]);
        inputs.Operations.Should().Be(expected: 1);
    }

    [Fact]
    public void EmptyDirtyIndexHasNoRelevantPaths()
    {
        var dirtyPaths = new MetadataDirtyPathIndex(paths: []);
        var inputs = new Dictionary<string, int>(comparer: StringComparer.OrdinalIgnoreCase) { [Path.Combine(path1: Path.GetTempPath(), path2: "Model.cs")] = 1 };

        dirtyPaths.FindRelevantPaths(sourceFiles: inputs, shapeFiles: inputs, directories: inputs).Should().BeEmpty();
        dirtyPaths.Count.Should().Be(expected: 0);
    }

    private sealed class CountingDictionary<T>(IReadOnlyDictionary<string, T> values) : IReadOnlyDictionary<string, T>
    {
        public int Operations { get; private set; }

        public int Count => values.Count;

        public IEnumerable<string> Keys => values.Keys;

        public IEnumerable<T> Values => values.Values;

        public T this[string key] => values[key];

        public bool ContainsKey(string key)
        {
            Operations++;
            return values.ContainsKey(key: key);
        }

        public bool TryGetValue(string key, [MaybeNullWhen(false)] out T value)
        {
            Operations++;
            return values.TryGetValue(key: key, value: out value);
        }

        public IEnumerator<KeyValuePair<string, T>> GetEnumerator()
        {
            foreach (var value in values)
            {
                Operations++;
                yield return value;
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
