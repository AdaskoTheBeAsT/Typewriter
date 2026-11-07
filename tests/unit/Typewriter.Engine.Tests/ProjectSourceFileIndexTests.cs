using AwesomeAssertions;
using Typewriter.Abstractions;
using Xunit;

namespace Typewriter.Engine.Tests;

public sealed class ProjectSourceFileIndexTests
{
    [Fact]
    public void DuplicatePathsUseLastValueAtFirstOccurrencePosition()
    {
        var type = AdapterTestData.CreateType();
        var first = new SourceFileMetadata(Path: "First.cs", Types: []);
        var second = new SourceFileMetadata(Path: "Second.cs", Types: [type]);
        var replacement = new SourceFileMetadata(Path: "FIRST.cs", Types: [type]);
        var index = new ProjectSourceFileIndex(sourceFiles: [first, second, replacement]);

        index.Find(type: type, filePath: null).Should().BeSameAs(replacement);
        index.Find(type: type, filePath: "first.cs").Should().BeSameAs(replacement);
    }

    [Fact]
    public void OverridesRemoveShadowedDeclarationsWithoutChangingFallbackOrder()
    {
        var type = AdapterTestData.CreateType();
        var first = new SourceFileMetadata(Path: "First.cs", Types: [type]);
        var second = new SourceFileMetadata(Path: "Second.cs", Types: [type]);
        var index = new ProjectSourceFileIndex(sourceFiles: [first, second]);
        var removed = index.WithOverrides(sourceFiles: [first with { Types = [] }]);
        var replaced = index.WithOverrides(sourceFiles: [first with { Types = [type with { Name = "Replacement" }] }]);

        removed.Find(type: type, filePath: null).Should().BeSameAs(second);
        replaced.Find(type: type, filePath: null)!.Path.Should().Be("First.cs");
        index.Find(type: type, filePath: null).Should().BeSameAs(first);
    }

    [Fact]
    public void AddedOverlayDeclarationUsesOriginalPathPosition()
    {
        var type = AdapterTestData.CreateType();
        var first = new SourceFileMetadata(Path: "First.cs", Types: []);
        var second = new SourceFileMetadata(Path: "Second.cs", Types: [type]);
        var replacement = first with { Types = [type] };
        var index = new ProjectSourceFileIndex(sourceFiles: [first, second])
            .WithOverrides(sourceFiles: [replacement]);

        index.Find(type: type, filePath: null).Should().BeSameAs(replacement);
    }

    [Fact]
    public void PartialPreferenceUsesAssemblyKindAndArityIdentity()
    {
        var type = AdapterTestData.CreateType() with { AssemblyName = "Sample" };
        var primary = new SourceFileMetadata(Path: "Primary.cs", Types: [type]);
        var current = new SourceFileMetadata(Path: "Current.cs", Types: [type with { Location = null }]);
        var index = new ProjectSourceFileIndex(sourceFiles: [primary, current]);

        index.Find(type: type, filePath: primary.Path, preferredSource: current).Should().BeSameAs(current);
        var unrelatedTypes = new[]
        {
            type with { AssemblyName = "Other" },
            type with { Kind = TypeMetadataKind.Struct },
            type with { TypeParameters = [new TypeParameterMetadata(Name: "T")] },
        };
        foreach (var different in unrelatedTypes)
        {
            var unrelated = current with { Types = [different] };
            index.Find(type: type, filePath: primary.Path, preferredSource: unrelated).Should().BeSameAs(primary);
        }
    }

    [Fact]
    public void MissingLocationsUseSharedDeclarationIndexOnce()
    {
        var type = AdapterTestData.CreateType();
        var declarations = new AdapterCountingList<TypeMetadata>(items: [type]);
        var source = new SourceFileMetadata(Path: "Models.cs", Types: declarations);
        var sources = new AdapterCountingList<SourceFileMetadata>(items: [source]);
        var index = new ProjectSourceFileIndex(sourceFiles: sources);

        for (var iteration = 0; iteration < 100; iteration++)
        {
            index.Find(type: type, filePath: null).Should().BeSameAs(source);
            index.Find(type: type, filePath: "Other.cs", preferredSource: source).Should().BeSameAs(source);
        }

        sources.Enumerations.Should().Be(1);
        declarations.Enumerations.Should().Be(1);
    }

    [Fact]
    public void StandaloneDelegatesResolveWithoutTypeDeclarations()
    {
        var callback = new DelegateMetadata(
            Name: "Callback",
            FullName: "Sample.Callback",
            Accessibility: MetadataAccessibility.Public,
            ReturnType: AdapterTestData.CreateReference(),
            IsGeneric: false,
            Parameters: [],
            Attributes: [],
            ParentTypeFullName: string.Empty)
        {
            AssemblyName = "Sample",
        };
        var other = new SourceFileMetadata(Path: "Other.cs", Types: [])
        {
            Delegates = [callback with { AssemblyName = "Other" }],
        };
        var source = new SourceFileMetadata(Path: "Callback.cs", Types: []) { Delegates = [callback] };
        var generic = new SourceFileMetadata(Path: "Generic.cs", Types: [])
        {
            Delegates = [callback with { IsGeneric = true, TypeParameters = [new TypeParameterMetadata(Name: "T")] }],
        };
        var index = new ProjectSourceFileIndex(sourceFiles: [other, generic, source]);

        index.Find(@delegate: callback, filePath: null).Should().BeSameAs(source);
        index.Find(@delegate: callback, filePath: other.Path, preferredSource: source).Should().BeSameAs(source);
    }

    [Fact]
    public void SyntheticFileMembershipIsIndexedOnceAndIncludesPartialLocations()
    {
        var type = AdapterTestData.CreateType() with
        {
            Location = new SourceLocation(Path: "Primary.cs", Line: 1, Column: 1),
            FileLocations = ["Primary.cs", "Partial.cs", "PARTIAL.cs"],
        };
        var types = new AdapterCountingList<TypeMetadata>(items: [type]);
        var metadata = AdapterTestData.CreateProject(types: types);
        var index = new ProjectSourceFileIndex(sourceFiles: []);

        for (var iteration = 0; iteration < 20; iteration++)
        {
            index.GetFallbackSource(metadata: metadata, filePath: "Partial.cs").Types.Should().ContainSingle().Which.Should().BeSameAs(type);
            index.GetFallbackSource(metadata: metadata, filePath: "Primary.cs").Types.Should().ContainSingle();
        }

        types.Enumerations.Should().Be(1);
    }
}
