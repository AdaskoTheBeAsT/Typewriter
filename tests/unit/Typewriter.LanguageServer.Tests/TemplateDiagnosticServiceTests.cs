using Xunit;

namespace Typewriter.LanguageServer.Tests;

public sealed class TemplateDiagnosticServiceTests
{
    [Fact]
    public async Task ValidateAsyncUsesOpenDocumentText()
    {
        var directory = CreateProjectDirectory();
        try
        {
            var project = await CreateSimpleProjectAsync(directory: directory);
            var document = new TextDocumentState(
                Uri: UriFromPath(path: project.TemplatePath),
                Path: project.TemplatePath,
                Text: "${",
                Version: 2);
            var settings = new LanguageServerSettings(
                RootPath: directory,
                WorkspacePath: directory,
                ProjectPath: project.ProjectPath,
                Framework: "net10.0",
                AllProjects: false);

            var diagnostics = await new TemplateDiagnosticService()
                .ValidateAsync(document: document, settings: settings, cancellationToken: CancellationToken.None);

            var diagnostic = Assert.Single(collection: diagnostics.Where(predicate: item => item.Code == "TW0002"));
            Assert.Equal(expected: project.TemplatePath, actual: diagnostic.File);
            Assert.Contains(expectedSubstring: "not closed", actualString: diagnostic.Message, comparisonType: StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(directory: directory);
        }
    }

    [Fact]
    public void MapUsesZeroBasedLspRanges()
    {
        var templatePath = Path.Combine(path1: Path.GetTempPath(), path2: "Models.tst");
        var document = new TextDocumentState(
            Uri: UriFromPath(path: templatePath),
            Path: templatePath,
            Text: string.Empty,
            Version: 1);

        var diagnostics = LspDiagnosticMapper.Map(
            document: document,
            diagnostics:
            [
                new(
                    File: templatePath,
                    Line: 3,
                    Column: 5,
                    Severity: Typewriter.Abstractions.DiagnosticSeverity.Error,
                    Message: "Broken template",
                    Code: "TW0002"),
            ]);

        var diagnostic = Assert.Single(collection: diagnostics);
        Assert.Equal(expected: 2, actual: diagnostic.Range.Start.Line);
        Assert.Equal(expected: 4, actual: diagnostic.Range.Start.Character);
        Assert.Equal(expected: 1, actual: diagnostic.Severity);
        Assert.Equal(expected: "TW0002", actual: diagnostic.Code);
    }

    private static async Task<SampleProject> CreateSimpleProjectAsync(string directory)
    {
        Directory.CreateDirectory(path: directory);
        var projectPath = Path.Combine(path1: directory, path2: "Sample.csproj");
        var templatePath = Path.Combine(path1: directory, path2: "Models.tst");
        var modelDirectory = Path.Combine(path1: directory, path2: "Models");
        Directory.CreateDirectory(path: modelDirectory);

        await File.WriteAllTextAsync(
            path: projectPath,
            contents: """
                      <Project Sdk="Microsoft.NET.Sdk">
                        <PropertyGroup>
                          <TargetFramework>net10.0</TargetFramework>
                          <ImplicitUsings>enable</ImplicitUsings>
                          <Nullable>enable</Nullable>
                        </PropertyGroup>
                      </Project>
                      """).ConfigureAwait(continueOnCapturedContext: false);
        await File.WriteAllTextAsync(
            path: Path.Combine(path1: modelDirectory, path2: "Customer.cs"),
            contents: """
                      namespace Sample.Models;

                      public sealed class Customer
                      {
                          public required string Name { get; init; }
                      }
                      """).ConfigureAwait(continueOnCapturedContext: false);
        await File.WriteAllTextAsync(
            path: templatePath,
            contents: """
                      // output: generated/models.ts
                      $Classes[$Name]
                      """).ConfigureAwait(continueOnCapturedContext: false);

        return new SampleProject(ProjectPath: projectPath, TemplatePath: templatePath);
    }

    private static string CreateProjectDirectory()
    {
        var directory = Path.Combine(
            path1: Path.GetTempPath(),
            path2: "Typewriter.LanguageServer.Tests",
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

    private static string UriFromPath(string path) => new Uri(uriString: path).AbsoluteUri;

    private sealed record SampleProject(
        string ProjectPath,
        string TemplatePath);
}
