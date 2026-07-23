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
                args: [
                "init",
                "--workspace",
                directory
                ],
                cancellationToken: CancellationToken.None);

            result.ExitCode.Should().Be(0);
            result.StandardError.Should().BeEmpty();
            result.StandardOutput.Should().Contain($"created: {configurationPath}");

            using var configuration = JsonDocument.Parse(json: await File.ReadAllTextAsync(path: configurationPath));
            var root = configuration.RootElement;
            ReadStringArray(element: root.GetProperty(propertyName: "templates")).Should().Equal("**/*.tst");
            ReadStringArray(element: root.GetProperty(propertyName: "exclude")).Should().Equal("**/bin/**", "**/obj/**", "**/node_modules/**");
            ReadStringArray(element: root.GetProperty(propertyName: "inputExtensions"))
                .Should().Equal(".cs", ".csproj", ".json", ".props", ".sln", ".slnx", ".targets", ".tst");
            root.GetProperty(propertyName: "defaultTargetFramework").ValueKind.Should().Be(JsonValueKind.Null);

            var output = root.GetProperty(propertyName: "output");
            output.GetProperty(propertyName: "newline").GetString().Should().Be("lf");
            output.GetProperty(propertyName: "encoding").GetString().Should().Be("utf-8");
            output.GetProperty(propertyName: "writeOnlyWhenChanged").GetBoolean().Should().BeTrue();
            output.GetProperty(propertyName: "dryRun").GetBoolean().Should().BeFalse();
            output.GetProperty(propertyName: "fileNameConvention").GetString().Should().Be("preserve");
            output.GetProperty(propertyName: "dateLibrary").GetString().Should().Be("legacy");
            output.GetProperty(propertyName: "dateType").GetString().Should().Be("Date");
            output.GetProperty(propertyName: "dateInitializer").GetString().Should().Be("new Date()");
            output.GetProperty(propertyName: "dateOnlyType").GetString().Should().Be("Date");
            output.GetProperty(propertyName: "dateOnlyInitializer").GetString().Should().Be("new Date()");
            output.GetProperty(propertyName: "timeOnlyType").GetString().Should().Be("string");
            output.GetProperty(propertyName: "timeOnlyInitializer").GetString().Should().Be("\"00:00:00\"");
            output.GetProperty(propertyName: "guidType").GetString().Should().Be("string");
            output.GetProperty(propertyName: "guidInitializer").GetString().Should().Be("auto");
            output.GetProperty(propertyName: "decimalType").GetString().Should().Be("number");
            output.GetProperty(propertyName: "decimalInitializer").GetString().Should().Be("auto");

            var diagnostics = root.GetProperty(propertyName: "diagnostics");
            diagnostics.GetProperty(propertyName: "failOnWarning").GetBoolean().Should().BeFalse();
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(directory: directory);
        }
    }

    [Fact]
    public async Task RunAsyncInitWritesSchemaReferenceAsFirstProperty()
    {
        var directory = CreateProjectDirectory();
        try
        {
            var configurationPath = Path.Combine(path1: directory, path2: ConfigurationFileName);

            var result = await RunCliRawAsync(
                args: [
                "init",
                "--workspace",
                directory
                ],
                cancellationToken: CancellationToken.None);

            result.ExitCode.Should().Be(0);

            using var configuration = JsonDocument.Parse(json: await File.ReadAllTextAsync(path: configurationPath));
            var root = configuration.RootElement;
            using var propertyEnumerator = root.EnumerateObject();
            propertyEnumerator.MoveNext().Should().BeTrue();
            var firstProperty = propertyEnumerator.Current;
            firstProperty.Name.Should().Be("$schema");
            firstProperty.Value.GetString().Should().Be("https://raw.githubusercontent.com/AdaskoTheBeAsT/Typewriter/master/typewriter.schema.json");

            var loadedConfiguration = await global::Typewriter.Engine.TypewriterConfigurationLoader.LoadAsync(
                workspacePath: directory,
                projectPath: null,
                cancellationToken: CancellationToken.None);
            loadedConfiguration.Should().BeEquivalentTo(global::Typewriter.Abstractions.TypewriterConfiguration.Default);
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
                args: [
                "init",
                "--workspace",
                directory
                ],
                cancellationToken: CancellationToken.None);

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
                args: [
                "init",
                "--workspace",
                directory,
                "--force"
                ],
                cancellationToken: CancellationToken.None);

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
    public async Task RunAsyncGenerateWithDiffIncludesUnifiedDiffInJsonOutput()
    {
        var directory = CreateProjectDirectory();
        try
        {
            var project = await CreateSimpleProjectAsync(directory: directory);
            var generatedPath = Path.Combine(path1: directory, path2: "generated", path3: "models.ts");

            var firstResult = await RunCliAsync(
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

            firstResult.ExitCode.Should().Be(0);

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
                "json",
                "--diff");

            result.ExitCode.Should().Be(0);
            result.Success.Should().BeTrue(because: result.StandardError);

            var generatedFile = result.GeneratedFiles.Should().ContainSingle().Which;
            generatedFile.Path.Should().Be(generatedPath);
            generatedFile.Changed.Should().BeTrue();
            generatedFile.Diff.Should().NotBeNull();
            generatedFile.Diff.Should().Contain("--- ", because: "the diff should include the old file header");
            generatedFile.Diff.Should().Contain("+++ ", because: "the diff should include the new file header");
            generatedFile.Diff.Should().Contain("@@ ", because: "the diff should include hunk headers");
            generatedFile.Diff.Should().Contain("+  email: string;", because: "the diff should include the newly added property");
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(directory: directory);
        }
    }

    [Fact]
    public async Task RunAsyncGenerateWithDiffIncludesUnifiedDiffInTextOutput()
    {
        var directory = CreateProjectDirectory();
        try
        {
            var project = await CreateSimpleProjectAsync(directory: directory);

            var firstResult = await RunCliRawAsync(
                args: [
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
                "text"
                ],
                cancellationToken: CancellationToken.None);

            firstResult.ExitCode.Should().Be(0);

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

            var result = await RunCliRawAsync(
                args: [
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
                "text",
                "--diff"
                ],
                cancellationToken: CancellationToken.None);

            result.ExitCode.Should().Be(0);
            result.StandardOutput.Should().Contain("updated:", because: "the file should be marked as changed");
            result.StandardOutput.Should().Contain("@@ ", because: "the diff hunk headers should appear in text output");
            result.StandardOutput.Should().Contain("+  email: string;", because: "the new property should appear in the diff");
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(directory: directory);
        }
    }

    [Fact]
    public async Task RunAsyncGenerateWithDiffIncludesLineEndingOnlyChanges()
    {
        var directory = CreateProjectDirectory();
        try
        {
            var project = await CreateSimpleProjectAsync(directory: directory);
            var generatedPath = Path.Combine(path1: directory, path2: "generated", path3: "models.ts");

            var firstResult = await RunCliAsync(
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

            firstResult.ExitCode.Should().Be(0);
            var generatedContent = await File.ReadAllTextAsync(path: generatedPath);
#pragma warning disable SEC0116
            await File.WriteAllTextAsync(
                path: generatedPath,
                contents: generatedContent.Replace(oldValue: "\n", newValue: "\r\n", comparisonType: StringComparison.Ordinal));
#pragma warning restore SEC0116

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
                "json",
                "--dry-run",
                "--diff");

            result.ExitCode.Should().Be(0);
            var generatedFile = result.GeneratedFiles.Should().ContainSingle().Which;
            generatedFile.Changed.Should().BeTrue();
            generatedFile.Diff.Should().NotBeNullOrWhiteSpace();
            generatedFile.Diff.Should().Contain("-// <auto-generated />");
            generatedFile.Diff.Should().Contain("+// <auto-generated />");
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(directory: directory);
        }
    }

    [Fact]
    public async Task RunAsyncGenerateWritesFileOutsideWorkspace()
    {
        var directory = CreateProjectDirectory();
        try
        {
            var backendDirectory = Path.Combine(path1: directory, path2: "backend");
            var generatedPath = Path.Combine(path1: directory, path2: "frontend", path3: "generated", path4: "models.ts");
            var project = await CreateSimpleProjectAsync(
                directory: backendDirectory,
                templateContent: """
                                 // output: ../frontend/generated/models.ts
                                 $Classes[
                                 export interface $Name {
                                 $Properties[
                                   $name: $Type;]
                                 }
                                 ]
                                 """);

            var result = await RunCliAsync(
                "generate",
                "--workspace",
                backendDirectory,
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

            var generatedContent = await File.ReadAllTextAsync(path: generatedPath);
            generatedContent.Should().Contain("export interface Customer");
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
                args: [
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
                "json"
                ],
                cancellationToken: cancellation.Token);

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
                args: [
                "watch",
                "--workspace",
                directory,
                "--project",
                project.ProjectPath,
                "--template",
                project.TemplatePath,
                "--framework",
                "net10.0"
                ],
                cancellationToken: cancellation.Token);

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
    public async Task RunAsyncWatchRegeneratesAfterProjectFileChange()
    {
        var directory = CreateProjectDirectory();
        try
        {
            var project = await CreateSimpleProjectAsync(
                directory: directory,
                sourceContent: """
                               namespace Sample.Models;

                               public sealed class Customer
                               {
                                   public required string Name { get; init; }

                               #if INCLUDE_EMAIL
                                   public required string Email { get; init; }
                               #endif
                               }
                               """);
            var generatedPath = Path.Combine(path1: directory, path2: "generated", path3: "models.ts");
            using var cancellation = new CancellationTokenSource();

            var runTask = RunCliRawAsync(
                args: [
                "watch",
                "--workspace",
                directory,
                "--project",
                project.ProjectPath,
                "--template",
                project.TemplatePath,
                "--framework",
                "net10.0"
                ],
                cancellationToken: cancellation.Token);

            await WaitForFileContentAsync(path: generatedPath, expectedContent: "name: string;", cancellationToken: cancellation.Token);
            (await File.ReadAllTextAsync(path: generatedPath, cancellationToken: cancellation.Token))
                .Should().NotContain("email: string;");
            await File.WriteAllTextAsync(
                path: project.ProjectPath,
                contents: """
                          <Project Sdk="Microsoft.NET.Sdk">
                            <PropertyGroup>
                              <TargetFramework>net10.0</TargetFramework>
                              <ImplicitUsings>enable</ImplicitUsings>
                              <Nullable>enable</Nullable>
                              <DefineConstants>$(DefineConstants);INCLUDE_EMAIL</DefineConstants>
                            </PropertyGroup>
                          </Project>
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
    public async Task RunAsyncWatchRegeneratesAfterDirectoryBuildPropsChange()
    {
        var directory = CreateProjectDirectory();
        try
        {
            var project = await CreateSimpleProjectAsync(
                directory: directory,
                sourceContent: """
                               namespace Sample.Models;

                               public sealed class Customer
                               {
                                   public required string Name { get; init; }

                               #if INCLUDE_EMAIL
                                   public required string Email { get; init; }
                               #endif
                               }
                               """);
            var propsPath = Path.Combine(path1: directory, path2: "Directory.Build.props");
            await File.WriteAllTextAsync(
                path: propsPath,
                contents: """
                          <Project>
                            <PropertyGroup />
                          </Project>
                          """);
            var generatedPath = Path.Combine(path1: directory, path2: "generated", path3: "models.ts");
            using var cancellation = new CancellationTokenSource();

            var runTask = RunCliRawAsync(
                args: [
                "watch",
                "--workspace",
                directory,
                "--project",
                project.ProjectPath,
                "--template",
                project.TemplatePath,
                "--framework",
                "net10.0"
                ],
                cancellationToken: cancellation.Token);

            await WaitForFileContentAsync(path: generatedPath, expectedContent: "name: string;", cancellationToken: cancellation.Token);
            (await File.ReadAllTextAsync(path: generatedPath, cancellationToken: cancellation.Token))
                .Should().NotContain("email: string;");
            await File.WriteAllTextAsync(
                path: propsPath,
                contents: """
                          <Project>
                            <PropertyGroup>
                              <DefineConstants>$(DefineConstants);INCLUDE_EMAIL</DefineConstants>
                            </PropertyGroup>
                          </Project>
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
    public async Task RunAsyncWatchUsesConfiguredInputExtensions()
    {
        var directory = CreateProjectDirectory();
        try
        {
            var project = await CreateSimpleProjectAsync(directory: directory);
            var triggerPath = Path.Combine(path1: directory, path2: "refresh.trigger");
            await File.WriteAllTextAsync(
                path: Path.Combine(path1: directory, path2: ConfigurationFileName),
                contents: """
                          {
                            "inputExtensions": [ "trigger" ]
                          }
                          """);
            var generatedPath = Path.Combine(path1: directory, path2: "generated", path3: "models.ts");
            using var cancellation = new CancellationTokenSource();

            var runTask = RunCliRawAsync(
                args: [
                "watch",
                "--workspace",
                directory,
                "--project",
                project.ProjectPath,
                "--template",
                project.TemplatePath,
                "--framework",
                "net10.0"
                ],
                cancellationToken: cancellation.Token);

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
            await File.WriteAllTextAsync(path: triggerPath, contents: "refresh");
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
    public async Task RunAsyncWatchCoalescesRapidSourceSaves()
    {
        static string CreateSource(string propertyName) =>
            $$"""
              namespace Sample.Models;

              public sealed class Customer
              {
                  public required string Name { get; init; }

                  public required string {{propertyName}} { get; init; }
              }
              """;

        var directory = CreateProjectDirectory();
        try
        {
            var project = await CreateSimpleProjectAsync(directory: directory);
            var generatedPath = Path.Combine(path1: directory, path2: "generated", path3: "models.ts");
            using var cancellation = new CancellationTokenSource();

            var runTask = RunCliRawAsync(
                args: [
                "watch",
                "--workspace",
                directory,
                "--project",
                project.ProjectPath,
                "--template",
                project.TemplatePath,
                "--framework",
                "net10.0"
                ],
                cancellationToken: cancellation.Token);

            await WaitForFileContentAsync(path: generatedPath, expectedContent: "name: string;", cancellationToken: cancellation.Token);
            await Task.Delay(millisecondsDelay: 500, cancellationToken: CancellationToken.None);
            for (var index = 0; index < 10; index++)
            {
                await File.WriteAllTextAsync(
                    path: project.SourcePath,
                    contents: CreateSource(propertyName: "Value" + index.ToString(provider: CultureInfo.InvariantCulture)));
            }

            await WaitForFileContentAsync(path: generatedPath, expectedContent: "value9: string;", cancellationToken: cancellation.Token);
            await Task.Delay(millisecondsDelay: 750, cancellationToken: CancellationToken.None);
            await cancellation.CancelAsync();

            var result = await runTask.WaitAsync(timeout: TimeSpan.FromSeconds(seconds: 30));

            result.ExitCode.Should().Be(0);
            CountOccurrences(text: result.StandardOutput, value: "updated:").Should().BeLessThanOrEqualTo(expected: 2);
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
    public async Task RunAsyncProjectEvaluationFailureReturnsDetailedJsonDiagnostic()
    {
        var directory = CreateProjectDirectory();
        try
        {
            var projectPath = Path.Combine(path1: directory, path2: "Sample.csproj");
            await File.WriteAllTextAsync(
                path: projectPath,
                contents: """
                          <Project Sdk="Microsoft.NET.Sdk">
                            <PropertyGroup>
                              <TargetFramework>net10.0</TargetFramework>
                            </PropertyGroup>
                            <Target Name="FailBeforeCompile" BeforeTargets="CoreCompile">
                              <Error Text="Detailed project evaluation failure." />
                            </Target>
                          </Project>
                          """);
            await File.WriteAllTextAsync(path: Path.Combine(path1: directory, path2: "Model.cs"), contents: "namespace Sample; public sealed class Model { }");
            var templatePath = Path.Combine(path1: directory, path2: "Models.tst");
            await File.WriteAllTextAsync(
                path: templatePath,
                contents: """
                          // output: generated/models.ts
                          $Classes[]
                          """);

            var result = await RunCliAsync(
                "generate",
                "--workspace",
                directory,
                "--project",
                projectPath,
                "--template",
                templatePath,
                "--framework",
                "net10.0",
                "--output",
                "json");

            result.ExitCode.Should().Be(3);
            result.Success.Should().BeFalse();

            var diagnostic = result.Diagnostics.Should().ContainSingle().Which;
            diagnostic.Code.Should().Be("TW0003");
            diagnostic.Severity.Should().Be("error");
            diagnostic.File.Should().Be(projectPath);
            diagnostic.Line.Should().BePositive();
            diagnostic.Message.Should().Contain("Detailed project evaluation failure.");
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
                args: [
                "validate",
                "--workspace",
                directory,
                "--project",
                project.ProjectPath,
                "--template",
                project.TemplatePath,
                "--framework",
                "net10.0"
                ],
                cancellationToken: CancellationToken.None);

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
                args: [
                "generate",
                "--workspace",
                directory,
                "--project",
                project.ProjectPath,
                "--template",
                project.TemplatePath,
                "--framework",
                "net10.0"
                ],
                cancellationToken: CancellationToken.None);

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
        string[] args,
        CancellationToken cancellationToken)
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
        RunCliAsync(args: args, cancellationToken: CancellationToken.None);

    private static async Task<CliRunResult> RunCliAsync(
        string[] args,
        CancellationToken cancellationToken)
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
                    Changed: file.GetProperty(propertyName: "changed").GetBoolean(),
                    Diff: file.TryGetProperty(propertyName: "diff", out var diffElement) && diffElement.ValueKind == JsonValueKind.String
                        ? diffElement.GetString()
                        : null));
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
                    File: diagnostic.GetProperty(propertyName: "file").GetString(),
                    Line: ReadNullableInt(element: diagnostic, propertyName: "line"),
                    Column: ReadNullableInt(element: diagnostic, propertyName: "column"),
                    Severity: diagnostic.GetProperty(propertyName: "severity").GetString() ?? string.Empty,
                    Message: diagnostic.GetProperty(propertyName: "message").GetString() ?? string.Empty,
                    Code: diagnostic.GetProperty(propertyName: "code").GetString(),
                    HelpLink: diagnostic.GetProperty(propertyName: "helpLink").GetString()));
        }

        return diagnostics;
    }

    private static int? ReadNullableInt(
        JsonElement element,
        string propertyName)
    {
        var property = element.GetProperty(propertyName: propertyName);
        return property.ValueKind == JsonValueKind.Null
            ? null
            : property.GetInt32();
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

    private static int CountOccurrences(
        string text,
        string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value: value, startIndex: index, comparisonType: StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
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
        string? templateContent = null,
        string? sourceContent = null)
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
            contents: sourceContent
                      ?? """
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

    private sealed record CliGeneratedFile(string Path, bool Changed, string? Diff = null);

    private sealed record CliDiagnostic(
        string? File,
        int? Line,
        int? Column,
        string Severity,
        string Message,
        string? Code,
        string? HelpLink);
}
