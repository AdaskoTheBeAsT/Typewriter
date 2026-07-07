using Typewriter.Abstractions;
using Typewriter.Engine;
using Xunit;

namespace Typewriter.Engine.Tests;

public sealed class TypewriterGeneratorIncludeProjectTests
{
    [Fact]
    public async Task GenerateAsyncMergesTypesFromIncludedProjectIntoCodeModel()
    {
        var directory = CreateProjectDirectory();
        try
        {
            var appProjectPath = Path.Combine(path1: directory, path2: "App", path3: "App.csproj");
            var contractsProjectPath = Path.Combine(path1: directory, path2: "Contracts", path3: "Contracts.csproj");
            Directory.CreateDirectory(path: Path.GetDirectoryName(path: appProjectPath)!);
            Directory.CreateDirectory(path: Path.GetDirectoryName(path: contractsProjectPath)!);
            await File.WriteAllTextAsync(path: appProjectPath, contents: "<Project />");
            await File.WriteAllTextAsync(path: contractsProjectPath, contents: "<Project />");

            var localModel = CreateClassMetadata(name: "LocalDto");
            var sharedModel = CreateClassMetadata(name: "SharedDto");
            var metadataProvider = new ProjectMappedMetadataProvider(
                metadataByProjectPath: new Dictionary<string, ProjectMetadata>(comparer: StringComparer.OrdinalIgnoreCase)
                {
                    [key: appProjectPath] = new ProjectMetadata(
                        ProjectPath: appProjectPath,
                        SourceFiles: [new SourceFileMetadata(Path: Path.Combine(path1: directory, path2: "App", path3: "LocalDto.cs"), Types: [localModel])],
                        Types: [localModel],
                        Diagnostics: []),
                    [key: contractsProjectPath] = new ProjectMetadata(
                        ProjectPath: contractsProjectPath,
                        SourceFiles: [new SourceFileMetadata(Path: Path.Combine(path1: directory, path2: "Contracts", path3: "SharedDto.cs"), Types: [sharedModel])],
                        Types: [sharedModel],
                        Diagnostics: []),
                });

            var templatePath = Path.Combine(path1: directory, path2: "App", path3: "Models.tst");
            var generator = new TypewriterGenerator(
                templateDiscovery: new StaticTemplateDiscovery(
                    templates: new TemplateFile(
                        Path: templatePath,
                        Content: """
                                 ${
                                     Template(Settings settings)
                                     {
                                         settings.IncludeProject("Contracts");
                                     }
                                 }
                                 $Classes[
                                 export class $Name {
                                 }
                                 ]
                                 """)),
                metadataProvider: metadataProvider,
                fileWriter: new PassthroughFileWriter());

            var result = await generator.GenerateAsync(
                request: new GenerationRequest(
                    WorkspacePath: directory,
                    ProjectPath: appProjectPath,
                    TemplatePath: templatePath,
                    Mode: GenerationMode.Generate,
                    Configuration: TypewriterConfiguration.Default),
                cancellationToken: CancellationToken.None);

            result.Success.Should().BeTrue(because: string.Join(separator: Environment.NewLine, values: result.Diagnostics.Select(selector: diagnostic => diagnostic.Message)));
            metadataProvider.Projects.Select(selector: project => project.ProjectPath).Should().Contain(contractsProjectPath);
            result.GeneratedFiles.Select(selector: file => file.Path).Order(comparer: StringComparer.OrdinalIgnoreCase).Should().Equal(
                Path.Combine(path1: directory, path2: "App", path3: "LocalDto.ts"),
                Path.Combine(path1: directory, path2: "App", path3: "SharedDto.ts"));
            result.GeneratedFiles.Should().Contain(
                file => file.Path.EndsWith(value: "SharedDto.ts", comparisonType: StringComparison.OrdinalIgnoreCase)
                        && file.Content.Contains(value: "export class SharedDto", comparisonType: StringComparison.Ordinal));
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(directory: directory);
        }
    }

    [Fact]
    public async Task GenerateAsyncReportsWarningWhenIncludedProjectIsNotFoundInWorkspace()
    {
        var directory = CreateProjectDirectory();
        try
        {
            var appProjectPath = Path.Combine(path1: directory, path2: "App", path3: "App.csproj");
            Directory.CreateDirectory(path: Path.GetDirectoryName(path: appProjectPath)!);
            await File.WriteAllTextAsync(path: appProjectPath, contents: "<Project />");

            var localModel = CreateClassMetadata(name: "LocalDto");
            var metadataProvider = new ProjectMappedMetadataProvider(
                metadataByProjectPath: new Dictionary<string, ProjectMetadata>(comparer: StringComparer.OrdinalIgnoreCase)
                {
                    [key: appProjectPath] = new ProjectMetadata(
                        ProjectPath: appProjectPath,
                        SourceFiles: [new SourceFileMetadata(Path: Path.Combine(path1: directory, path2: "App", path3: "LocalDto.cs"), Types: [localModel])],
                        Types: [localModel],
                        Diagnostics: []),
                });

            var templatePath = Path.Combine(path1: directory, path2: "App", path3: "Models.tst");
            var generator = new TypewriterGenerator(
                templateDiscovery: new StaticTemplateDiscovery(
                    templates: new TemplateFile(
                        Path: templatePath,
                        Content: """
                                 ${
                                     Template(Settings settings)
                                     {
                                         settings.IncludeProject("Missing");
                                         settings.IncludeProject("Missing");
                                     }
                                 }
                                 $Classes[
                                 export class $Name {
                                 }
                                 ]
                                 """)),
                metadataProvider: metadataProvider,
                fileWriter: new PassthroughFileWriter());

            var result = await generator.GenerateAsync(
                request: new GenerationRequest(
                    WorkspacePath: directory,
                    ProjectPath: appProjectPath,
                    TemplatePath: templatePath,
                    Mode: GenerationMode.Generate,
                    Configuration: TypewriterConfiguration.Default),
                cancellationToken: CancellationToken.None);

            result.Success.Should().BeTrue(because: string.Join(separator: Environment.NewLine, values: result.Diagnostics.Select(selector: diagnostic => diagnostic.Message)));
            var diagnostic = result.Diagnostics.Should().ContainSingle(predicate: candidate => candidate.Code == "TW0009").Which;
            diagnostic.Severity.Should().Be(DiagnosticSeverity.Warning);
            diagnostic.Message.Should().Contain("Missing");
            result.GeneratedFiles.Should().ContainSingle()
                .Which.Path.Should().Be(Path.Combine(path1: directory, path2: "App", path3: "LocalDto.ts"));
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(directory: directory);
        }
    }

    [Fact]
    public async Task GenerateAsyncReportsWarningWhenIncludedProjectPathIsOutsideWorkspace()
    {
        var directory = CreateProjectDirectory();
        try
        {
            var workspaceDirectory = Path.Combine(path1: directory, path2: "Workspace");
            var appProjectPath = Path.Combine(path1: workspaceDirectory, path2: "App", path3: "App.csproj");
            var externalProjectPath = Path.Combine(path1: directory, path2: "External", path3: "External.csproj");
            Directory.CreateDirectory(path: Path.GetDirectoryName(path: appProjectPath)!);
            Directory.CreateDirectory(path: Path.GetDirectoryName(path: externalProjectPath)!);
            await File.WriteAllTextAsync(path: appProjectPath, contents: "<Project />");
            await File.WriteAllTextAsync(path: externalProjectPath, contents: "<Project />");

            var localModel = CreateClassMetadata(name: "LocalDto");
            var metadataProvider = new ProjectMappedMetadataProvider(
                metadataByProjectPath: new Dictionary<string, ProjectMetadata>(comparer: StringComparer.OrdinalIgnoreCase)
                {
                    [key: appProjectPath] = new ProjectMetadata(
                        ProjectPath: appProjectPath,
                        SourceFiles: [new SourceFileMetadata(Path: Path.Combine(path1: workspaceDirectory, path2: "App", path3: "LocalDto.cs"), Types: [localModel])],
                        Types: [localModel],
                        Diagnostics: []),
                });

            var templatePath = Path.Combine(path1: workspaceDirectory, path2: "App", path3: "Models.tst");
            var generator = new TypewriterGenerator(
                templateDiscovery: new StaticTemplateDiscovery(
                    templates: new TemplateFile(
                        Path: templatePath,
                        Content: """
                                 ${
                                     Template(Settings settings)
                                     {
                                         settings.IncludeProject("../External/External.csproj");
                                     }
                                 }
                                 $Classes[
                                 export class $Name {
                                 }
                                 ]
                                 """)),
                metadataProvider: metadataProvider,
                fileWriter: new PassthroughFileWriter());

            var result = await generator.GenerateAsync(
                request: new GenerationRequest(
                    WorkspacePath: workspaceDirectory,
                    ProjectPath: appProjectPath,
                    TemplatePath: templatePath,
                    Mode: GenerationMode.Generate,
                    Configuration: TypewriterConfiguration.Default),
                cancellationToken: CancellationToken.None);

            result.Success.Should().BeTrue(because: string.Join(separator: Environment.NewLine, values: result.Diagnostics.Select(selector: diagnostic => diagnostic.Message)));
            var diagnostic = result.Diagnostics.Should().ContainSingle(predicate: candidate => candidate.Code == "TW0009").Which;
            diagnostic.Severity.Should().Be(DiagnosticSeverity.Warning);
            diagnostic.Message.Should().Contain("../External/External.csproj");
            metadataProvider.Projects.Select(selector: project => project.ProjectPath).Should().NotContain(externalProjectPath);
            result.GeneratedFiles.Should().ContainSingle()
                .Which.Path.Should().Be(Path.Combine(path1: workspaceDirectory, path2: "App", path3: "LocalDto.ts"));
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(directory: directory);
        }
    }

    [Fact]
    public async Task GenerateAsyncIgnoresIncludeProjectPointingToCurrentProject()
    {
        var directory = CreateProjectDirectory();
        try
        {
            var appProjectPath = Path.Combine(path1: directory, path2: "App", path3: "App.csproj");
            Directory.CreateDirectory(path: Path.GetDirectoryName(path: appProjectPath)!);
            await File.WriteAllTextAsync(path: appProjectPath, contents: "<Project />");

            var localModel = CreateClassMetadata(name: "LocalDto");
            var metadataProvider = new ProjectMappedMetadataProvider(
                metadataByProjectPath: new Dictionary<string, ProjectMetadata>(comparer: StringComparer.OrdinalIgnoreCase)
                {
                    [key: appProjectPath] = new ProjectMetadata(
                        ProjectPath: appProjectPath,
                        SourceFiles: [new SourceFileMetadata(Path: Path.Combine(path1: directory, path2: "App", path3: "LocalDto.cs"), Types: [localModel])],
                        Types: [localModel],
                        Diagnostics: []),
                });

            var templatePath = Path.Combine(path1: directory, path2: "App", path3: "Models.tst");
            var generator = new TypewriterGenerator(
                templateDiscovery: new StaticTemplateDiscovery(
                    templates: new TemplateFile(
                        Path: templatePath,
                        Content: """
                                 ${
                                     Template(Settings settings)
                                     {
                                         settings.IncludeProject("App");
                                     }
                                 }
                                 $Classes[
                                 export class $Name {
                                 }
                                 ]
                                 """)),
                metadataProvider: metadataProvider,
                fileWriter: new PassthroughFileWriter());

            var result = await generator.GenerateAsync(
                request: new GenerationRequest(
                    WorkspacePath: directory,
                    ProjectPath: appProjectPath,
                    TemplatePath: templatePath,
                    Mode: GenerationMode.Generate,
                    Configuration: TypewriterConfiguration.Default),
                cancellationToken: CancellationToken.None);

            result.Success.Should().BeTrue(because: string.Join(separator: Environment.NewLine, values: result.Diagnostics.Select(selector: diagnostic => diagnostic.Message)));
            result.Diagnostics.Should().NotContain(predicate: candidate => candidate.Code == "TW0009");
            metadataProvider.Projects.Should().ContainSingle();
            result.GeneratedFiles.Should().ContainSingle()
                .Which.Path.Should().Be(Path.Combine(path1: directory, path2: "App", path3: "LocalDto.ts"));
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(directory: directory);
        }
    }

    private static TypeMetadata CreateClassMetadata(string name) =>
        new(
            Name: name,
            FullName: "Sample." + name,
            Namespace: "Sample",
            Kind: TypeMetadataKind.Class,
            Accessibility: MetadataAccessibility.Public,
            Properties: [],
            Attributes: [],
            BaseTypes: [],
            EnumValues: [],
            IsNullableAware: true);

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

    private sealed class StaticTemplateDiscovery : ITemplateDiscovery
    {
        private readonly IReadOnlyList<TemplateFile> _templates;

        public StaticTemplateDiscovery(params TemplateFile[] templates)
        {
            _templates = templates;
        }

        public Task<IReadOnlyList<TemplateFile>> FindTemplatesAsync(
            WorkspaceContext workspace,
            GenerationRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(result: _templates);
    }

    private sealed class ProjectMappedMetadataProvider : IProjectMetadataProvider
    {
        private readonly IReadOnlyDictionary<string, ProjectMetadata> _metadataByProjectPath;
        private readonly List<ProjectContext> _projects = [];

        public ProjectMappedMetadataProvider(IReadOnlyDictionary<string, ProjectMetadata> metadataByProjectPath)
        {
            _metadataByProjectPath = metadataByProjectPath;
        }

        public IReadOnlyList<ProjectContext> Projects => _projects;

        public Task<ProjectMetadata> GetMetadataAsync(
            ProjectContext project,
            CancellationToken cancellationToken)
        {
            _projects.Add(item: project);
            return Task.FromResult(
                result: _metadataByProjectPath.TryGetValue(key: project.ProjectPath, value: out var metadata)
                    ? metadata
                    : new ProjectMetadata(ProjectPath: project.ProjectPath, SourceFiles: [], Types: [], Diagnostics: []));
        }
    }

    private sealed class PassthroughFileWriter : IGeneratedFileWriter
    {
        public Task<GeneratedFile> WriteAsync(
            GeneratedFile file,
            GenerationRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(result: file);
    }
}
