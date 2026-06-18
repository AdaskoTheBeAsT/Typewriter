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

            result.Diagnostics.Should().BeEmpty();
            result.TargetFramework.Should().Be("net10.0");
            result.NullableEnabled.Should().BeTrue();
            result.ImplicitUsingsEnabled.Should().BeTrue();
            result.PreprocessorSymbols.Should().Contain("LOCAL_SYMBOL");
            result.PreprocessorSymbols.Should().Contain("NET10_0");
            result.GlobalUsings.Should().Contain("System.Collections.Generic");
            result.SourceFiles.Should().Contain(path => path.EndsWith(value: "Model.cs", comparisonType: StringComparison.OrdinalIgnoreCase));
            result.SourceFiles.Should().NotContain(path => path.EndsWith(value: "Excluded.cs", comparisonType: StringComparison.OrdinalIgnoreCase));
            result.SourceFiles.Should().NotContain(path => path.EndsWith(value: "ReferencedModel.cs", comparisonType: StringComparison.OrdinalIgnoreCase));
            result.ProjectReferences.Should().Contain(referencedProjectPath);
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

            result.Diagnostics.Should().BeEmpty();
            result.SourceFiles.Should().Contain(path => path.EndsWith(value: "Model.cs", comparisonType: StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(directory: directory);
        }
    }

    [Fact]
    public async Task LoadAsyncParsesSdkProjectWithLocalizedResources()
    {
        var directory = CreateProjectDirectory();
        try
        {
            var projectPath = Path.Combine(path1: directory, path2: "App.csproj");
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
            await File.WriteAllTextAsync(path: Path.Combine(path1: directory, path2: "MyModel.cs"), contents: "namespace App; public sealed class MyModel { public int Id { get; set; } }");
            await File.WriteAllTextAsync(path: Path.Combine(path1: directory, path2: "Strings.resx"), contents: CreateResxContent(value: "Hello"));
            await File.WriteAllTextAsync(path: Path.Combine(path1: directory, path2: "Strings.de.resx"), contents: CreateResxContent(value: "Hallo"));

            var loader = new MsBuildProjectLoader();

            var result = await loader.LoadAsync(
                project: new ProjectContext(ProjectPath: projectPath, WorkspacePath: directory, TargetFramework: "net10.0"),
                cancellationToken: CancellationToken.None);

            result.Diagnostics.Should().BeEmpty();
            result.SourceFiles.Should().Contain(path => path.EndsWith(value: "MyModel.cs", comparisonType: StringComparison.OrdinalIgnoreCase));
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
            await File.WriteAllTextAsync(
                path: Path.Combine(path1: directory, path2: "Directory.Build.props"),
                contents: """
                          <Project>
                            <PropertyGroup Condition="'$(MSBuildProjectName)' == 'Referenced'">
                              <BaseIntermediateOutputPath>obj\Referenced\</BaseIntermediateOutputPath>
                              <BaseOutputPath>bin\Referenced\</BaseOutputPath>
                            </PropertyGroup>
                            <PropertyGroup Condition="'$(MSBuildProjectName)' == 'Sample'">
                              <BaseIntermediateOutputPath>obj\Sample\</BaseIntermediateOutputPath>
                              <BaseOutputPath>bin\Sample\</BaseOutputPath>
                            </PropertyGroup>
                          </Project>
                          """);
            var referencedProjectPath = Path.Combine(path1: directory, path2: "Referenced.csproj");
            await File.WriteAllTextAsync(
                path: referencedProjectPath,
                contents: """
                          <Project Sdk="Microsoft.NET.Sdk">
                            <PropertyGroup>
                              <TargetFramework>net10.0</TargetFramework>
                              <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
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

            result.Diagnostics.Should().BeEmpty();
            result.SourceFiles.Should().Contain(path => path.EndsWith(value: "Model.cs", comparisonType: StringComparison.OrdinalIgnoreCase));
            result.ProjectReferences.Should().Contain(referencedProjectPath);
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(directory: directory);
        }
    }

    [Fact]
    public async Task LoadAsyncUsesCompilerInputsWhenDesignTimeTargetFails()
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
                              <DesignTimeBuild>true</DesignTimeBuild>
                            </PropertyGroup>
                            <Target Name="FailLegacyDesignTimeTarget" AfterTargets="CoreCompile" Condition=" '$(DesignTimeBuild)' == 'true' ">
                              <Error Text="Simulated legacy design-time target failure." />
                            </Target>
                          </Project>
                          """);
            await File.WriteAllTextAsync(path: Path.Combine(path1: directory, path2: "Model.cs"), contents: "namespace Sample; public sealed class Model { }");

            var loader = new MsBuildProjectLoader();

            var result = await loader.LoadAsync(
                project: new ProjectContext(ProjectPath: projectPath, WorkspacePath: directory, TargetFramework: "net10.0"),
                cancellationToken: CancellationToken.None);

            result.Diagnostics.Should().BeEmpty();
            result.SourceFiles.Should().Contain(path => path.EndsWith(value: "Model.cs", comparisonType: StringComparison.OrdinalIgnoreCase));
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

            result.Diagnostics.Should().BeEmpty();
            result.SourceFiles.Should().Contain(sharedSourcePath);
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

    private static string CreateResxContent(string value) =>
        $"""
         <?xml version="1.0" encoding="utf-8"?>
         <root>
           <resheader name="resmimetype">
             <value>text/microsoft-resx</value>
           </resheader>
           <resheader name="version">
             <value>2.0</value>
           </resheader>
           <resheader name="reader">
             <value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
           </resheader>
           <resheader name="writer">
             <value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
           </resheader>
           <data name="Greeting" xml:space="preserve">
             <value>{value}</value>
           </data>
         </root>
         """;

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
