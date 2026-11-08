using System.Globalization;
using System.Runtime.CompilerServices;
using AwesomeAssertions;
using Microsoft.CodeAnalysis;
using Typewriter.Abstractions;
using Typewriter.Buildalyzer;
using Xunit;

namespace Typewriter.Roslyn.Tests;

[Collection("Roslyn metadata cache")]
public sealed class MetadataReuseTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(8192)]
    [InlineData(8193)]
    public async Task SourceOnlyRebuildReusesOwnedTreesAcrossCapacityAndRepeatedEdits(int count)
    {
        using var workspace = new Workspace();
        await workspace.CreateAsync(count: count);
        MetadataCacheInvalidation.EnableTracking();
        var first = await workspace.Provider.GetMetadataAsync(project: workspace.Context, cancellationToken: CancellationToken.None);
        first.Types.Should().HaveCount(expected: count);
        var unchanged = await workspace.Provider.GetMetadataAsync(project: workspace.Context, cancellationToken: CancellationToken.None);
        unchanged.Should().BeSameAs(expected: first);

        for (var cycle = 0; cycle < 2; cycle++)
        {
            // Isolate the project-owned fallback, independent of the global cache's eviction order.
            CSharpProjectMetadataProvider.ClearSyntaxTreeCacheForTests();
            await File.AppendAllTextAsync(path: workspace.Paths[0], contents: "\npublic class Added" + cycle.ToString(provider: CultureInfo.InvariantCulture) + " { }");
            MetadataCacheInvalidation.MarkDirty(fullPath: workspace.Paths[0]);
            var parsedBefore = MetadataCacheMetrics.ParsedSourceFiles;
            var rebuilt = await workspace.Provider.GetMetadataAsync(project: workspace.Context, cancellationToken: CancellationToken.None);
            rebuilt.Diagnostics.Should().BeEmpty();
            rebuilt.Types.Should().HaveCount(expected: count + cycle + 1);
            (MetadataCacheMetrics.ParsedSourceFiles - parsedBefore).Should().Be(expected: 1);
        }

        workspace.LoadCount.Should().Be(expected: 1);
    }

    [Fact]
    public async Task OwnedTreesPreserveDifferentParseConfigurationsForLinkedSources()
    {
        using var workspace = new Workspace();
        await workspace.CreateAsync(count: 2);
        await File.WriteAllTextAsync(
            path: workspace.Paths[0],
            contents: "#if FIRST\npublic class First { }\n#else\npublic class Second { }\n#endif");
        MetadataCacheInvalidation.EnableTracking();
        var firstContext = workspace.Context with { TargetFramework = "FIRST" };
        var secondContext = workspace.Context with { TargetFramework = "SECOND" };
        var first = await workspace.Provider.GetMetadataAsync(project: firstContext, cancellationToken: CancellationToken.None);
        var second = await workspace.Provider.GetMetadataAsync(project: secondContext, cancellationToken: CancellationToken.None);
        first.Types.Should().Contain(predicate: type => type.Name == "First");
        second.Types.Should().Contain(predicate: type => type.Name == "Second");
        var sharedParseCount = CSharpProjectMetadataProvider.GetSourceParseCountForTests(path: workspace.Paths[0]);
        CSharpProjectMetadataProvider.ClearSyntaxTreeCacheForTests();
        await File.AppendAllTextAsync(path: workspace.Paths[1], contents: "\npublic class Added { }");
        MetadataCacheInvalidation.MarkDirty(fullPath: workspace.Paths[1]);

        first = await workspace.Provider.GetMetadataAsync(project: firstContext, cancellationToken: CancellationToken.None);
        second = await workspace.Provider.GetMetadataAsync(project: secondContext, cancellationToken: CancellationToken.None);

        first.Types.Select(selector: type => type.Name).Should().BeEquivalentTo("First", "Type1", "Added");
        second.Types.Select(selector: type => type.Name).Should().BeEquivalentTo("Second", "Type1", "Added");
        CSharpProjectMetadataProvider.GetSourceParseCountForTests(path: workspace.Paths[0]).Should().Be(expected: sharedParseCount);
    }

    [Fact]
    public async Task SourceStampRemainsTheStampOfTheParsedContent()
    {
        using var workspace = new Workspace();
        await workspace.CreateAsync(count: 1);
        var changed = false;
        var context = workspace.Context with
        {
            PerformanceSink = timing =>
            {
                if (timing.Stage == "Parsing" && !changed)
                {
                    File.AppendAllText(path: workspace.Paths[0], contents: "\npublic class AddedDuringExtraction { }");
                    changed = true;
                }
            },
        };
        var first = await workspace.Provider.GetMetadataAsync(project: context, cancellationToken: CancellationToken.None);
        var next = await workspace.Provider.GetMetadataAsync(project: workspace.Context, cancellationToken: CancellationToken.None);
        first.Types.Should().ContainSingle();
        next.Types.Should().HaveCount(expected: 2);
        next.Types.Should().Contain(predicate: type => type.Name == "AddedDuringExtraction");
    }

    [Fact]
    public async Task TimingsBelongToTheCurrentRequestAndDoNotEnterCachedMetadata()
    {
        using var workspace = new Workspace();
        await workspace.CreateAsync(count: 2);
        var coldTimings = new List<MetadataStageTiming>();
        var first = await workspace.Provider.GetMetadataAsync(
            project: workspace.Context with { PerformanceSink = coldTimings.Add },
            cancellationToken: CancellationToken.None);
        var coldCount = coldTimings.Count;
        coldTimings.Select(selector: timing => timing.Stage).Should().Contain(
            expected: ["Cache validation (Miss)", "MSBuild", "Parsing", "Compilation/references", "Source generators", "Diagnostics", "Metadata extraction", "Metadata merge"]);
        coldTimings.Should().OnlyContain(predicate: timing => timing.ProjectPath == workspace.Context.ProjectPath && timing.Elapsed >= TimeSpan.Zero);
        var hitTimings = new List<MetadataStageTiming>();
        var cached = await workspace.Provider.GetMetadataAsync(
            project: workspace.Context with { PerformanceSink = hitTimings.Add },
            cancellationToken: CancellationToken.None);
        cached.Should().BeSameAs(expected: first);
        hitTimings.Select(selector: timing => timing.Stage).Should().Equal("Cache validation (Hit)", "Metadata merge");
        coldTimings.Should().HaveCount(expected: coldCount);
        cached.Diagnostics.Should().BeEmpty();
        await File.AppendAllTextAsync(path: workspace.Paths[0], contents: "\npublic class Added { }");
        hitTimings.Clear();
        await workspace.Provider.GetMetadataAsync(
            project: workspace.Context with { PerformanceSink = hitTimings.Add },
            cancellationToken: CancellationToken.None);
        hitTimings.Select(selector: timing => timing.Stage).Should().Contain(expected: ["Cache validation (RebuildFromSources)", "Parsing"]);
        hitTimings.Select(selector: timing => timing.Stage).Should().NotContain("MSBuild");
    }

    [Fact]
    public async Task TimingsPropagateToReferencesAndConcurrentRequestsRemainIndependent()
    {
        using var workspace = new Workspace();
        await workspace.CreateAsync(count: 1);
        var dependencyPath = await workspace.AddDependencyAsync();
        var firstTimings = new List<MetadataStageTiming>();
        var secondTimings = new List<MetadataStageTiming>();
        await Task.WhenAll(
            workspace.Provider.GetMetadataAsync(project: workspace.Context with { PerformanceSink = firstTimings.Add }, cancellationToken: CancellationToken.None),
            workspace.Provider.GetMetadataAsync(project: workspace.Context with { PerformanceSink = secondTimings.Add }, cancellationToken: CancellationToken.None));

        foreach (var timings in new[] { firstTimings, secondTimings })
        {
            timings.Should().Contain(predicate: timing => timing.ProjectPath == workspace.Context.ProjectPath && timing.Stage == "Metadata extraction");
            timings.Should().Contain(predicate: timing => timing.ProjectPath == dependencyPath && timing.Stage == "Metadata extraction");
            timings.Count(timing => timing.Stage == "Metadata merge").Should().Be(expected: 1);
        }

        var firstCount = firstTimings.Count;
        var secondCount = secondTimings.Count;
        var metadata = await workspace.Provider.GetMetadataAsync(project: workspace.Context, cancellationToken: CancellationToken.None);
        metadata.Types.Should().HaveCount(expected: 2);
        metadata.Diagnostics.Should().BeEmpty();
        firstTimings.Should().HaveCount(expected: firstCount);
        secondTimings.Should().HaveCount(expected: secondCount);
    }

    [Fact]
    public async Task ReferenceImagesAreSharedAcrossIndependentCompilations()
    {
        using var workspace = new Workspace();
        await workspace.CreateAsync(count: 1);
        var first = await workspace.Provider.GetCompilationAsync(project: workspace.Context with { TargetFramework = "FIRST" }, cancellationToken: CancellationToken.None);
        var second = await workspace.Provider.GetCompilationAsync(project: workspace.Context with { TargetFramework = "SECOND" }, cancellationToken: CancellationToken.None);
        first.Should().NotBeNull();
        second.Should().NotBeNull();
        first.References.Should().NotBeEmpty();
        first.References.Zip(second: second.References).Should().OnlyContain(predicate: pair => ReferenceEquals(pair.First, pair.Second));
    }

    [Fact]
    public async Task LoaderSessionIsSharedWithinGraphAndRecreatedForEachRequest()
    {
        using var workspace = new Workspace();
        await workspace.CreateAsync(count: 1);
        var dependency = await workspace.AddDependencyAsync();
        var first = await workspace.Provider.GetMetadataAsync(project: workspace.Context, cancellationToken: CancellationToken.None);
        first.Types.Should().HaveCount(expected: 2);
        workspace.SessionLoads.Should().ContainSingle();
        workspace.SessionLoads[0].Should().Equal(workspace.Context.ProjectPath, dependency);

        CSharpProjectMetadataProvider.ClearCachesForTests();
        await workspace.Provider.GetMetadataAsync(project: workspace.Context, cancellationToken: CancellationToken.None);
        workspace.SessionLoads.Should().HaveCount(expected: 2);
        workspace.SessionLoads[1].Should().Equal(workspace.Context.ProjectPath, dependency);
        await workspace.Provider.GetCompilationAsync(project: workspace.Context, cancellationToken: CancellationToken.None);
        workspace.SessionLoads.Should().HaveCount(expected: 3);
        workspace.SessionLoads[2].Should().BeEmpty();
    }

    [Fact]
    public async Task SourceOnlyRebuildReleasesPreviousCompilationWithLazyReferenceEdges()
    {
        using var workspace = new Workspace();
        await workspace.CreateAsync(count: 1);
        await File.WriteAllTextAsync(path: workspace.Paths[0], contents: "public class Model { public string Value { get; set; } = string.Empty; }");
        MetadataCacheInvalidation.EnableTracking();
        var previous = await CaptureCompilationAsync(provider: workspace.Provider, context: workspace.Context);
        previous.IsAlive.Should().BeTrue();
        for (var cycle = 0; cycle < 2; cycle++)
        {
            await File.AppendAllTextAsync(path: workspace.Paths[0], contents: "\npublic class Added" + cycle.ToString(provider: CultureInfo.InvariantCulture) + " { }");
            MetadataCacheInvalidation.MarkDirty(fullPath: workspace.Paths[0]);
            var current = await CaptureCompilationAsync(provider: workspace.Provider, context: workspace.Context);
            for (var attempt = 0; attempt < 10 && previous.IsAlive; attempt++)
            {
#pragma warning disable S1215 // Verify that replaced extraction graphs can be collected.
                GC.Collect();
#pragma warning restore S1215 // Verify that replaced extraction graphs can be collected.
                GC.WaitForPendingFinalizers();
            }

            previous.IsAlive.Should().BeFalse();
            current.IsAlive.Should().BeTrue();
            previous = current;
        }

        var metadata = await workspace.Provider.GetMetadataAsync(project: workspace.Context, cancellationToken: CancellationToken.None);
        metadata.Types.Single(predicate: type => type.Name == "Model").Properties.Single().Type.Interfaces.Should().NotBeEmpty();
        workspace.LoadCount.Should().Be(expected: 1);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<WeakReference> CaptureCompilationAsync(CSharpProjectMetadataProvider provider, ProjectContext context)
    {
        var compilation = await provider.GetCompilationAsync(project: context, cancellationToken: CancellationToken.None);
        return new WeakReference(target: compilation);
    }

    private sealed class Workspace : IProjectWorkspaceLoader, IDisposable
    {
        private readonly string _directory = Path.Combine(path1: Path.GetTempPath(), path2: "Typewriter.Reuse-" + Guid.NewGuid().ToString(format: "N"));
        private ProjectLoadResult? _loaded;
        private ProjectLoadResult? _dependency;

        public Workspace()
        {
            CSharpProjectMetadataProvider.ClearCachesForTests();
            MetadataCacheInvalidation.Reset();
            Directory.CreateDirectory(path: _directory);
            Context = new ProjectContext(ProjectPath: Path.Combine(path1: _directory, path2: "Project.csproj"), WorkspacePath: _directory);
            Provider = new CSharpProjectMetadataProvider(projectLoader: this);
        }

        public CSharpProjectMetadataProvider Provider { get; }

        public ProjectContext Context { get; }

        public string[] Paths { get; private set; } = [];

        public int LoadCount { get; private set; }

        public List<List<string>> SessionLoads { get; } = [];

        public IProjectWorkspaceLoader CreateSession()
        {
            var loads = new List<string>();
            SessionLoads.Add(item: loads);
            return new Session(owner: this, loads: loads);
        }

        public async Task CreateAsync(int count)
        {
            await File.WriteAllTextAsync(path: Context.ProjectPath, contents: "<Project />");
            Paths = new string[count];
            for (var index = 0; index < count; index++)
            {
                var name = "Type" + index.ToString(provider: CultureInfo.InvariantCulture);
                Paths[index] = Path.Combine(path1: _directory, path2: name + ".cs");
                await File.WriteAllTextAsync(path: Paths[index], contents: "public class " + name + " { }");
            }

            _loaded = new ProjectLoadResult(
                ProjectPath: Context.ProjectPath,
                ProjectDirectory: _directory,
                TargetFramework: "net10.0",
                NullableEnabled: true,
                ImplicitUsingsEnabled: false,
                SourceFiles: Paths,
                PreprocessorSymbols: [],
                GlobalUsings: [],
                ProjectReferences: [],
                ReferencePaths: [],
                AnalyzerReferences: [],
                AdditionalFiles: [],
                AnalyzerConfigFiles: [],
                Diagnostics: []);
        }

        public Task<ProjectLoadResult> LoadAsync(ProjectContext project, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LoadCount++;
            var loaded = _dependency?.ProjectPath == project.ProjectPath ? _dependency : _loaded!;
            return Task.FromResult(result: loaded with { PreprocessorSymbols = project.TargetFramework == "FIRST" ? ["FIRST"] : [] });
        }

        public async Task<string> AddDependencyAsync()
        {
            var path = Path.Combine(path1: _directory, path2: "Dependency.csproj");
            var source = Path.Combine(path1: _directory, path2: "Dependency.cs");
            await File.WriteAllTextAsync(path: path, contents: "<Project />");
            await File.WriteAllTextAsync(path: source, contents: "public class Dependency { }");
            _dependency = _loaded! with { ProjectPath = path, SourceFiles = [source] };
            _loaded = _loaded with { ProjectReferences = [path] };
            return path;
        }

        public void Dispose()
        {
            CSharpProjectMetadataProvider.ClearCachesForTests();
            MetadataCacheInvalidation.Reset();
            Directory.Delete(path: _directory, recursive: true);
        }

        private sealed class Session(Workspace owner, List<string> loads) : IProjectWorkspaceLoader
        {
            public Task<ProjectLoadResult> LoadAsync(ProjectContext project, CancellationToken cancellationToken)
            {
                loads.Add(item: project.ProjectPath);
                return owner.LoadAsync(project: project, cancellationToken: cancellationToken);
            }
        }
    }
}
