using System.Globalization;
using System.Text.Json;
using Xunit;

namespace Typewriter.Cli.Tests;

public sealed class CliIntegrationTests
{
    private const string DiagnosticsPropertyName = "diagnostics";
    private const string GeneratedFilesPropertyName = "generatedFiles";
    private const string ConfigurationFileName = "typewriter.json";
    private static readonly SemaphoreSlim ConsoleLock = new(initialCount: 1, maxCount: 1);

    [Fact]
    public async Task RunAsyncInitWritesDefaultConfiguration()
    {
        var directory = CreateProjectDirectory();
        try
        {
            var configurationPath = Path.Combine(path1: directory, path2: ConfigurationFileName);

            var result = await RunCliRawAsync(
                cancellationToken: CancellationToken.None,
                "init",
                "--workspace",
                directory);

            result.ExitCode.Should().Be(0);
            result.StandardError.Should().BeEmpty();
            result.StandardOutput.Should().Contain($"created: {configurationPath}");

            using var configuration = JsonDocument.Parse(json: await File.ReadAllTextAsync(path: configurationPath));
            var root = configuration.RootElement;
            ReadStringArray(element: root.GetProperty(propertyName: "templates")).Should().Equal("**/*.tst");
            ReadStringArray(element: root.GetProperty(propertyName: "exclude")).Should().Equal("**/bin/**", "**/obj/**", "**/node_modules/**");
            root.GetProperty(propertyName: "defaultTargetFramework").ValueKind.Should().Be(JsonValueKind.Null);

            var output = root.GetProperty(propertyName: "output");
            output.GetProperty(propertyName: "newline").GetString().Should().Be("lf");
            output.GetProperty(propertyName: "encoding").GetString().Should().Be("utf-8");
            output.GetProperty(propertyName: "writeOnlyWhenChanged").GetBoolean().Should().BeTrue();
            output.GetProperty(propertyName: "dryRun").GetBoolean().Should().BeFalse();
            output.GetProperty(propertyName: "fileNameConvention").GetString().Should().Be("preserve");

            var diagnostics = root.GetProperty(propertyName: "diagnostics");
            diagnostics.GetProperty(propertyName: "failOnWarning").GetBoolean().Should().BeFalse();
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(directory: directory);
        }
    }

    [Fact]
    public async Task RunAsyncInitDoesNotOverwriteExistingConfiguration()
    {
        var directory = CreateProjectDirectory();
        try
        {
            var configurationPath = Path.Combine(path1: directory, path2: ConfigurationFileName);
            const string ExistingConfiguration = "{}";
            await File.WriteAllTextAsync(path: configurationPath, contents: ExistingConfiguration);

            var result = await RunCliRawAsync(
                cancellationToken: CancellationToken.None,
                "init",
                "--workspace",
                directory);

            result.ExitCode.Should().Be(1);
            result.StandardError.Should().Contain("Configuration file already exists:");
            (await File.ReadAllTextAsync(path: configurationPath)).Should().Be(ExistingConfiguration);
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(directory: directory);
        }
    }

    [Fact]
    public async Task RunAsyncInitForceOverwritesExistingConfiguration()
    {
        var directory = CreateProjectDirectory();
        try
        {
            var configurationPath = Path.Combine(path1: directory, path2: ConfigurationFileName);
            await File.WriteAllTextAsync(path: configurationPath, contents: "{}");

            var result = await RunCliRawAsync(
                cancellationToken: CancellationToken.None,
                "init",
                "--workspace",
                directory,
                "--force");

            result.ExitCode.Should().Be(0);
            result.StandardError.Should().BeEmpty();
            result.StandardOutput.Should().Contain($"updated: {configurationPath}");
            (await File.ReadAllTextAsync(path: configurationPath)).Should().Contain("\"templates\"");
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(directory: directory);
        }
    }

    [Fact]
    public async Task RunAsyncGenerateWritesFileAndReturnsJsonResult()
    {
        var directory = CreateProjectDirectory();
        try
        {
            var project = await CreateSimpleProjectAsync(directory: directory);
            var generatedPath = Path.Combine(path1: directory, path2: "generated", path3: "models.ts");

            var result = await RunCliAsync(
                "generate",
                "--workspace",
                directory,
                "--project",
                project.ProjectPath,
                "--template",
                project.TemplatePath,
                "--framework",
                "net10.0",
                "--output",
                "json");

            result.ExitCode.Should().Be(0);
            result.Success.Should().BeTrue(because: result.StandardError);
            result.Diagnostics.Should().BeEmpty();

            var generatedFile = result.GeneratedFiles.Should().ContainSingle().Which;
            generatedFile.Path.Should().Be(generatedPath);
            generatedFile.Changed.Should().BeTrue();

            var generatedContent = await File.ReadAllTextAsync(path: generatedPath);
            generatedContent.Should().Contain("export interface Customer");
            generatedContent.Should().Contain("name: string;");
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(directory: directory);
        }
    }

    [Fact]
    public async Task RunAsyncGenerateAllProjectsWritesEveryProjectTemplate()
    {
        var directory = CreateProjectDirectory();
        try
        {
            var firstDirectory = Path.Combine(path1: directory, path2: "First");
            var secondDirectory = Path.Combine(path1: directory, path2: "Second");
            _ = await CreateSimpleProjectAsync(directory: firstDirectory);
            _ = await CreateSimpleProjectAsync(directory: secondDirectory);

            var solutionPath = Path.Combine(path1: directory, path2: "Sample.slnx");
            await File.WriteAllTextAsync(
                path: solutionPath,
                contents: """
                          <Solution>
                            <Project Path="First/Sample.csproj" />
                            <Project Path="Second/Sample.csproj" />
                          </Solution>
                          """);

            var result = await RunCliAsync(
                "generate",
                "--workspace",
                solutionPath,
                "--framework",
                "net10.0",
                "--output",
                "json",
                "--all-projects");

            result.ExitCode.Should().Be(0);
            result.Success.Should().BeTrue(because: result.StandardError);
            result.Diagnostics.Should().BeEmpty();
            result.GeneratedFiles.Select(selector: file => file.Path)
                .Order(comparer: StringComparer.OrdinalIgnoreCase)
                .Should().Equal(
                    Path.Combine(path1: firstDirectory, path2: "generated", path3: "models.ts"),
                    Path.Combine(path1: secondDirectory, path2: "generated", path3: "models.ts"));
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(directory: directory);
        }
    }

    [Fact]
    public async Task RunAsyncWatchGeneratesInitiallyAndStopsOnCancellation()
    {
        var directory = CreateProjectDirectory();
        try
        {
            var project = await CreateSimpleProjectAsync(directory: directory);
            var generatedPath = Path.Combine(path1: directory, path2: "generated", path3: "models.ts");
            using var cancellation = new CancellationTokenSource();

            var runTask = RunCliAsync(
                cancellationToken: cancellation.Token,
                "watch",
                "--workspace",
                directory,
                "--project",
                project.ProjectPath,
                "--template",
                project.TemplatePath,
                "--framework",
                "net10.0",
                "--output",
                "json");

            await WaitForFileAsync(path: generatedPath, cancellationToken: cancellation.Token);
            await Task.Delay(millisecondsDelay: 250, cancellationToken: CancellationToken.None);
            await cancellation.CancelAsync();

            var result = await runTask.WaitAsync(timeout: TimeSpan.FromSeconds(seconds: 30));

            result.ExitCode.Should().Be(0);
            result.Success.Should().BeTrue(because: result.StandardError);
            result.GeneratedFiles.Should().Contain(file => file.Path.Equals(value: generatedPath, comparisonType: StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(directory: directory);
        }
    }

    [Fact]
    public async Task RunAsyncWatchRegeneratesAfterSourceChange()
    {
        var directory = CreateProjectDirectory();
        try
        {
            var project = await CreateSimpleProjectAsync(directory: directory);
            var generatedPath = Path.Combine(path1: directory, path2: "generated", path3: "models.ts");
            using var cancellation = new CancellationTokenSource();

            var runTask = RunCliRawAsync(
                cancellationToken: cancellation.Token,
                "watch",
                "--workspace",
                directory,
                "--project",
                project.ProjectPath,
                "--template",
                project.TemplatePath,
                "--framework",
                "net10.0");

            await WaitForFileContentAsync(path: generatedPath, expectedContent: "name: string;", cancellationToken: cancellation.Token);
            await File.WriteAllTextAsync(
                path: project.SourcePath,
                contents: """
                          namespace Sample.Models;

                          public sealed class Customer
                          {
                              public required string Name { get; init; }

                              public required string Email { get; init; }
                          }
                          """);
            await WaitForFileContentAsync(path: generatedPath, expectedContent: "email: string;", cancellationToken: cancellation.Token);
            await cancellation.CancelAsync();

            var result = await runTask.WaitAsync(timeout: TimeSpan.FromSeconds(seconds: 30));

            result.ExitCode.Should().Be(0);
            result.StandardOutput.Should().Contain("updated:");
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(directory: directory);
        }
    }

    [Fact]
    public async Task RunAsyncMissingProjectReturnsProjectLoadExitCodeAndJsonDiagnostic()
    {
        var directory = CreateProjectDirectory();
        try
        {
            var templatePath = Path.Combine(path1: directory, path2: "Models.tst");
            await File.WriteAllTextAsync(
                path: templatePath,
                contents: """
                          // output: generated/models.ts
                          $Classes[]
                          """);

            var missingProjectPath = Path.Combine(path1: directory, path2: "Missing.csproj");
            var result = await RunCliAsync(
                "generate",
                "--workspace",
                directory,
                "--project",
                missingProjectPath,
                "--template",
                templatePath,
                "--output",
                "json");

            result.ExitCode.Should().Be(3);
            result.Success.Should().BeFalse();

            var diagnostic = result.Diagnostics.Should().ContainSingle().Which;
            diagnostic.Code.Should().Be("TW0003");
            diagnostic.Severity.Should().Be("error");
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(directory: directory);
        }
    }

    [Fact]
    public async Task RunAsyncTemplateParseFailureReturnsTemplateExitCodeAndJsonDiagnostic()
    {
        var directory = CreateProjectDirectory();
        try
        {
            var project = await CreateSimpleProjectAsync(
                directory: directory,
                templateContent: """
                                 ${
                                 """);

            var result = await RunCliAsync(
                "validate",
                "--workspace",
                directory,
                "--project",
                project.ProjectPath,
                "--template",
                project.TemplatePath,
                "--framework",
                "net10.0",
                "--output",
                "json");

            result.ExitCode.Should().Be(4);
            result.Success.Should().BeFalse();

            var diagnostic = result.Diagnostics.Should().ContainSingle().Which;
            diagnostic.Code.Should().Be("TW0002");
            diagnostic.Severity.Should().Be("error");
            diagnostic.Message.Should().ContainEquivalentOf("not closed");
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(directory: directory);
        }
    }

    [Fact]
    public async Task RunAsyncTemplateCompileFailureWritesCompilerStyleDiagnosticToStandardError()
    {
        var directory = CreateProjectDirectory();
        try
        {
            var project = await CreateSimpleProjectAsync(
                directory: directory,
                templateContent: """
                                 ${
                                     string Broken(Property property) => Missing.Symbol;
                                 }
                                 $Classes[$Properties[$Broken;]]
                                 """);

            var result = await RunCliRawAsync(
                cancellationToken: CancellationToken.None,
                "validate",
                "--workspace",
                directory,
                "--project",
                project.ProjectPath,
                "--template",
                project.TemplatePath,
                "--framework",
                "net10.0");

            result.ExitCode.Should().Be(4);
            result.StandardError.Should().Contain("Models.tst(");
            result.StandardError.Should().Contain("error TW0002:");
            result.StandardError.Should().Contain("CS0103");
            result.StandardOutput.Should().NotContain("error TW0002:");
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(directory: directory);
        }
    }

    [Fact]
    public async Task RunAsyncTemplateCompileFailureDoesNotWriteGeneratedFile()
    {
        var directory = CreateProjectDirectory();
        try
        {
            var project = await CreateSimpleProjectAsync(
                directory: directory,
                templateContent: """
                                 // output: generated/models.ts
                                 ${
                                     string Broken(Property property) => Missing.Symbol;
                                 }
                                 $Classes[$Properties[$Broken;]]
                                 """);
            var generatedPath = Path.Combine(path1: directory, path2: "generated", path3: "models.ts");

            var result = await RunCliRawAsync(
                cancellationToken: CancellationToken.None,
                "generate",
                "--workspace",
                directory,
                "--project",
                project.ProjectPath,
                "--template",
                project.TemplatePath,
                "--framework",
                "net10.0");

            result.ExitCode.Should().Be(4);
            result.StandardError.Should().Contain("error TW0002:");
            File.Exists(path: generatedPath).Should().BeFalse();
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(directory: directory);
        }
    }

    private static async Task<CliRawRunResult> RunCliRawAsync(
        CancellationToken cancellationToken,
        params string[] args)
    {
        await ConsoleLock.WaitAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        try
        {
            var originalOut = Console.Out;
            var originalError = Console.Error;
            await using var output = new StringWriter(formatProvider: CultureInfo.InvariantCulture);
            await using var error = new StringWriter(formatProvider: CultureInfo.InvariantCulture);

            try
            {
                Console.SetOut(newOut: output);
                Console.SetError(newError: error);

                var exitCode = await TypewriterCli.RunAsync(args: args, cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
                return new CliRawRunResult(
                    ExitCode: exitCode,
                    StandardOutput: output.ToString(),
                    StandardError: error.ToString());
            }
            finally
            {
                Console.SetOut(newOut: originalOut);
                Console.SetError(newError: originalError);
            }
        }
        finally
        {
            _ = ConsoleLock.Release();
        }
    }

    private static Task<CliRunResult> RunCliAsync(params string[] args) =>
        RunCliAsync(cancellationToken: CancellationToken.None, args: args);

    private static async Task<CliRunResult> RunCliAsync(
        CancellationToken cancellationToken,
        params string[] args)
    {
        await ConsoleLock.WaitAsync().ConfigureAwait(continueOnCapturedContext: false);
        try
        {
            var originalOut = Console.Out;
            var originalError = Console.Error;
            await using var output = new StringWriter(formatProvider: CultureInfo.InvariantCulture);
            await using var error = new StringWriter(formatProvider: CultureInfo.InvariantCulture);

            try
            {
                Console.SetOut(newOut: output);
                Console.SetError(newError: error);

                var exitCode = await TypewriterCli.RunAsync(args: args, cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
                var standardOutput = output.ToString();
                var standardError = error.ToString();
                using var json = JsonDocument.Parse(json: standardOutput);
                var root = json.RootElement;
                return new CliRunResult(
                    ExitCode: exitCode,
                    StandardOutput: standardOutput,
                    StandardError: standardError,
                    Success: root.GetProperty(propertyName: "success").GetBoolean(),
                    GeneratedFiles: ReadGeneratedFiles(root: root),
                    Diagnostics: ReadDiagnostics(root: root));
            }
            finally
            {
                Console.SetOut(newOut: originalOut);
                Console.SetError(newError: originalError);
            }
        }
        finally
        {
            _ = ConsoleLock.Release();
        }
    }

    private static IReadOnlyList<CliGeneratedFile> ReadGeneratedFiles(JsonElement root)
    {
        var files = new List<CliGeneratedFile>();
        using var generatedFiles = root.GetProperty(propertyName: GeneratedFilesPropertyName).EnumerateArray();
        while (generatedFiles.MoveNext())
        {
            var file = generatedFiles.Current;
            files.Add(
                item: new CliGeneratedFile(
                    Path: file.GetProperty(propertyName: "path").GetString() ?? string.Empty,
                    Changed: file.GetProperty(propertyName: "changed").GetBoolean()));
        }

        return files;
    }

    private static IReadOnlyList<CliDiagnostic> ReadDiagnostics(JsonElement root)
    {
        var diagnostics = new List<CliDiagnostic>();
        using var diagnosticEnumerator = root.GetProperty(propertyName: DiagnosticsPropertyName).EnumerateArray();
        while (diagnosticEnumerator.MoveNext())
        {
            var diagnostic = diagnosticEnumerator.Current;
            diagnostics.Add(
                item: new CliDiagnostic(
                    Severity: diagnostic.GetProperty(propertyName: "severity").GetString() ?? string.Empty,
                    Message: diagnostic.GetProperty(propertyName: "message").GetString() ?? string.Empty,
                    Code: diagnostic.GetProperty(propertyName: "code").GetString()));
        }

        return diagnostics;
    }

    private static IReadOnlyList<string?> ReadStringArray(JsonElement element)
    {
        var values = new List<string?>();
        using var enumerator = element.EnumerateArray();
        while (enumerator.MoveNext())
        {
            values.Add(item: enumerator.Current.GetString());
        }

        return values;
    }

    private static async Task WaitForFileAsync(
        string path,
        CancellationToken cancellationToken)
    {
        var timeoutAt = DateTimeOffset.UtcNow.AddSeconds(seconds: 30);
        while (!File.Exists(path: path))
        {
            if (DateTimeOffset.UtcNow >= timeoutAt)
            {
                throw new TimeoutException(message: $"File was not generated: {path}");
            }

            await Task.Delay(millisecondsDelay: 100, cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        }
    }

    private static async Task WaitForFileContentAsync(
        string path,
        string expectedContent,
        CancellationToken cancellationToken)
    {
        var timeoutAt = DateTimeOffset.UtcNow.AddSeconds(seconds: 30);
        string? lastContent = null;
        while (true)
        {
            if (File.Exists(path: path))
            {
                try
                {
                    var content = await File.ReadAllTextAsync(path: path, cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
                    lastContent = content;
                    if (content.Contains(value: expectedContent, comparisonType: StringComparison.Ordinal))
                    {
                        return;
                    }
                }
                catch (IOException) when (DateTimeOffset.UtcNow < timeoutAt)
                {
                    await Task.Delay(millisecondsDelay: 100, cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
                }
            }

            if (DateTimeOffset.UtcNow >= timeoutAt)
            {
                var message = $"Expected content was not generated in {path}: {expectedContent}";
                if (lastContent is not null)
                {
                    message += $"{Environment.NewLine}Last content:{Environment.NewLine}{lastContent}";
                }

                throw new TimeoutException(message: message);
            }

            await Task.Delay(millisecondsDelay: 100, cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        }
    }

    private static async Task<SampleProject> CreateSimpleProjectAsync(
        string directory,
        string? templateContent = null)
    {
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
                      """);
        await File.WriteAllTextAsync(
            path: Path.Combine(path1: modelDirectory, path2: "Customer.cs"),
            contents: """
                      namespace Sample.Models;

                      public sealed class Customer
                      {
                          public required string Name { get; init; }
                      }
                      """);
        await File.WriteAllTextAsync(
            path: templatePath,
            contents: templateContent
                      ?? """
                         // output: generated/models.ts
                         $Classes[
                         export interface $Name {
                         $Properties[
                           $name: $Type;]
                         }
                         ]
                         """);

        return new SampleProject(ProjectPath: projectPath, TemplatePath: templatePath, SourcePath: Path.Combine(path1: modelDirectory, path2: "Customer.cs"));
    }

    private static string CreateProjectDirectory()
    {
        var directory = Path.Combine(
            path1: Path.GetTempPath(),
            path2: "Typewriter.Cli.Tests",
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

    private sealed record SampleProject(
        string ProjectPath,
        string TemplatePath,
        string SourcePath);

    private sealed record CliRawRunResult(
        int ExitCode,
        string StandardOutput,
        string StandardError);

    private sealed record CliRunResult(
        int ExitCode,
        string StandardOutput,
        string StandardError,
        bool Success,
        IReadOnlyList<CliGeneratedFile> GeneratedFiles,
        IReadOnlyList<CliDiagnostic> Diagnostics);

    private sealed record CliGeneratedFile(string Path, bool Changed);

    private sealed record CliDiagnostic(
        string Severity,
        string Message,
        string? Code);
}
