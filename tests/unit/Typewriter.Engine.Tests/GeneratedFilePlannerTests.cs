using Typewriter.Abstractions;
using Typewriter.Engine;
using Xunit;

namespace Typewriter.Engine.Tests;

public sealed class GeneratedFilePlannerTests
{
    [Fact]
    public void HeaderValueUsesDeterministicLineFeeds()
    {
        Assert.Contains(expectedSubstring: "\n", actualString: GeneratedFileHeader.Value, comparisonType: StringComparison.Ordinal);
        Assert.DoesNotContain(expectedSubstring: "\r", actualString: GeneratedFileHeader.Value, comparisonType: StringComparison.Ordinal);
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

            Assert.True(condition: planned, userMessage: diagnostic?.Message);
            Assert.Null(@object: diagnostic);
            var file = generatedFile ?? throw new InvalidOperationException(message: "Expected generated file.");
            Assert.Equal(expected: outputPath, actual: file.Path);
            Assert.StartsWith(expectedStartString: GeneratedFileHeader.Value, actualString: file.Content, comparisonType: StringComparison.Ordinal);
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
