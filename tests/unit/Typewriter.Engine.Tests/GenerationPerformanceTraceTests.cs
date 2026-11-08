using System.Globalization;
using AwesomeAssertions;
using Typewriter.Abstractions;
using Xunit;

namespace Typewriter.Engine.Tests;

[Collection("Performance environment")]
public sealed class GenerationPerformanceTraceTests
{
    [Fact]
    public void DisabledTraceDoesNotAllocatePerSourceStatistics()
    {
        var trace = new GenerationPerformanceTrace(enabled: false);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 10000; index++)
        {
            _ = trace.Start();
            _ = trace.CreateTemplateStatistics(templatePath: "Unused.tst");
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        allocated.Should().Be(expected: 0);
        trace.MetadataSink.Should().BeNull();
        var diagnostics = new List<GenerationDiagnostic>();
        trace.AddDiagnostics(diagnostics: diagnostics, file: "Unused.tst");
        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task OverlappingObserversKeepTheirMeasurementsSeparate()
    {
        var first = new GenerationPerformanceTrace(enabled: true);
        var second = new GenerationPerformanceTrace(enabled: true);
        await Task.WhenAll(
            Task.Run(action: () => first.MetadataSink!(new MetadataStageTiming(ProjectPath: "First.csproj", Stage: "Parsing", Elapsed: TimeSpan.FromMilliseconds(value: 1)))),
            Task.Run(action: () => second.MetadataSink!(new MetadataStageTiming(ProjectPath: "Second.csproj", Stage: "MSBuild", Elapsed: TimeSpan.FromMilliseconds(value: 2)))));
        var firstDiagnostics = new List<GenerationDiagnostic>();
        var secondDiagnostics = new List<GenerationDiagnostic>();
        first.AddDiagnostics(diagnostics: firstDiagnostics, file: "First.csproj");
        second.AddDiagnostics(diagnostics: secondDiagnostics, file: "Second.csproj");
        firstDiagnostics.Should().Contain(predicate: diagnostic => diagnostic.Message.Contains("First.csproj", StringComparison.Ordinal));
        firstDiagnostics.Should().NotContain(predicate: diagnostic => diagnostic.Message.Contains("Second.csproj", StringComparison.Ordinal));
        secondDiagnostics.Should().Contain(predicate: diagnostic => diagnostic.Message.Contains("Second.csproj", StringComparison.Ordinal));
        secondDiagnostics.Should().NotContain(predicate: diagnostic => diagnostic.Message.Contains("First.csproj", StringComparison.Ordinal));
    }

    [Fact]
    public async Task LargeGenerationUsesBoundedSummariesAndPreservesOutputBytes()
    {
        var previous = Environment.GetEnvironmentVariable(variable: "TYPEWRITER_TRACE_PERF");
        var directory = Path.Combine(path1: Path.GetTempPath(), path2: "Typewriter.Trace-" + Guid.NewGuid().ToString(format: "N"));
        Directory.CreateDirectory(path: directory);
        try
        {
            var projectPath = Path.Combine(path1: directory, path2: "Project.csproj");
            await File.WriteAllTextAsync(path: projectPath, contents: "<Project />");
            var types = Enumerable.Range(start: 0, count: 9275).Select(
                selector: index => new TypeMetadata(
                    Name: "Type" + index.ToString(provider: CultureInfo.InvariantCulture),
                    FullName: "Type" + index.ToString(provider: CultureInfo.InvariantCulture),
                    Namespace: string.Empty,
                    Kind: TypeMetadataKind.Class,
                    Accessibility: MetadataAccessibility.Public,
                    Properties: [],
                    Attributes: [],
                    BaseTypes: [],
                    EnumValues: [],
                    IsNullableAware: true)).ToArray();
            var sources = types.Select(selector: type => new SourceFileMetadata(Path: Path.Combine(path1: directory, path2: type.Name + ".cs"), Types: [type])).ToArray();
            var metadata = new ProjectMetadata(ProjectPath: projectPath, SourceFiles: sources, Types: types, Diagnostics: []);
            var provider = new Provider(metadata: metadata);
            var generator = new TypewriterGenerator(templateDiscovery: new Discovery(directory: directory), metadataProvider: provider, fileWriter: new Writer());
            var request = new GenerationRequest(WorkspacePath: directory, ProjectPath: projectPath, TemplatePath: null, Mode: GenerationMode.Generate, Configuration: TypewriterConfiguration.Default);
            Environment.SetEnvironmentVariable(variable: "TYPEWRITER_TRACE_PERF", value: null);
            var plain = await generator.GenerateAsync(request: request, cancellationToken: CancellationToken.None);
            provider.SawSink.Should().BeFalse();
            Environment.SetEnvironmentVariable(variable: "TYPEWRITER_TRACE_PERF", value: "yes");
            var traced = await generator.GenerateAsync(request: request, cancellationToken: CancellationToken.None);

            plain.Success.Should().BeTrue();
            traced.Success.Should().BeTrue();
            plain.Diagnostics.Should().BeEmpty();
            traced.GeneratedFiles.Should().Equal(expected: plain.GeneratedFiles);
            traced.GeneratedFiles.Should().HaveCount(expected: 18550);
            traced.Diagnostics.Should().OnlyContain(predicate: diagnostic => diagnostic.Code == "TWPERF");
            traced.Diagnostics.Count.Should().BeLessThan(expected: 30);
            var summaries = traced.Diagnostics.Where(predicate: diagnostic => diagnostic.Message.StartsWith("Template run", StringComparison.Ordinal)).ToArray();
            summaries.Should().HaveCount(expected: 2);
            summaries.Should().OnlyContain(predicate: diagnostic => diagnostic.Message.Contains("render-calls=9275, outputs=9275", StringComparison.Ordinal));
            summaries.Should().OnlyContain(predicate: diagnostic => diagnostic.Message.Split(" ms", StringSplitOptions.None).Length <= 12);
            provider.SawSink.Should().BeTrue();
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable: "TYPEWRITER_TRACE_PERF", value: previous);
            Directory.Delete(path: directory, recursive: true);
        }
    }

    [Fact]
    public void TemplateReferenceResolutionReusesImmutableImages()
    {
        var diagnostics = new List<GenerationDiagnostic>();
        var resolver = new TemplateReferenceResolver(templatePath: Path.Combine(path1: Path.GetTempPath(), path2: "References.tst"), diagnostics: diagnostics);
        var first = resolver.CreateReferences(referenceDirectives: []);
        var second = resolver.CreateReferences(referenceDirectives: []);
        first.Should().NotBeEmpty();
        first.Zip(second: second).Should().OnlyContain(predicate: pair => ReferenceEquals(pair.First, pair.Second));
        diagnostics.Should().BeEmpty();
    }

    private sealed class Discovery(string directory) : ITemplateDiscovery
    {
        public Task<IReadOnlyList<TemplateFile>> FindTemplatesAsync(WorkspaceContext workspace, GenerationRequest request, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TemplateFile>>(
                result:
                [
                    new TemplateFile(Path: Path.Combine(path1: directory, path2: "One", path3: "Models.tst"), Content: "$Classes[$Name;]"),
                    new TemplateFile(Path: Path.Combine(path1: directory, path2: "Two", path3: "Models.tst"), Content: "$Classes[$Name;]"),
                ]);
    }

    private sealed class Provider(ProjectMetadata metadata) : IProjectMetadataProvider
    {
        public bool SawSink { get; private set; }

        public Task<ProjectMetadata> GetMetadataAsync(ProjectContext project, CancellationToken cancellationToken)
        {
            SawSink = project.PerformanceSink is not null;
            project.PerformanceSink?.Invoke(obj: new MetadataStageTiming(ProjectPath: project.ProjectPath, Stage: "Cache validation (Hit)", Elapsed: TimeSpan.Zero));
            return Task.FromResult(result: metadata);
        }
    }

    private sealed class Writer : IGeneratedFileWriter
    {
        public Task<GeneratedFile> WriteAsync(GeneratedFile file, GenerationRequest request, CancellationToken cancellationToken) => Task.FromResult(result: file);
    }
}
