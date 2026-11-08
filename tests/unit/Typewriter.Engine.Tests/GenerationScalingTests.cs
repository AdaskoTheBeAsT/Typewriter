using System.Collections;
using System.Globalization;
using AwesomeAssertions;
using Typewriter.Abstractions;
using Xunit;

namespace Typewriter.Engine.Tests;

public sealed class GenerationScalingTests
{
    [Fact]
    public async Task GenerateAsyncDoesNotRescanTheProjectForEverySourceFile()
    {
        var directory = Path.Combine(path1: Path.GetTempPath(), path2: "TypewriterScaling-" + Guid.NewGuid().ToString(format: "N"));
        Directory.CreateDirectory(path: directory);
        try
        {
            var projectPath = Path.Combine(path1: directory, path2: "Sample.csproj");
            await File.WriteAllTextAsync(path: projectPath, contents: "<Project />");
            var types = Enumerable.Range(start: 0, count: 128).Select(selector: CreateType).ToArray();
            var sources = new CountingList<SourceFileMetadata>(
                items: types.Select(selector: type => new SourceFileMetadata(Path: Path.Combine(path1: directory, path2: type.Name + ".cs"), Types: [type])).ToArray());
            var metadata = new ProjectMetadata(ProjectPath: projectPath, SourceFiles: sources, Types: types, Diagnostics: []);
            var discovery = new Discovery(
                template: new TemplateFile(
                    Path: Path.Combine(path1: directory, path2: "Models.tst"),
                    Content: """
                        ${
                            bool Select(Class c) => c.Name.EndsWith("0", StringComparison.Ordinal);
                        }
                        $Classes(Select)[$Name;]
                        """));
            var generator = new TypewriterGenerator(templateDiscovery: discovery, metadataProvider: new Provider(metadata: metadata), fileWriter: new Writer());

            var result = await generator.GenerateAsync(
                request: new GenerationRequest(
                    WorkspacePath: directory,
                    ProjectPath: projectPath,
                    TemplatePath: null,
                    Mode: GenerationMode.Generate,
                    Configuration: TypewriterConfiguration.Default),
                cancellationToken: CancellationToken.None);

            result.Success.Should().BeTrue(because: string.Join(separator: Environment.NewLine, values: result.Diagnostics.Select(selector: diagnostic => diagnostic.Message)));
            result.Diagnostics.Should().BeEmpty();
            result.GeneratedFiles.Should().HaveCount(13);
            sources.ReadCount.Should().BeLessThan(expected: sources.Count * 20, because: "per-file rendering must reuse project-wide source indexes");
        }
        finally
        {
            Directory.Delete(path: directory, recursive: true);
        }
    }

    private static TypeMetadata CreateType(int index) =>
        new(
            Name: "Type" + index.ToString(provider: CultureInfo.InvariantCulture),
            FullName: "Sample.Type" + index.ToString(provider: CultureInfo.InvariantCulture),
            Namespace: "Sample",
            Kind: TypeMetadataKind.Class,
            Accessibility: MetadataAccessibility.Public,
            Properties: [],
            Attributes: [],
            BaseTypes: [],
            EnumValues: [],
            IsNullableAware: true);

    private sealed class CountingList<T>(IReadOnlyList<T> items) : IReadOnlyList<T>
    {
        public int Count => items.Count;

        public int ReadCount { get; private set; }

        public T this[int index]
        {
            get
            {
                ReadCount++;
                return items[index];
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            foreach (var item in items)
            {
                ReadCount++;
                yield return item;
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class Discovery(TemplateFile template) : ITemplateDiscovery
    {
        public Task<IReadOnlyList<TemplateFile>> FindTemplatesAsync(
            WorkspaceContext workspace,
            GenerationRequest request,
            CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<TemplateFile>>(result: [template]);
    }

    private sealed class Provider(ProjectMetadata metadata) : IProjectMetadataProvider
    {
        public Task<ProjectMetadata> GetMetadataAsync(
            ProjectContext project,
            CancellationToken cancellationToken) => Task.FromResult(result: metadata);
    }

    private sealed class Writer : IGeneratedFileWriter
    {
        public Task<GeneratedFile> WriteAsync(
            GeneratedFile file,
            GenerationRequest request,
            CancellationToken cancellationToken) => Task.FromResult(result: file);
    }
}
