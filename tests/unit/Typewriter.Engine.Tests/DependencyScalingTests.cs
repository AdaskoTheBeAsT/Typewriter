using System.Globalization;
using AwesomeAssertions;
using Typewriter.Abstractions;
using Xunit;

namespace Typewriter.Engine.Tests;

public sealed class DependencyScalingTests
{
    [Fact]
    public void ReverseChainClosureAllocatesProportionallyToTheNumberOfFiles()
    {
        const int count = 2000;
        var types = Enumerable.Range(start: 0, count: count).Select(selector: CreateType).ToArray();
        var files = types.Select(selector: (type, index) => new SourceFileMetadata(Path: string.Create(provider: CultureInfo.InvariantCulture, $"Type{index}.cs"), Types: [type])).ToArray();
        var index = ProjectMetadataIndex.Create(metadata: new ProjectMetadata(ProjectPath: "Probe.csproj", SourceFiles: files, Types: types, Diagnostics: []));
        index.GetAffectedSourceFiles(changedSourcePaths: []).Should().BeEmpty();
        var before = GC.GetAllocatedBytesForCurrentThread();

        var affected = index.GetAffectedSourceFiles(changedSourcePaths: [files[^1].Path]);

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        affected.Should().BeEquivalentTo(files.Select(selector: file => file.Path));
        allocated.Should().BeLessThan(expected: count * 4096L, because: "dependency traversal must not rescan the full graph for each newly affected file");
    }

    [Fact]
    public void ClosureHandlesCyclesDuplicateChangesAndUntrackedPaths()
    {
        var first = CreateType(index: 0);
        var second = CreateType(index: 1) with { BaseTypes = [CreateReference(index: 0)] };
        var unrelated = CreateType(index: 3) with { BaseTypes = [] };
        SourceFileMetadata[] files =
        [
            new(Path: "First.cs", Types: [first]),
            new(Path: "Second.cs", Types: [second]),
            new(Path: "Unrelated.cs", Types: [unrelated]),
        ];
        var index = ProjectMetadataIndex.Create(metadata: new ProjectMetadata(ProjectPath: "Probe.csproj", SourceFiles: files, Types: [first, second, unrelated], Diagnostics: []));

        var affected = index.GetAffectedSourceFiles(changedSourcePaths: ["FIRST.CS", "First.cs", "Missing.cs"]);

        affected.Select(selector: path => path.ToUpperInvariant()).Should().BeEquivalentTo("FIRST.CS", "SECOND.CS");
    }

    private static TypeMetadata CreateType(int index) =>
        new(
            Name: "Type" + index.ToString(provider: CultureInfo.InvariantCulture),
            FullName: "Probe.Type" + index.ToString(provider: CultureInfo.InvariantCulture),
            Namespace: "Probe",
            Kind: TypeMetadataKind.Class,
            Accessibility: MetadataAccessibility.Public,
            Properties: [],
            Attributes: [],
            BaseTypes: [CreateReference(index: index + 1)],
            EnumValues: [],
            IsNullableAware: true);

    private static TypeMetadataReference CreateReference(int index) =>
        new(
            Name: "Type" + index.ToString(provider: CultureInfo.InvariantCulture),
            FullName: "Probe.Type" + index.ToString(provider: CultureInfo.InvariantCulture),
            Namespace: "Probe",
            IsNullable: false,
            IsCollection: false,
            IsDictionary: false,
            IsEnum: false,
            IsPrimitive: false,
            IsDateLike: false,
            ElementType: null,
            TypeArguments: []);
}
