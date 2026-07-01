using Xunit;

namespace Typewriter.LanguageServer.Tests;

public sealed class WorkspaceGenerationServiceTests
{
    [Fact]
    public async Task GenerateAsyncWritesOutputAndReusesServiceForSubsequentRequests()
    {
        var directory = CreateProjectDirectory();
        try
        {
            var projectPath = Path.Combine(path1: directory, path2: "App.csproj");
            var templatePath = Path.Combine(path1: directory, path2: "Models.tst");
            var outputPath = Path.Combine(path1: directory, path2: "generated", path3: "models.ts");
            await File.WriteAllTextAsync(
                path: projectPath,
                contents: """
                    <Project Sdk="Microsoft.NET.Sdk">
                      <PropertyGroup>
                        <TargetFramework>net10.0</TargetFramework>
                      </PropertyGroup>
                    </Project>
                    """);
            await File.WriteAllTextAsync(
                path: Path.Combine(path1: directory, path2: "Model.cs"),
                contents: "namespace App; public sealed class Model { public string Name { get; set; } = string.Empty; }");
            await File.WriteAllTextAsync(
                path: templatePath,
                contents: """
                    // output: generated/models.ts
                    $Classes[export interface $Name {
                    $Properties[  $name: $Type;
                    ]}
                    ]
                    """);

            using var service = new WorkspaceGenerationService();
            var request = new WorkspaceGenerationRequest(
                Command: "generate",
                WorkspacePath: directory,
                ProjectPath: projectPath,
                TemplatePath: templatePath,
                Framework: null,
                AllProjects: false);
            var settings = LanguageServerSettings.Default;

            var first = await service.GenerateAsync(request: request, settings: settings, cancellationToken: CancellationToken.None);
            var second = await service.GenerateAsync(request: request, settings: settings, cancellationToken: CancellationToken.None);

            first.Success.Should().BeTrue();
            first.GeneratedFiles.Should().ContainSingle().Which.Changed.Should().BeTrue();
            second.Success.Should().BeTrue();
            second.GeneratedFiles.Should().ContainSingle().Which.Changed.Should().BeFalse();
            File.Exists(path: outputPath).Should().BeTrue();
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(directory: directory);
        }
    }

    [Fact]
    public async Task GenerateAsyncRefreshesOutputAfterSourceFileChanges()
    {
        var directory = CreateProjectDirectory();
        try
        {
            var projectPath = Path.Combine(path1: directory, path2: "App.csproj");
            var sourcePath = Path.Combine(path1: directory, path2: "Model.cs");
            var outputPath = Path.Combine(path1: directory, path2: "generated", path3: "models.ts");
            await File.WriteAllTextAsync(
                path: projectPath,
                contents: """
                    <Project Sdk="Microsoft.NET.Sdk">
                      <PropertyGroup>
                        <TargetFramework>net10.0</TargetFramework>
                      </PropertyGroup>
                    </Project>
                    """);
            await File.WriteAllTextAsync(
                path: sourcePath,
                contents: "namespace App; public sealed class Model { public string Name { get; set; } = string.Empty; }");
            await File.WriteAllTextAsync(
                path: Path.Combine(path1: directory, path2: "Models.tst"),
                contents: """
                    // output: generated/models.ts
                    $Classes[export interface $Name {
                    $Properties[  $name: $Type;
                    ]}]
                    """);

            using var service = new WorkspaceGenerationService();
            var request = new WorkspaceGenerationRequest(
                Command: "generate",
                WorkspacePath: directory,
                ProjectPath: projectPath,
                TemplatePath: null,
                TemplateSearchPath: directory,
                Framework: null,
                AllProjects: false);
            var settings = LanguageServerSettings.Default;

            var first = await service.GenerateAsync(request: request, settings: settings, cancellationToken: CancellationToken.None);
            await File.WriteAllTextAsync(
                path: sourcePath,
                contents: "namespace App; public sealed class Model { public string Name { get; set; } = string.Empty; public int Age { get; set; } }");
            var second = await service.GenerateAsync(request: request, settings: settings, cancellationToken: CancellationToken.None);

            first.Success.Should().BeTrue();
            second.Success.Should().BeTrue();
            second.GeneratedFiles.Should().ContainSingle().Which.Changed.Should().BeTrue();
            var output = await File.ReadAllTextAsync(path: outputPath);
            output.Should().Contain("  name: string;");
            output.Should().Contain("  age: number;");
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(directory: directory);
        }
    }

    [Fact]
    public async Task GenerateAsyncPreservesDetailedProjectLoadDiagnostics()
    {
        var directory = CreateProjectDirectory();
        try
        {
            var projectPath = Path.Combine(path1: directory, path2: "App.csproj");
            var templatePath = Path.Combine(path1: directory, path2: "Models.tst");
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
            await File.WriteAllTextAsync(path: Path.Combine(path1: directory, path2: "Model.cs"), contents: "namespace App; public sealed class Model { }");
            await File.WriteAllTextAsync(
                path: templatePath,
                contents: """
                    // output: generated/models.ts
                    $Classes[]
                    """);

            using var service = new WorkspaceGenerationService();
            var request = new WorkspaceGenerationRequest(
                Command: "generate",
                WorkspacePath: directory,
                ProjectPath: projectPath,
                TemplatePath: templatePath,
                Framework: null,
                AllProjects: false);

            var result = await service.GenerateAsync(request: request, settings: LanguageServerSettings.Default, cancellationToken: CancellationToken.None);

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
                await Task.Delay(millisecondsDelay: 100);
            }
            catch (UnauthorizedAccessException) when (attempt < MaxAttempts)
            {
                await Task.Delay(millisecondsDelay: 100);
            }
        }
    }
}
