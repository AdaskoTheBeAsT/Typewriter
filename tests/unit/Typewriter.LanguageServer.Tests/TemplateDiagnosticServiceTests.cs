using AwesomeAssertions;
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

            var diagnostic = diagnostics.Should().ContainSingle(item => item.Code == "TW0002").Which;
            diagnostic.File.Should().Be(project.TemplatePath);
            diagnostic.Message.Should().ContainEquivalentOf("not closed");
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(directory: directory);
        }
    }

    [Fact]
    public async Task ValidateAsyncDoesNotReportUnclosedBlockForEscapedInterpolatedBracesIssue114()
    {
        var directory = CreateProjectDirectory();
        try
        {
            var project = await CreateSimpleProjectAsync(directory: directory);
            const string issue114Template = """
                ${
                    string ModelName(Class model) => model.Name;

                    string Foo(Class model)
                    {
                        var methodName = "foo";
                        var sb = new StringBuilder();
                        sb.AppendLine($"public {methodName}() {{");
                        sb.AppendLine("}");
                        return sb.ToString();
                    }
                }
                $Classes[export class $ModelName {
                    $Foo
                }]
                """;
            var document = new TextDocumentState(
                Uri: UriFromPath(path: project.TemplatePath),
                Path: project.TemplatePath,
                Text: issue114Template,
                Version: 2);
            var settings = new LanguageServerSettings(
                RootPath: directory,
                WorkspacePath: directory,
                ProjectPath: project.ProjectPath,
                Framework: "net10.0",
                AllProjects: false);

            var diagnostics = await new TemplateDiagnosticService()
                .ValidateAsync(document: document, settings: settings, cancellationToken: CancellationToken.None);

            diagnostics.Should().BeEmpty();
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(directory: directory);
        }
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public async Task ValidateAsyncMapsCompileErrorsToOpenDocumentPositions(string lineEnding)
    {
        var directory = CreateProjectDirectory();
        try
        {
            var project = await CreateSimpleProjectAsync(directory: directory);
            var template = """
                // Output before the helper block.
                // Another output line.
                ${
                    string MemberTypes(IEnumerable<Interface> interfaces)
                    {
                        return InterfaceNames(interfaces);
                    }

                    string InterfaceNames(IInterfaceCollection interfaces) => "";
                }
                // Output between helper blocks.
                ${
                    string FileSpecificImports(File f) => "";
                }
                """.ReplaceLineEndings(replacementText: lineEnding);
            var document = new TextDocumentState(
                Uri: UriFromPath(path: project.TemplatePath),
                Path: project.TemplatePath,
                Text: template,
                Version: 2);
            var settings = new LanguageServerSettings(
                RootPath: directory,
                WorkspacePath: directory,
                ProjectPath: project.ProjectPath,
                Framework: "net10.0",
                AllProjects: false);

            var diagnostics = await new TemplateDiagnosticService()
                .ValidateAsync(document: document, settings: settings, cancellationToken: CancellationToken.None);

            var diagnostic = diagnostics.Should().ContainSingle().Which;
            diagnostic.Code.Should().Be("TW0002");
            diagnostic.Message.Should().Contain("CS1503");
            diagnostic.File.Should().Be(project.TemplatePath);
            diagnostic.Line.Should().Be(6);
            diagnostic.Column.Should().Be(31);

            var mapped = LspDiagnosticMapper.Map(document: document, diagnostics: diagnostics).Should().ContainSingle().Which;
            mapped.Range.Start.Line.Should().Be(5);
            mapped.Range.Start.Character.Should().Be(30);
            mapped.Range.End.Line.Should().Be(5);
            mapped.Range.End.Character.Should().Be(31);
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

        var diagnostic = diagnostics.Should().ContainSingle().Which;
        diagnostic.Range.Start.Line.Should().Be(2);
        diagnostic.Range.Start.Character.Should().Be(4);
        diagnostic.Severity.Should().Be(1);
        diagnostic.Code.Should().Be("TW0002");
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
