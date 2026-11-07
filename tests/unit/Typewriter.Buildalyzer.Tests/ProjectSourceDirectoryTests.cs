using System.Globalization;
using AwesomeAssertions;
using Xunit;

namespace Typewriter.Buildalyzer.Tests;

public sealed class ProjectSourceDirectoryTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(4096)]
    public void DirectoryIndexMatchesAncestorsWithoutMatchingSiblingPrefixes(int referenceCount)
    {
        var workspace = Path.Combine(path1: Path.GetTempPath(), path2: "Typewriter.DirectoryIndex.Tests");
        var directories = Enumerable.Range(start: 0, count: referenceCount)
            .Select(selector: index => Path.Combine(path1: workspace, path2: "Project" + index.ToString(provider: CultureInfo.InvariantCulture)))
            .ToHashSet(comparer: StringComparer.OrdinalIgnoreCase);
        var lastDirectory = Path.Combine(path1: workspace, path2: "Project" + (referenceCount - 1).ToString(provider: CultureInfo.InvariantCulture));

        MsBuildProjectLoader.IsInAnyDirectory(path: Path.Combine(path1: lastDirectory, path2: "Models", path3: "Model.cs"), directories: directories).Should().BeTrue();
        MsBuildProjectLoader.IsInAnyDirectory(path: lastDirectory, directories: directories).Should().BeTrue();
        MsBuildProjectLoader.IsInAnyDirectory(path: Path.Combine(path1: lastDirectory + "Suffix", path2: "Model.cs"), directories: directories).Should().BeFalse();
        MsBuildProjectLoader.IsInAnyDirectory(path: Path.Combine(path1: workspace, path2: "Unrelated.cs"), directories: directories).Should().BeFalse();
        MsBuildProjectLoader.IsInAnyDirectory(path: Path.Combine(path1: lastDirectory, path2: "..Named.cs"), directories: directories).Should().BeFalse();
    }

    [Fact]
    public void EmptyDirectoryIndexDoesNotExcludeSources()
    {
        var path = Path.Combine(path1: Path.GetTempPath(), path2: "Model.cs");

        MsBuildProjectLoader.IsInAnyDirectory(path: path, directories: []).Should().BeFalse();
    }
}
