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
