using Typewriter.Abstractions;
using Xunit;

namespace Typewriter.Buildalyzer.Tests;

public sealed class MsBuildProjectLoaderTests
{
    [Fact]
    public async Task LoadAsyncParsesSdkProjectInputs()
    {
        var directory = CreateProjectDirectory();
        try
        {
            var referencedDirectory = Path.Combine(path1: directory, path2: "Referenced");
            Directory.CreateDirectory(path: referencedDirectory);
            var referencedProjectPath = Path.Combine(path1: referencedDirectory, path2: "Referenced.csproj");
            await File.WriteAllTextAsync(
                path: referencedProjectPath,
                contents: """
                          <Project Sdk="Microsoft.NET.Sdk">
                            <PropertyGroup>
                              <TargetFramework>net10.0</TargetFramework>
                            </PropertyGroup>
                          </Project>
                          """);
            await File.WriteAllTextAsync(path: Path.Combine(path1: referencedDirectory, path2: "ReferencedModel.cs"), contents: "namespace Referenced; public sealed class ReferencedModel { }");

            var projectPath = Path.Combine(path1: directory, path2: "Sample.csproj");
            await File.WriteAllTextAsync(
                path: projectPath,
                contents: """
                          <Project Sdk="Microsoft.NET.Sdk">
                            <PropertyGroup>
                              <TargetFrameworks>net8.0;net10.0</TargetFrameworks>
                              <Nullable>enable</Nullable>
                              <ImplicitUsings>enable</ImplicitUsings>
                              <DefineConstants>LOCAL_SYMBOL</DefineConstants>
                            </PropertyGroup>
                            <ItemGroup>
                              <Compile Remove="Excluded.cs" />
                              <ProjectReference Include="Referenced/Referenced.csproj" />
                            </ItemGroup>
                          </Project>
                          """);
            await File.WriteAllTextAsync(path: Path.Combine(path1: directory, path2: "Model.cs"), contents: "namespace Sample; public sealed class Model { }");
            await File.WriteAllTextAsync(path: Path.Combine(path1: directory, path2: "Excluded.cs"), contents: "namespace Sample; public sealed class Excluded { }");

            var loader = new MsBuildProjectLoader();

            var result = await loader.LoadAsync(
                project: new ProjectContext(ProjectPath: projectPath, WorkspacePath: directory, TargetFramework: "net10.0"),
                cancellationToken: CancellationToken.None);

            Assert.Empty(collection: result.Diagnostics);
            Assert.Equal(expected: "net10.0", actual: result.TargetFramework);
            Assert.True(condition: result.NullableEnabled);
            Assert.True(condition: result.ImplicitUsingsEnabled);
            Assert.Contains(expected: "LOCAL_SYMBOL", collection: result.PreprocessorSymbols);
            Assert.Contains(expected: "NET10_0", collection: result.PreprocessorSymbols);
            Assert.Contains(expected: "System.Collections.Generic", collection: result.GlobalUsings);
            Assert.Contains(collection: result.SourceFiles, filter: path => path.EndsWith(value: "Model.cs", comparisonType: StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(collection: result.SourceFiles, filter: path => path.EndsWith(value: "Excluded.cs", comparisonType: StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(collection: result.SourceFiles, filter: path => path.EndsWith(value: "ReferencedModel.cs", comparisonType: StringComparison.OrdinalIgnoreCase));
            Assert.Contains(expected: referencedProjectPath, collection: result.ProjectReferences);
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(directory: directory);
        }
    }

    [Fact]
    public async Task LoadAsyncSetsApiCompatGenerateSuppressionFile()
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
                          </Project>
                          """);
            await File.WriteAllTextAsync(path: Path.Combine(path1: directory, path2: "Model.cs"), contents: "namespace Sample; public sealed class Model { }");

            var loader = new MsBuildProjectLoader();

            var result = await loader.LoadAsync(
                project: new ProjectContext(ProjectPath: projectPath, WorkspacePath: directory, TargetFramework: "net10.0"),
                cancellationToken: CancellationToken.None);

            Assert.Empty(collection: result.Diagnostics);
            Assert.Contains(collection: result.SourceFiles, filter: path => path.EndsWith(value: "Model.cs", comparisonType: StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(directory: directory);
        }
    }

    [Fact]
    public async Task LoadAsyncKeepsCurrentProjectSourcesWhenProjectReferenceSharesDirectory()
    {
        var directory = CreateProjectDirectory();
        try
        {
            var referencedProjectPath = Path.Combine(path1: directory, path2: "Referenced.csproj");
            await File.WriteAllTextAsync(
                path: referencedProjectPath,
                contents: """
                          <Project Sdk="Microsoft.NET.Sdk">
                            <PropertyGroup>
                              <TargetFramework>net10.0</TargetFramework>
                              <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
                              <BaseIntermediateOutputPath>obj\Referenced\</BaseIntermediateOutputPath>
                            </PropertyGroup>
                            <ItemGroup>
                              <Compile Include="ReferencedModel.cs" />
                            </ItemGroup>
                          </Project>
                          """);
            await File.WriteAllTextAsync(path: Path.Combine(path1: directory, path2: "ReferencedModel.cs"), contents: "namespace Referenced; public sealed class ReferencedModel { }");

            var projectPath = Path.Combine(path1: directory, path2: "Sample.csproj");
            await File.WriteAllTextAsync(
                path: projectPath,
                contents: """
                          <Project Sdk="Microsoft.NET.Sdk">
                            <PropertyGroup>
                              <TargetFramework>net10.0</TargetFramework>
                              <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
                              <BaseIntermediateOutputPath>obj\Sample\</BaseIntermediateOutputPath>
                            </PropertyGroup>
                            <ItemGroup>
                              <Compile Include="Model.cs" />
                              <ProjectReference Include="Referenced.csproj" />
                            </ItemGroup>
                          </Project>
                          """);
            await File.WriteAllTextAsync(path: Path.Combine(path1: directory, path2: "Model.cs"), contents: "namespace Sample; public sealed class Model { }");

            var loader = new MsBuildProjectLoader();

            var result = await loader.LoadAsync(
                project: new ProjectContext(ProjectPath: projectPath, WorkspacePath: directory, TargetFramework: "net10.0"),
                cancellationToken: CancellationToken.None);

            Assert.Empty(collection: result.Diagnostics);
            Assert.Contains(collection: result.SourceFiles, filter: path => path.EndsWith(value: "Model.cs", comparisonType: StringComparison.OrdinalIgnoreCase));
            Assert.Contains(expected: referencedProjectPath, collection: result.ProjectReferences);
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(directory: directory);
        }
    }

    [Fact]
    public async Task LoadAsyncUsesSolutionPropertiesFromSlnxWorkspace()
    {
        var directory = CreateProjectDirectory();
        try
        {
            var projectDirectory = Path.Combine(path1: directory, path2: "src", path3: "Sample");
            Directory.CreateDirectory(path: projectDirectory);

            var solutionPath = Path.Combine(path1: directory, path2: "Sample.slnx");
            var projectPath = Path.Combine(path1: projectDirectory, path2: "Sample.csproj");
            var sharedSourcePath = Path.Combine(path1: directory, path2: "Shared.cs");

            await File.WriteAllTextAsync(
                path: solutionPath,
                contents: """
                          <Solution>
                            <Project Path="src/Sample/Sample.csproj" />
                          </Solution>
                          """);
            await File.WriteAllTextAsync(
                path: projectPath,
                contents: """
                          <Project Sdk="Microsoft.NET.Sdk">
                            <PropertyGroup>
                              <TargetFramework>net10.0</TargetFramework>
                            </PropertyGroup>
                            <ItemGroup>
                              <Compile Include="$(SolutionDir)Shared.cs" Link="Shared.cs" />
                            </ItemGroup>
                          </Project>
                          """);
            await File.WriteAllTextAsync(path: sharedSourcePath, contents: "namespace Sample; public sealed class Shared { }");

            var loader = new MsBuildProjectLoader();

            var result = await loader.LoadAsync(
                project: new ProjectContext(ProjectPath: projectPath, WorkspacePath: solutionPath, TargetFramework: "net10.0"),
                cancellationToken: CancellationToken.None);

            Assert.Empty(collection: result.Diagnostics);
            Assert.Contains(expected: sharedSourcePath, collection: result.SourceFiles);
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
            path2: "Typewriter.Buildalyzer.Tests",
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
