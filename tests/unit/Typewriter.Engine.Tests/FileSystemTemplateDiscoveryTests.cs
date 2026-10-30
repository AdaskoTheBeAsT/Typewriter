using AwesomeAssertions;
using Typewriter.Abstractions;
using Xunit;

namespace Typewriter.Engine.Tests;

public sealed class FileSystemTemplateDiscoveryTests
{
    [Fact]
    public async Task FindTemplatesAsyncDiscoversTemplatesWhenTemplatePathIsDirectory()
    {
        var directory = CreateDirectory();
        try
        {
            var nestedDirectory = Directory.CreateDirectory(path: Path.Combine(path1: directory, path2: "nested")).FullName;
            var ignoredDirectory = Directory.CreateDirectory(path: Path.Combine(path1: directory, path2: "bin")).FullName;
            var firstTemplatePath = Path.Combine(path1: directory, path2: "First.tst");
            var secondTemplatePath = Path.Combine(path1: nestedDirectory, path2: "Second.tst");
#pragma warning disable SEC0116
            await File.WriteAllTextAsync(path: firstTemplatePath, contents: "first");
            await File.WriteAllTextAsync(path: secondTemplatePath, contents: "second");
            await File.WriteAllTextAsync(path: Path.Combine(path1: ignoredDirectory, path2: "Ignored.tst"), contents: "ignored");
#pragma warning restore SEC0116
            var discovery = new FileSystemTemplateDiscovery();

            var templates = await discovery.FindTemplatesAsync(
                workspace: new WorkspaceContext(RootPath: directory),
                request: new GenerationRequest(
                    WorkspacePath: directory,
                    ProjectPath: null,
                    TemplatePath: directory,
                    Mode: GenerationMode.Generate,
                    Configuration: TypewriterConfiguration.Default),
                cancellationToken: CancellationToken.None);

            templates.Select(selector: template => template.Path)
                .Should().Equal(firstTemplatePath, secondTemplatePath);
            templates.Select(selector: template => template.Content)
                .Should().Equal("first", "second");
        }
        finally
        {
            Directory.Delete(path: directory, recursive: true);
        }
    }

    [Fact]
    public async Task FindTemplatesAsyncDiscoversTemplatesWhenRootOrNestedFolderIsNamedGeneratedIssue121()
    {
        var baseDirectory = CreateDirectory();
        try
        {
            var rootGeneratedDirectory = Directory.CreateDirectory(path: Path.Combine(path1: baseDirectory, path2: "Generated")).FullName;
            var nestedGeneratedDirectory = Directory.CreateDirectory(path: Path.Combine(path1: rootGeneratedDirectory, path2: "Generated")).FullName;
            var rootTemplatePath = Path.Combine(path1: rootGeneratedDirectory, path2: "Root.tst");
            var nestedTemplatePath = Path.Combine(path1: nestedGeneratedDirectory, path2: "Nested.tst");
#pragma warning disable SEC0116
            await File.WriteAllTextAsync(path: rootTemplatePath, contents: "root");
            await File.WriteAllTextAsync(path: nestedTemplatePath, contents: "nested");
#pragma warning restore SEC0116
            var discovery = new FileSystemTemplateDiscovery();

            var templates = await discovery.FindTemplatesAsync(
                workspace: new WorkspaceContext(RootPath: rootGeneratedDirectory),
                request: new GenerationRequest(
                    WorkspacePath: rootGeneratedDirectory,
                    ProjectPath: null,
                    TemplatePath: null,
                    Mode: GenerationMode.Generate,
                    Configuration: TypewriterConfiguration.Default),
                cancellationToken: CancellationToken.None);

            templates.Select(selector: template => template.Path)
                .Should().Equal(nestedTemplatePath, rootTemplatePath);
        }
        finally
        {
            Directory.Delete(path: baseDirectory, recursive: true);
        }
    }

    [Theory]
    [InlineData(data: ["Generated"])]
    [InlineData(data: ["Generated/"])]
    [InlineData(data: ["**/Generated/**"])]
    [InlineData(data: ["sub/Generated"])]
    public async Task FindTemplatesAsyncExcludesDirectoriesMatchingConfiguredExcludePatternIssue121(string excludePattern)
    {
        var baseDirectory = CreateDirectory();
        try
        {
            var workspaceDirectory = Directory.CreateDirectory(path: Path.Combine(path1: baseDirectory, path2: "Generated")).FullName;
            var keptDirectory = Directory.CreateDirectory(path: Path.Combine(path1: workspaceDirectory, path2: "Models")).FullName;
            var excludedDirectory = Directory.CreateDirectory(path: Path.Combine(path1: workspaceDirectory, path2: "sub", path3: "Generated")).FullName;
            var keptTemplatePath = Path.Combine(path1: keptDirectory, path2: "Kept.tst");
            var excludedTemplatePath = Path.Combine(path1: excludedDirectory, path2: "Excluded.tst");
#pragma warning disable SEC0116
            await File.WriteAllTextAsync(path: keptTemplatePath, contents: "kept");
            await File.WriteAllTextAsync(path: excludedTemplatePath, contents: "excluded");
#pragma warning restore SEC0116
            var discovery = new FileSystemTemplateDiscovery();
            var configuration = TypewriterConfiguration.Default with
            {
                Exclude = [.. TypewriterConfiguration.Default.Exclude, excludePattern],
            };

            var templates = await discovery.FindTemplatesAsync(
                workspace: new WorkspaceContext(RootPath: workspaceDirectory),
                request: new GenerationRequest(
                    WorkspacePath: workspaceDirectory,
                    ProjectPath: null,
                    TemplatePath: null,
                    Mode: GenerationMode.Generate,
                    Configuration: configuration),
                cancellationToken: CancellationToken.None);

            templates.Select(selector: template => template.Path)
                .Should().Equal(keptTemplatePath);
        }
        finally
        {
            Directory.Delete(path: baseDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task FindTemplatesAsyncScopesSolutionWorkspaceToSolutionProjectsAndIgnoresUnrelatedSubdirectories()
    {
        var directory = CreateDirectory();
        try
        {
            var appDirectory = Directory.CreateDirectory(path: Path.Combine(path1: directory, path2: "src", path3: "App")).FullName;
            var samplesDirectory = Directory.CreateDirectory(path: Path.Combine(path1: directory, path2: "samples", path3: "SampleApp")).FullName;
            var solutionPath = Path.Combine(path1: directory, path2: "Workspace.slnx");
            var appProjectPath = Path.Combine(path1: appDirectory, path2: "App.csproj");
            var sampleProjectPath = Path.Combine(path1: samplesDirectory, path2: "SampleApp.csproj");
            var appTemplatePath = Path.Combine(path1: appDirectory, path2: "Models.tst");
            var sampleTemplatePath = Path.Combine(path1: samplesDirectory, path2: "SampleModels.tst");
#pragma warning disable SEC0116
            await File.WriteAllTextAsync(
                path: solutionPath,
                contents: """
                          <Solution>
                            <Project Path="src/App/App.csproj" />
                          </Solution>
                          """);
            await File.WriteAllTextAsync(path: appProjectPath, contents: "<Project />");
            await File.WriteAllTextAsync(path: sampleProjectPath, contents: "<Project />");
            await File.WriteAllTextAsync(path: appTemplatePath, contents: "app");
            await File.WriteAllTextAsync(path: sampleTemplatePath, contents: "sample");
#pragma warning restore SEC0116
            var discovery = new FileSystemTemplateDiscovery();

            var templates = await discovery.FindTemplatesAsync(
                workspace: new WorkspaceContext(RootPath: solutionPath),
                request: new GenerationRequest(
                    WorkspacePath: solutionPath,
                    ProjectPath: null,
                    TemplatePath: null,
                    Mode: GenerationMode.Generate,
                    Configuration: TypewriterConfiguration.Default),
                cancellationToken: CancellationToken.None);

            templates.Select(selector: template => template.Path)
                .Should().Equal(appTemplatePath);
        }
        finally
        {
            Directory.Delete(path: directory, recursive: true);
        }
    }

    private static string CreateDirectory()
    {
        var directory = Path.Combine(
            path1: Path.GetTempPath(),
            path2: "Typewriter.Engine.Tests",
            path3: Guid.NewGuid().ToString(format: "N"));
        Directory.CreateDirectory(path: directory);
        return directory;
    }
}
