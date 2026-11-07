using System.Globalization;
using System.Text;
using AwesomeAssertions;
using Microsoft.CodeAnalysis;
using Typewriter.Abstractions;
using Typewriter.Buildalyzer;
using Xunit;

namespace Typewriter.Roslyn.Tests;

[Collection("Roslyn metadata cache")]
public sealed class MetadataScalingTests
{
    [Theory]
    [InlineData(32, 1)]
    [InlineData(1, 128)]
    public async Task GetMetadataSharesCompletedTypesAcrossDeepAndWideNesting(int depth, int width)
    {
        await using var workspace = new MetadataWorkspace();
        var source = new StringBuilder(value: "namespace Scaling; public partial class Root<T> { public class Generic<TValue> { } public class Generic<TValue, TOther> { } ");
        for (var level = 0; level < depth; level++)
        {
            for (var sibling = 0; sibling < width; sibling++)
            {
                source.Append(value: "public class Leaf").Append(value: sibling.ToString(provider: CultureInfo.InvariantCulture)).Append(value: " { public int Value { get; set; } = 42; } ");
            }

            source.Append(value: "public class Level").Append(value: level.ToString(provider: CultureInfo.InvariantCulture)).Append(value: " { ");
        }

        source.Append(value: '}', repeatCount: depth + 1);
        var projectPath = await workspace.AddProjectAsync(name: "Nested", source: source.ToString());
        await workspace.AddSourceAsync(
            projectPath: projectPath,
            name: "Partial.cs",
            source: "namespace Scaling; public partial class Root<T> { public class Other { } }");

        var metadata = await workspace.GetMetadataAsync(projectPath: projectPath);

        metadata.Diagnostics.Should().BeEmpty();
        metadata.Types.Should().HaveCount(expected: 4 + (depth * (width + 1)));
        var flatTypes = metadata.Types.ToDictionary(keySelector: type => (type.FullName, type.TypeParameters.Count));
        foreach (var type in metadata.Types)
        {
            foreach (var nested in type.NestedClasses)
            {
                nested.Should().BeSameAs(expected: flatTypes[key: (nested.FullName, nested.TypeParameters.Count)]);
            }
        }

        var root = metadata.Types.Single(predicate: type => type.FullName == "Scaling.Root");
        root.FileLocations.Should().HaveCount(expected: 2);
        root.NestedClasses.Where(predicate: type => type.Name == "Generic").Select(selector: type => type.TypeParameters.Count).Should().Equal(1, 2);
        foreach (var sourceFile in metadata.SourceFiles)
        {
            sourceFile.Types.Single(predicate: type => type.FullName == "Scaling.Root").Should().BeSameAs(expected: root);
        }
    }

    [Theory]
    [InlineData(64)]
    [InlineData(512)]
    public async Task GetMetadataSharesEnumValuesAcrossNullableAndCollectionReferences(int propertyCount)
    {
        const int ValueCount = 128;
        await using var workspace = new MetadataWorkspace();
        var source = new StringBuilder(value: "namespace Scaling; public enum Status {\n");
        for (var index = 0; index < ValueCount; index++)
        {
            var number = index.ToString(provider: CultureInfo.InvariantCulture);
            source.Append(value: "/// <summary>Value ").Append(value: number).Append(value: ".</summary>\nValue").Append(value: number).Append(value: " = ").Append(value: number).AppendLine(value: ",");
        }

        source.Append(value: "} public class Model { ");
        for (var index = 0; index < propertyCount; index++)
        {
            var typeName = (index % 3) switch
            {
                0 => "Status",
                1 => "Status?",
                _ => "System.Collections.Generic.List<Status>",
            };
            source.Append(value: "public ").Append(value: typeName).Append(value: " Property").Append(value: index.ToString(provider: CultureInfo.InvariantCulture)).Append(value: " { get; set; } ");
        }

        source.Append(value: '}');
        var projectPath = await workspace.AddProjectAsync(name: "Enums", source: source.ToString());

        var metadata = await workspace.GetMetadataAsync(projectPath: projectPath);

        metadata.Diagnostics.Should().BeEmpty();
        var values = metadata.Types.Single(predicate: type => type.Name == "Status").EnumValues;
        values.Should().HaveCount(expected: ValueCount);
        values[0].Documentation.Should().Be("Value 0.");
        values[0].Location.Should().NotBeNull();
        var properties = metadata.Types.Single(predicate: type => type.Name == "Model").Properties;
        properties.Should().HaveCount(expected: propertyCount);
        foreach (var property in properties)
        {
            var reference = property.Type.ElementType ?? property.Type;
            reference.EnumValues.Should().BeSameAs(expected: values);
        }
    }

    [Fact]
    public async Task GetMetadataHandlesEnumAttributesThatReferBackToTheSameEnum()
    {
        await using var workspace = new MetadataWorkspace();
        var projectPath = await workspace.AddProjectAsync(
            name: "RecursiveEnum",
            source: """
                    namespace Scaling;
                    public sealed class ValueAttribute(Status value) : System.Attribute { }
                    public enum Status
                    {
                        [Value(Status.One)]
                        One = 1,
                        [Value(Status.One)]
                        Two = 2,
                    }
                    """);

        var metadata = await workspace.GetMetadataAsync(projectPath: projectPath);

        metadata.Diagnostics.Should().BeEmpty();
        var values = metadata.Types.Single(predicate: type => type.Name == "Status").EnumValues;
        values.Should().HaveCount(expected: 2);
        foreach (var value in values)
        {
            var argument = value.Attributes.Should().ContainSingle().Which.Arguments.Should().ContainSingle().Which;
            argument.Type.Should().NotBeNull();
            argument.Type.EnumValues.Should().BeSameAs(expected: values);
        }
    }

    [Fact]
    public async Task GetMetadataSharesPartialLocationsAndPathIndependentContainingTypeArguments()
    {
        await using var workspace = new MetadataWorkspace();
        var projectPath = await workspace.AddProjectAsync(
            name: "References",
            source: """
                    namespace Scaling;
                    public partial class Partial<T> { }
                    public class Pair<TLeft, TRight> { }
                    public class Outer<T> { public class Nested { } }
                    public class Model
                    {
                        public Partial<int> First { get; set; }
                        public Partial<int>? Second { get; set; }
                        public Partial<string> Third { get; set; }
                        public Outer<Pair<Partial<int>, Partial<int>>>.Nested Nested { get; set; }
                    }
                    """);
        for (var index = 0; index < 16; index++)
        {
            await workspace.AddSourceAsync(
                projectPath: projectPath,
                name: "Partial" + index.ToString(provider: CultureInfo.InvariantCulture) + ".cs",
                source: "namespace Scaling; public partial class Partial<T> { }");
        }

        var metadata = await workspace.GetMetadataAsync(projectPath: projectPath);

        metadata.Diagnostics.Should().BeEmpty();
        var locations = metadata.Types.Single(predicate: type => type.Name == "Partial").FileLocations;
        locations.Should().HaveCount(expected: 17);
        var properties = metadata.Types.Single(predicate: type => type.Name == "Model").Properties;
        foreach (var property in properties.Where(predicate: property => property.Name != "Nested"))
        {
            property.Type.FileLocations.Should().BeSameAs(expected: locations);
        }

        var containing = properties.Single(predicate: property => property.Name == "Nested").Type.ContainingType;
        containing.Should().NotBeNull();
        var pair = containing.TypeArguments.Should().ContainSingle().Which;
        pair.TypeArguments.Should().HaveCount(expected: 2);
        pair.TypeArguments[0].Should().BeSameAs(expected: pair.TypeArguments[1]);
        pair.TypeArguments[0].FileLocations.Should().BeSameAs(expected: locations);
    }

    [Fact]
    public async Task GetMetadataPreservesTheFirstConstructedEnumerableAndDictionaryClassification()
    {
        await using var workspace = new MetadataWorkspace();
        var projectPath = await workspace.AddProjectAsync(
            name: "Collections",
            source: """
                    namespace Scaling;
                    public interface Dual : System.Collections.Generic.IEnumerable<int>, System.Collections.Generic.IEnumerable<string> { }
                    public class Model
                    {
                        public Dual First { get; set; }
                        public Dual? Second { get; set; }
                        public System.Collections.Generic.Dictionary<string, int> Dictionary { get; set; }
                        public System.Collections.Generic.IReadOnlyDictionary<string, int> ReadOnlyDictionary { get; set; }
                        public System.Collections.Generic.IEnumerable<int> Enumerable { get; set; }
                    }
                    """);

        var metadata = await workspace.GetMetadataAsync(projectPath: projectPath);
        var compilation = await workspace.GetCompilationAsync(projectPath: projectPath);

        metadata.Diagnostics.Should().BeEmpty();
        compilation.Should().NotBeNull();
        var symbol = compilation.GetTypeByMetadataName(fullyQualifiedMetadataName: "Scaling.Dual");
        symbol.Should().NotBeNull();
        var enumerable = symbol.AllInterfaces.First(predicate: candidate => candidate.OriginalDefinition.MetadataName == "IEnumerable`1");
        var expectedElementName = enumerable.TypeArguments[0].ToDisplayString(
            format: new SymbolDisplayFormat(typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces));
        var properties = metadata.Types.Single(predicate: type => type.Name == "Model").Properties;
        foreach (var property in properties.Where(predicate: property => property.Name is "First" or "Second"))
        {
            property.Type.ElementType.Should().NotBeNull();
            property.Type.ElementType.FullName.Should().Be(expected: expectedElementName);
            property.Type.IsDictionary.Should().BeFalse();
        }

        properties.Single(predicate: property => property.Name == "Dictionary").Type.IsDictionary.Should().BeTrue();
        properties.Single(predicate: property => property.Name == "ReadOnlyDictionary").Type.IsDictionary.Should().BeTrue();
        var enumerableReference = properties.Single(predicate: property => property.Name == "Enumerable").Type;
        enumerableReference.IsDictionary.Should().BeFalse();
        enumerableReference.ElementType.Should().NotBeNull();
        enumerableReference.ElementType.FullName.Should().Be(expected: "System.Int32");
    }

    [Fact]
    public async Task DirtyNewSourceInvalidatesTheDirectoryShapeAndReloadsTheProject()
    {
        await using var workspace = new MetadataWorkspace();
        MetadataCacheInvalidation.EnableTracking();
        var projectPath = await workspace.AddProjectAsync(name: "AddedSource", source: "namespace Scaling; public class Original { }");
        var first = await workspace.GetMetadataAsync(projectPath: projectPath);
        first.Types.Should().ContainSingle();
        var directory = Path.GetDirectoryName(path: projectPath)!;
        var oldStamp = Directory.GetLastWriteTimeUtc(path: directory);

        await workspace.AddSourceAsync(projectPath: projectPath, name: "Added.cs", source: "namespace Scaling; public class Added { }");
        Directory.SetLastWriteTimeUtc(path: directory, lastWriteTimeUtc: oldStamp.AddSeconds(value: 2));
        MetadataCacheInvalidation.MarkDirty(fullPath: Path.Combine(path1: directory, path2: "Added.cs"));
        var rebuilt = await workspace.GetMetadataAsync(projectPath: projectPath);

        rebuilt.Diagnostics.Should().BeEmpty();
        rebuilt.Types.Select(selector: type => type.Name).Should().Equal("Added", "Original");
        workspace.LoadCounts.Values.Should().ContainSingle().Which.Should().Be(expected: 2);
    }

    [Fact]
    public async Task GetMetadataRebuildsDiamondDependentsFromLocalManifestsWithoutReloadingProjects()
    {
        await using var workspace = new MetadataWorkspace();
        MetadataCacheInvalidation.EnableTracking();
        var leaf = await workspace.AddProjectAsync(name: "Leaf", source: "namespace Leaf; public class Model { public int Id { get; set; } }");
        var left = await workspace.AddProjectAsync(name: "Left", source: "namespace Left; public class Model { public Leaf.Model Item { get; set; } }", references: [leaf]);
        var right = await workspace.AddProjectAsync(name: "Right", source: "namespace Right; public class Model { public Leaf.Model Item { get; set; } }", references: [leaf]);
        var root = await workspace.AddProjectAsync(name: "Root", source: "namespace Root; public class Model { public Leaf.Model Item { get; set; } }", references: [left, right]);

        var first = await workspace.GetMetadataAsync(projectPath: root);
        first.Diagnostics.Should().BeEmpty();
        first.Types.Should().HaveCount(expected: 4);
        var cached = await workspace.GetMetadataAsync(projectPath: root);
        cached.Should().BeSameAs(expected: first);

        var leafSource = workspace.GetSourcePath(projectPath: leaf);
        await File.WriteAllTextAsync(path: leafSource, contents: "namespace Leaf; public class Model { public int Id { get; set; } public string Email { get; set; } }");
        MetadataCacheInvalidation.MarkDirty(fullPath: leafSource);

        var rebuilt = await workspace.GetMetadataAsync(projectPath: root);
        rebuilt.Diagnostics.Should().BeEmpty();
        rebuilt.Types.Should().HaveCount(expected: 4);
        rebuilt.Types.Single(predicate: type => type.FullName == "Leaf.Model").Properties.Should().Contain(predicate: property => property.Name == "Email");
        workspace.LoadCounts.Values.Should().OnlyContain(predicate: count => count == 1);
        workspace.LoadCounts.Should().HaveCount(expected: 4);

        await File.WriteAllTextAsync(path: leaf, contents: "<Project><!-- Changed project inputs --></Project>");
        MetadataCacheInvalidation.MarkDirty(fullPath: leaf);
        var reevaluated = await workspace.GetMetadataAsync(projectPath: root);
        reevaluated.Diagnostics.Should().BeEmpty();
        workspace.LoadCounts.Values.Should().OnlyContain(predicate: count => count == 2);
    }

    private sealed class MetadataWorkspace : IProjectWorkspaceLoader, IAsyncDisposable
    {
        private readonly Dictionary<string, ProjectLoadResult> _projects = new(comparer: StringComparer.OrdinalIgnoreCase);
        private readonly CSharpProjectMetadataProvider _provider;

        public MetadataWorkspace()
        {
            CSharpProjectMetadataProvider.ClearCachesForTests();
            MetadataCacheInvalidation.Reset();
            DirectoryPath = Path.Combine(path1: Path.GetTempPath(), path2: "Typewriter.MetadataScaling.Tests", path3: Guid.NewGuid().ToString(format: "N"));
            Directory.CreateDirectory(path: DirectoryPath);
            _provider = new CSharpProjectMetadataProvider(projectLoader: this);
        }

        public string DirectoryPath { get; }

        public Dictionary<string, int> LoadCounts { get; } = new(comparer: StringComparer.OrdinalIgnoreCase);

        public async Task<string> AddProjectAsync(
            string name,
            string source,
            IReadOnlyList<string>? references = null)
        {
            var directory = Path.Combine(path1: DirectoryPath, path2: name);
            Directory.CreateDirectory(path: directory);
            var projectPath = Path.Combine(path1: directory, path2: name + ".csproj");
            var sourcePath = Path.Combine(path1: directory, path2: name + ".cs");
            await File.WriteAllTextAsync(path: projectPath, contents: "<Project />");
            await File.WriteAllTextAsync(path: sourcePath, contents: source);
            _projects.Add(
                key: projectPath,
                value: new ProjectLoadResult(
                    ProjectPath: projectPath,
                    ProjectDirectory: directory,
                    TargetFramework: "net10.0",
                    NullableEnabled: true,
                    ImplicitUsingsEnabled: false,
                    SourceFiles: [sourcePath],
                    PreprocessorSymbols: [],
                    GlobalUsings: [],
                    ProjectReferences: references ?? [],
                    ReferencePaths: [],
                    AnalyzerReferences: [],
                    AdditionalFiles: [],
                    AnalyzerConfigFiles: [],
                    Diagnostics: []));
            return projectPath;
        }

        public async Task AddSourceAsync(string projectPath, string name, string source)
        {
            var project = _projects[key: projectPath];
            var sourcePath = Path.Combine(path1: project.ProjectDirectory, path2: name);
            await File.WriteAllTextAsync(path: sourcePath, contents: source);
            _projects[key: projectPath] = project with { SourceFiles = project.SourceFiles.Append(element: sourcePath).ToArray() };
        }

        public string GetSourcePath(string projectPath) => _projects[key: projectPath].SourceFiles[0];

        public Task<ProjectMetadata> GetMetadataAsync(string projectPath) =>
            _provider.GetMetadataAsync(
                project: new ProjectContext(ProjectPath: projectPath, WorkspacePath: DirectoryPath, TargetFramework: "net10.0"),
                cancellationToken: CancellationToken.None);

        public Task<Compilation?> GetCompilationAsync(string projectPath) =>
            _provider.GetCompilationAsync(
                project: new ProjectContext(ProjectPath: projectPath, WorkspacePath: DirectoryPath, TargetFramework: "net10.0"),
                cancellationToken: CancellationToken.None);

        public Task<ProjectLoadResult> LoadAsync(ProjectContext project, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var count = LoadCounts.GetValueOrDefault(key: project.ProjectPath);
            LoadCounts[key: project.ProjectPath] = count + 1;
            return Task.FromResult(result: _projects[key: project.ProjectPath]);
        }

        public ValueTask DisposeAsync()
        {
            MetadataCacheInvalidation.Reset();
            CSharpProjectMetadataProvider.ClearCachesForTests();
            Directory.Delete(path: DirectoryPath, recursive: true);
            return ValueTask.CompletedTask;
        }
    }
}
