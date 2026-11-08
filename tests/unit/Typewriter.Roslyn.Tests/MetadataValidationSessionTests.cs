using AwesomeAssertions;
using Typewriter.Abstractions;
using Typewriter.Buildalyzer;
using Xunit;

namespace Typewriter.Roslyn.Tests;

[Collection("Roslyn metadata cache")]
public sealed class MetadataValidationSessionTests
{
    [Fact]
    public async Task GraphValidationDoesNotAcknowledgeAnEditAfterAnEarlierManifestCheck()
    {
        using var workspace = new Workspace();
        MetadataCacheInvalidation.EnableTracking();
        await workspace.Provider.GetMetadataAsync(project: workspace.Context(name: "A"), cancellationToken: CancellationToken.None);
        MetadataCacheInvalidation.MarkDirty(fullPath: Path.Combine(path1: workspace.Directory, path2: "Unrelated.cs"));
        await workspace.Provider.GetMetadataAsync(project: workspace.Context(name: "A"), cancellationToken: CancellationToken.None);
        var edited = false;
        var context = workspace.Context(name: "Root") with
        {
            PerformanceSink = timing =>
            {
                if (!edited
                    && timing.ProjectPath == workspace.Context(name: "A").ProjectPath
                    && timing.Stage.StartsWith(value: "Cache validation", comparisonType: StringComparison.Ordinal))
                {
                    var source = workspace.SourcePath(name: "B");
                    File.WriteAllText(path: source, contents: "public class AfterChanged { }");
                    MetadataCacheInvalidation.MarkDirty(fullPath: source);
                    edited = true;
                }
            },
        };

        await workspace.Provider.GetMetadataAsync(project: context, cancellationToken: CancellationToken.None);

        edited.Should().BeTrue();
        foreach (var name in new[] { "B", "Root", "B", "Root" })
        {
            var metadata = await workspace.Provider.GetMetadataAsync(project: workspace.Context(name: name), cancellationToken: CancellationToken.None);
            metadata.Diagnostics.Should().BeEmpty();
            metadata.Types.Should().Contain(predicate: type => type.Name == "AfterChanged");
            metadata.Types.Should().NotContain(predicate: type => type.Name == "Before");
        }
    }

    private sealed class Workspace : IProjectWorkspaceLoader, IDisposable
    {
        private readonly Dictionary<string, ProjectLoadResult> _projects = new(comparer: StringComparer.OrdinalIgnoreCase);

        public Workspace()
        {
            CSharpProjectMetadataProvider.ClearCachesForTests();
            MetadataCacheInvalidation.Reset();
            System.IO.Directory.CreateDirectory(path: Directory);
            foreach (var name in new[] { "Root", "A", "B" })
            {
                var context = Context(name: name);
                var directory = Path.GetDirectoryName(path: context.ProjectPath)!;
                System.IO.Directory.CreateDirectory(path: directory);
                File.WriteAllText(path: context.ProjectPath, contents: "<Project />");
                File.WriteAllText(path: SourcePath(name: name), contents: "public class " + (name == "B" ? "Before" : name) + " { }");
                string[] references = name switch
                {
                    "Root" => ["A", "B"],
                    "A" => ["B"],
                    _ => [],
                };
                _projects.Add(
                    key: context.ProjectPath,
                    value: new ProjectLoadResult(
                        ProjectPath: context.ProjectPath,
                        ProjectDirectory: directory,
                        TargetFramework: "net10.0",
                        NullableEnabled: true,
                        ImplicitUsingsEnabled: false,
                        SourceFiles: [SourcePath(name: name)],
                        PreprocessorSymbols: [],
                        GlobalUsings: [],
                        ProjectReferences: references.Select(selector: reference => Context(name: reference).ProjectPath).ToArray(),
                        ReferencePaths: [],
                        AnalyzerReferences: [],
                        AdditionalFiles: [],
                        AnalyzerConfigFiles: [],
                        Diagnostics: []));
            }

            Provider = new CSharpProjectMetadataProvider(projectLoader: this);
        }

        public string Directory { get; } = Path.Combine(path1: Path.GetTempPath(), path2: "Typewriter.Validation-" + Guid.NewGuid().ToString(format: "N"));

        public CSharpProjectMetadataProvider Provider { get; }

        public ProjectContext Context(string name) =>
            new(ProjectPath: Path.Combine(path1: Directory, path2: name, path3: name + ".csproj"), WorkspacePath: Directory, TargetFramework: "net10.0");

        public string SourcePath(string name) => Path.Combine(path1: Directory, path2: name, path3: name + ".cs");

        public Task<ProjectLoadResult> LoadAsync(ProjectContext project, CancellationToken cancellationToken) =>
            Task.FromResult(result: _projects[project.ProjectPath]);

        public void Dispose()
        {
            CSharpProjectMetadataProvider.ClearCachesForTests();
            MetadataCacheInvalidation.Reset();
            System.IO.Directory.Delete(path: Directory, recursive: true);
        }
    }
}
