using Typewriter.Abstractions;
using Typewriter.Engine;
using Xunit;

namespace Typewriter.Engine.Tests;

public sealed class GeneratedFilePlannerTests
{
    [Fact]
    public void HeaderValueUsesDeterministicLineFeeds()
    {
        GeneratedFileHeader.Value.Should().Contain("\n");
        GeneratedFileHeader.Value.Should().NotContain("\r");
    }

    [Theory]
    [InlineData(data: "\n")]
    [InlineData(data: "\r\n")]
    public async Task TryPlanAllowsRegeneratingExistingFileWithGeneratedHeader(string newline)
    {
        var directory = CreateProjectDirectory();
        try
        {
            var templatePath = Path.Combine(path1: directory, path2: "Models.tst");
            var outputPath = Path.Combine(path1: directory, path2: "generated", path3: "models.ts");
            Directory.CreateDirectory(path: Path.GetDirectoryName(path: outputPath)!);
            await File.WriteAllTextAsync(path: templatePath, contents: string.Empty);
            await File.WriteAllTextAsync(
                path: outputPath,
                contents: GeneratedFileHeader.Value.Replace(oldValue: "\n", newValue: newline, comparisonType: StringComparison.Ordinal)
                          + $"{newline}{newline}export interface Old {{}}");

            var planner = new GeneratedFilePlanner();
            var planned = planner.TryPlan(
                workspace: new WorkspaceContext(RootPath: directory),
                template: new TemplateDocument(Path: templatePath, Content: string.Empty, OutputPath: "generated/models.ts"),
                content: "export interface Customer {}",
                generatedFile: out var generatedFile,
                diagnostic: out var diagnostic);

            planned.Should().BeTrue(because: diagnostic?.Message);
            diagnostic.Should().BeNull();
            var file = generatedFile ?? throw new InvalidOperationException(message: "Expected generated file.");
            file.Path.Should().Be(outputPath);
            file.Content.Should().StartWith(GeneratedFileHeader.Value);
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(directory: directory);
        }
    }

    [Fact]
    public async Task TryPlanAllowsOutputOutsideWorkspace()
    {
        var directory = CreateProjectDirectory();
        try
        {
            var backendDirectory = Path.Combine(path1: directory, path2: "backend");
            var templatePath = Path.Combine(path1: backendDirectory, path2: "Models.tst");
            var expectedOutputPath = Path.Combine(path1: directory, path2: "frontend", path3: "generated", path4: "models.ts");
            Directory.CreateDirectory(path: backendDirectory);
            await File.WriteAllTextAsync(path: templatePath, contents: string.Empty);

            var planner = new GeneratedFilePlanner();
            var planned = planner.TryPlan(
                workspace: new WorkspaceContext(RootPath: backendDirectory),
                template: new TemplateDocument(Path: templatePath, Content: string.Empty, OutputPath: "../frontend/generated/models.ts"),
                content: "export interface Customer {}",
                generatedFile: out var generatedFile,
                diagnostic: out var diagnostic);

            planned.Should().BeTrue(because: diagnostic?.Message);
            diagnostic.Should().BeNull();
            generatedFile.Should().NotBeNull();
            generatedFile!.Path.Should().Be(expectedOutputPath);
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(directory: directory);
        }
    }

    private static string CreateProjectDirectory()
    {
        var directory = Path.Combine(
            path1: Path.GetTempPath(),
            path2: "Typewriter.Engine.Tests",
            path3: Guid.NewGuid().ToString(format: "N"));
        Directory.CreateDirectory(path: directory);
        return directory;
    }

    private static async Task DeleteDirectoryWithRetryAsync(string directory)
    {
        const int MaxAttempts = 10;

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                if (Directory.Exists(path: directory))
                {
                    Directory.Delete(path: directory, recursive: true);
                }

                return;
            }
            catch (IOException) when (attempt < MaxAttempts)
            {
                await Task.Delay(millisecondsDelay: 100).ConfigureAwait(continueOnCapturedContext: false);
            }
            catch (UnauthorizedAccessException) when (attempt < MaxAttempts)
            {
                await Task.Delay(millisecondsDelay: 100).ConfigureAwait(continueOnCapturedContext: false);
            }
        }
    }
}
