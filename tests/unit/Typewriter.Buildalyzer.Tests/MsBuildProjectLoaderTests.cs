using System.Globalization;
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
    public async Task LoadAsyncReportsBuildErrorDiagnosticsWhenCompilerInputsAreUnavailable()
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

            var loader = new MsBuildProjectLoader();

            var result = await loader.LoadAsync(
                project: new ProjectContext(ProjectPath: projectPath, WorkspacePath: directory, TargetFramework: "net10.0"),
                cancellationToken: CancellationToken.None);

            var diagnostic = result.Diagnostics.Should().ContainSingle().Which;
            diagnostic.Code.Should().Be("TW0003");
            diagnostic.Severity.Should().Be(DiagnosticSeverity.Error);
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

    [Fact]
    public async Task LoadAsyncIncludesGeneratedAnalyzerConfigFiles()
    {
        var directory = CreateProjectDirectory();
        try
        {
            var projectPath = Path.Combine(path1: directory, path2: "WebSample.csproj");
            await File.WriteAllTextAsync(
                path: projectPath,
                contents: """
                          <Project Sdk="Microsoft.NET.Sdk.Web">
                            <PropertyGroup>
                              <TargetFramework>net10.0</TargetFramework>
                            </PropertyGroup>
                          </Project>
                          """);
            await File.WriteAllTextAsync(path: Path.Combine(path1: directory, path2: "Model.cs"), contents: "namespace WebSample; public sealed class Model { }");

            var loader = new MsBuildProjectLoader();

            var result = await loader.LoadAsync(
                project: new ProjectContext(ProjectPath: projectPath, WorkspacePath: directory, TargetFramework: "net10.0"),
                cancellationToken: CancellationToken.None);

            result.Diagnostics.Should().BeEmpty();
            var analyzerConfigFile = result.AnalyzerConfigFiles.Should()
                .ContainSingle(path => path.EndsWith(value: ".GeneratedMSBuildEditorConfig.editorconfig", comparisonType: StringComparison.OrdinalIgnoreCase))
                .Which;
            var analyzerConfig = await File.ReadAllTextAsync(path: analyzerConfigFile);
            analyzerConfig.Should().Contain("build_property.RazorLangVersion");
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(directory: directory);
        }
    }

    [Fact]
    public async Task LoadAsyncParsesOldStyleNonSdkProject()
    {
        var directory = CreateProjectDirectory();
        try
        {
            var projectPath = Path.Combine(path1: directory, path2: "LegacyApp.csproj");
            await File.WriteAllTextAsync(
                path: projectPath,
                contents: """
                          <?xml version="1.0" encoding="utf-8"?>
                          <Project ToolsVersion="4.0" DefaultTargets="Build" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
                            <Import Project="$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props" Condition="Exists('$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props')" />
                            <PropertyGroup>
                              <Configuration Condition=" '$(Configuration)' == '' ">Debug</Configuration>
                              <Platform Condition=" '$(Platform)' == '' ">AnyCPU</Platform>
                              <ProjectGuid>{9C1C4E52-9E63-4F1B-8B7B-6E7B2C6E7B2C}</ProjectGuid>
                              <OutputType>Library</OutputType>
                              <RootNamespace>LegacyApp</RootNamespace>
                              <AssemblyName>LegacyApp</AssemblyName>
                              <TargetFrameworkVersion>v4.8</TargetFrameworkVersion>
                            </PropertyGroup>
                            <PropertyGroup Condition=" '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' ">
                              <DebugSymbols>true</DebugSymbols>
                              <DebugType>full</DebugType>
                              <Optimize>false</Optimize>
                              <OutputPath>bin\Debug\</OutputPath>
                              <DefineConstants>DEBUG;TRACE;LEGACY_SYMBOL</DefineConstants>
                              <ErrorReport>prompt</ErrorReport>
                              <WarningLevel>4</WarningLevel>
                            </PropertyGroup>
                            <ItemGroup>
                              <Reference Include="System" />
                              <Reference Include="System.Core" />
                            </ItemGroup>
                            <ItemGroup>
                              <Compile Include="Model.cs" />
                            </ItemGroup>
                            <Import Project="$(MSBuildToolsPath)\Microsoft.CSharp.targets" />
                          </Project>
                          """);
            await File.WriteAllTextAsync(path: Path.Combine(path1: directory, path2: "Model.cs"), contents: "namespace LegacyApp { public sealed class Model { public int Id { get; set; } } }");

            var loader = new MsBuildProjectLoader();

            var result = await loader.LoadAsync(
                project: new ProjectContext(ProjectPath: projectPath, WorkspacePath: directory, TargetFramework: null),
                cancellationToken: CancellationToken.None);

            result.Diagnostics.Should().BeEmpty(because: FormatDiagnostics(diagnostics: result.Diagnostics));
            result.PreprocessorSymbols.Should().Contain("LEGACY_SYMBOL");
            result.SourceFiles.Should().Contain(path => path.EndsWith(value: "Model.cs", comparisonType: StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(directory: directory);
        }
    }

    [Fact]
    public async Task LoadAsyncReportsUnresolvedImportErrorForOldStyleProjectWithMissingUnconditionalImport()
    {
        // Regression test for https://github.com/AdaskoTheBeAsT/Typewriter/issues/69
        // Old-style, non-SDK projects (typically legacy ASP.NET/web application projects) sometimes
        // unconditionally import a sibling project file (for example a shared "Classic.Web.csproj").
        // When that import cannot be resolved, Buildalyzer can still report the overall result as
        // "succeeded" while returning zero source files, which previously caused Typewriter to silently
        // treat the project as empty instead of surfacing the real MSBuild error.
        var directory = CreateProjectDirectory();
        try
        {
            var projectPath = Path.Combine(path1: directory, path2: "MyProject.csproj");
            await File.WriteAllTextAsync(
                path: projectPath,
                contents: """
                          <?xml version="1.0" encoding="utf-8"?>
                          <Project ToolsVersion="Current" DefaultTargets="Build" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
                            <Import Project="..\Shared\Classic.Web.csproj" />
                            <Import Project="$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props" Condition="Exists('$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props')" />
                            <PropertyGroup>
                              <OutputType>Library</OutputType>
                              <RootNamespace>MyProject</RootNamespace>
                              <AssemblyName>MyProject</AssemblyName>
                              <TargetFrameworkVersion>v4.8</TargetFrameworkVersion>
                            </PropertyGroup>
                            <ItemGroup>
                              <Compile Include="Model.cs" />
                            </ItemGroup>
                            <Import Project="$(MSBuildToolsPath)\Microsoft.CSharp.targets" />
                          </Project>
                          """);
            await File.WriteAllTextAsync(path: Path.Combine(path1: directory, path2: "Model.cs"), contents: "namespace MyProject { public class Model { } }");

            var loader = new MsBuildProjectLoader();

            var result = await loader.LoadAsync(
                project: new ProjectContext(ProjectPath: projectPath, WorkspacePath: directory, TargetFramework: null),
                cancellationToken: CancellationToken.None);

            var diagnostic = result.Diagnostics.Should().ContainSingle().Which;
            diagnostic.Code.Should().Be("TW0003");
            diagnostic.Severity.Should().Be(DiagnosticSeverity.Error);
            diagnostic.Message.Should().Contain("Classic.Web.csproj");
            result.SourceFiles.Should().BeEmpty();
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(directory: directory);
        }
    }

    private static string FormatDiagnostics(IEnumerable<GenerationDiagnostic> diagnostics) =>
        string.Join(
            separator: Environment.NewLine,
            values: diagnostics.Select(
                selector: diagnostic =>
                    string.Create(
                        provider: CultureInfo.InvariantCulture,
                        handler: $"{diagnostic.Severity} {diagnostic.Code}: {diagnostic.Message} ({diagnostic.File}:{diagnostic.Line})")));

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
