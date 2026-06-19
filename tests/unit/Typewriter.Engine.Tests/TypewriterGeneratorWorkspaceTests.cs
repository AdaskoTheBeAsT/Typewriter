using Typewriter.Abstractions;
using Typewriter.Engine;
using Xunit;

namespace Typewriter.Engine.Tests;

public sealed class TypewriterGeneratorWorkspaceTests
{
    [Fact]
    public async Task GenerateAsyncUsesSingleProjectFromSlnxWorkspace()
    {
        var directory = CreateProjectDirectory();
        try
        {
            var projectDirectory = Path.Combine(path1: directory, path2: "src", path3: "Sample");
            Directory.CreateDirectory(path: projectDirectory);

            var solutionPath = Path.Combine(path1: directory, path2: "Sample.slnx");
            var projectPath = Path.Combine(path1: projectDirectory, path2: "Sample.csproj");
            var templatePath = Path.Combine(path1: directory, path2: "Models.tst");

            await File.WriteAllTextAsync(
                path: solutionPath,
                contents: """
                          <Solution>
                            <Project Path="src/Sample/Sample.csproj" />
                          </Solution>
                          """);
            await File.WriteAllTextAsync(path: projectPath, contents: "<Project />");

            var metadataProvider = new CapturingMetadataProvider();
            var generator = new TypewriterGenerator(
                templateDiscovery: new StaticTemplateDiscovery(
                    templates: new TemplateFile(
                        Path: templatePath,
                        Content: """
                                 // output: generated.ts
                                 export const selectedProject = "$ProjectPath";
                                 """)),
                metadataProvider: metadataProvider,
                fileWriter: new PassthroughFileWriter());

            var result = await generator.GenerateAsync(
                request: new GenerationRequest(
                    WorkspacePath: solutionPath,
                    ProjectPath: null,
                    TemplatePath: templatePath,
                    Mode: GenerationMode.Generate,
                    Configuration: TypewriterConfiguration.Default),
                cancellationToken: CancellationToken.None);

            result.Success.Should().BeTrue(because: string.Join(separator: Environment.NewLine, values: result.Diagnostics.Select(selector: diagnostic => diagnostic.Message)));
            metadataProvider.Project.Should().NotBeNull();
            metadataProvider.Project!.ProjectPath.Should().Be(projectPath);
            metadataProvider.Project.WorkspacePath.Should().Be(solutionPath);
            result.GeneratedFiles.Should().ContainSingle();
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(directory: directory);
        }
    }

    [Fact]
    public async Task GenerateAsyncRequiresExplicitProjectWhenSlnxContainsMultipleProjects()
    {
        var directory = CreateProjectDirectory();
        try
        {
            var firstProjectPath = Path.Combine(path1: directory, path2: "First", path3: "First.csproj");
            var secondProjectPath = Path.Combine(path1: directory, path2: "Second", path3: "Second.csproj");
            Directory.CreateDirectory(path: Path.GetDirectoryName(path: firstProjectPath)!);
            Directory.CreateDirectory(path: Path.GetDirectoryName(path: secondProjectPath)!);

            var solutionPath = Path.Combine(path1: directory, path2: "Sample.slnx");
            await File.WriteAllTextAsync(
                path: solutionPath,
                contents: """
                          <Solution>
                            <Project Path="First/First.csproj" />
                            <Project Path="Second/Second.csproj" />
                          </Solution>
                          """);
            await File.WriteAllTextAsync(path: firstProjectPath, contents: "<Project />");
            await File.WriteAllTextAsync(path: secondProjectPath, contents: "<Project />");

            var generator = new TypewriterGenerator(
                templateDiscovery: new ThrowingTemplateDiscovery(),
                metadataProvider: new CapturingMetadataProvider(),
                fileWriter: new PassthroughFileWriter());

            var result = await generator.GenerateAsync(
                request: new GenerationRequest(
                    WorkspacePath: solutionPath,
                    ProjectPath: null,
                    TemplatePath: null,
                    Mode: GenerationMode.Generate,
                    Configuration: TypewriterConfiguration.Default),
                cancellationToken: CancellationToken.None);

            var diagnostic = result.Diagnostics.Should().ContainSingle().Which;
            result.Success.Should().BeFalse();
            diagnostic.Severity.Should().Be(DiagnosticSeverity.Error);
            diagnostic.Code.Should().Be("TW0003");
            diagnostic.Message.Should().Contain("Multiple .csproj files were found");
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(directory: directory);
        }
    }

    [Fact]
    public async Task GenerateAsyncUsesNearestAncestorProjectWhenWorkspaceContainsMultipleProjects()
    {
        var directory = CreateProjectDirectory();
        try
        {
            var firstProjectPath = Path.Combine(path1: directory, path2: "First", path3: "First.csproj");
            var secondProjectPath = Path.Combine(path1: directory, path2: "Second", path3: "Second.csproj");
            var templatePath = Path.Combine(path1: directory, path2: "Second", path3: "Templates", path4: "Models.tst");
            Directory.CreateDirectory(path: Path.GetDirectoryName(path: firstProjectPath)!);
            Directory.CreateDirectory(path: Path.GetDirectoryName(path: secondProjectPath)!);
            Directory.CreateDirectory(path: Path.GetDirectoryName(path: templatePath)!);
            await File.WriteAllTextAsync(path: firstProjectPath, contents: "<Project />");
            await File.WriteAllTextAsync(path: secondProjectPath, contents: "<Project />");

            var metadataProvider = new CapturingMetadataProvider();
            var generator = new TypewriterGenerator(
                templateDiscovery: new StaticTemplateDiscovery(
                    templates: new TemplateFile(
                        Path: templatePath,
                        Content: """
                                 // output: generated.ts
                                 export const selectedProject = "$ProjectPath";
                                 """)),
                metadataProvider: metadataProvider,
                fileWriter: new PassthroughFileWriter());

            var result = await generator.GenerateAsync(
                request: new GenerationRequest(
                    WorkspacePath: directory,
                    ProjectPath: null,
                    TemplatePath: templatePath,
                    Mode: GenerationMode.Generate,
                    Configuration: TypewriterConfiguration.Default),
                cancellationToken: CancellationToken.None);

            result.Success.Should().BeTrue(because: string.Join(separator: Environment.NewLine, values: result.Diagnostics.Select(selector: diagnostic => diagnostic.Message)));
            metadataProvider.Project.Should().NotBeNull();
            metadataProvider.Project!.ProjectPath.Should().Be(secondProjectPath);
            result.GeneratedFiles.Should().ContainSingle();
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(directory: directory);
        }
    }

    [Fact]
    public async Task GenerateAsyncProcessesEveryProjectWhenAllProjectsIsEnabled()
    {
        var directory = CreateProjectDirectory();
        try
        {
            var firstProjectPath = await CreateProjectWithTemplateAsync(directory: directory, projectName: "First");
            var secondProjectPath = await CreateProjectWithTemplateAsync(directory: directory, projectName: "Second");

            var solutionPath = Path.Combine(path1: directory, path2: "Sample.slnx");
            await File.WriteAllTextAsync(
                path: solutionPath,
                contents: """
                          <Solution>
                            <Project Path="First/First.csproj" />
                            <Project Path="Second/Second.csproj" />
                          </Solution>
                          """);

            var metadataProvider = new CapturingMetadataProvider();
            var generator = new TypewriterGenerator(
                templateDiscovery: new FileSystemTemplateDiscovery(),
                metadataProvider: metadataProvider,
                fileWriter: new PassthroughFileWriter());

            var result = await generator.GenerateAsync(
                request: new GenerationRequest(
                    WorkspacePath: solutionPath,
                    ProjectPath: null,
                    TemplatePath: null,
                    Mode: GenerationMode.Generate,
                    Configuration: TypewriterConfiguration.Default,
                    AllProjects: true),
                cancellationToken: CancellationToken.None);

            result.Success.Should().BeTrue(because: string.Join(separator: Environment.NewLine, values: result.Diagnostics.Select(selector: diagnostic => diagnostic.Message)));
            metadataProvider.Projects.Select(selector: project => project.ProjectPath).Should().Equal(firstProjectPath, secondProjectPath);
            result.GeneratedFiles.Select(selector: file => file.Path).Order(comparer: StringComparer.OrdinalIgnoreCase).Should().Equal(
                Path.Combine(path1: directory, path2: "First", path3: "generated.ts"),
                Path.Combine(path1: directory, path2: "Second", path3: "generated.ts"));
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(directory: directory);
        }
    }

    [Fact]
    public async Task GenerateAsyncFansOutBySourceFileUsingSourceFilenameWhenNoOutputFilenameFactoryIsConfigured()
    {
        var directory = CreateProjectDirectory();
        try
        {
            var projectPath = Path.Combine(path1: directory, path2: "Sample.csproj");
            var templatePath = Path.Combine(path1: directory, path2: "Models.tst");
            await File.WriteAllTextAsync(path: projectPath, contents: "<Project />");

            var userModel = CreateClassMetadata(name: "UserDto");
            var orderModel = CreateClassMetadata(name: "OrderDto");
            var metadataProvider = new CapturingMetadataProvider(
                types: [userModel, orderModel],
                sourceFiles:
                [
                    new SourceFileMetadata(Path: Path.Combine(path1: directory, path2: "UserDto.cs"), Types: [userModel]),
                    new SourceFileMetadata(Path: Path.Combine(path1: directory, path2: "OrderDto.cs"), Types: [orderModel]),
                ]);
            var generator = new TypewriterGenerator(
                templateDiscovery: new StaticTemplateDiscovery(
                    templates: new TemplateFile(
                        Path: templatePath,
                        Content: """
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
                    ProjectPath: projectPath,
                    TemplatePath: templatePath,
                    Mode: GenerationMode.Generate,
                    Configuration: TypewriterConfiguration.Default),
                cancellationToken: CancellationToken.None);

            result.Success.Should().BeTrue(because: string.Join(separator: Environment.NewLine, values: result.Diagnostics.Select(selector: diagnostic => diagnostic.Message)));
            result.GeneratedFiles.Select(selector: file => file.Path).Order(comparer: StringComparer.OrdinalIgnoreCase).Should().Equal(
                Path.Combine(path1: directory, path2: "OrderDto.ts"),
                Path.Combine(path1: directory, path2: "UserDto.ts"));
            result.GeneratedFiles.Should().Contain(
                file => file.Path.EndsWith(value: "UserDto.ts", comparisonType: StringComparison.OrdinalIgnoreCase)
                        && file.Content.Contains(value: "export class UserDto", comparisonType: StringComparison.Ordinal)
                        && !file.Content.Contains(value: "OrderDto", comparisonType: StringComparison.Ordinal));
            result.GeneratedFiles.Should().Contain(
                file => file.Path.EndsWith(value: "OrderDto.ts", comparisonType: StringComparison.OrdinalIgnoreCase)
                        && file.Content.Contains(value: "export class OrderDto", comparisonType: StringComparison.Ordinal)
                        && !file.Content.Contains(value: "UserDto", comparisonType: StringComparison.Ordinal));
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(directory: directory);
        }
    }

    [Fact]
    public async Task GenerateAsyncUsesOutputDirectoryAndExtensionForSourceFilenameFallback()
    {
        var directory = CreateProjectDirectory();
        try
        {
            var projectPath = Path.Combine(path1: directory, path2: "Sample.csproj");
            var templatePath = Path.Combine(path1: directory, path2: "Models.tst");
            await File.WriteAllTextAsync(path: projectPath, contents: "<Project />");

            var userModel = CreateClassMetadata(name: "UserDto");
            var metadataProvider = new CapturingMetadataProvider(
                types: [userModel],
                sourceFiles:
                [
                    new SourceFileMetadata(Path: Path.Combine(path1: directory, path2: "UserDto.cs"), Types: [userModel]),
                ]);
            var generator = new TypewriterGenerator(
                templateDiscovery: new StaticTemplateDiscovery(
                    templates: new TemplateFile(
                        Path: templatePath,
                        Content: """
                                 ${
                                     Template(Settings settings)
                                     {
                                         settings.OutputDirectory = "generated";
                                         settings.OutputExtension = "model.ts";
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
                    ProjectPath: projectPath,
                    TemplatePath: templatePath,
                    Mode: GenerationMode.Generate,
                    Configuration: TypewriterConfiguration.Default),
                cancellationToken: CancellationToken.None);

            result.Success.Should().BeTrue(because: string.Join(separator: Environment.NewLine, values: result.Diagnostics.Select(selector: diagnostic => diagnostic.Message)));
            var generatedFile = result.GeneratedFiles.Should().ContainSingle().Which;
            generatedFile.Path.Should().Be(Path.Combine(path1: directory, path2: "generated", path3: "UserDto.model.ts"));
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(directory: directory);
        }
    }

    [Theory]
    [InlineData(data: [FileNameConvention.Kebab, "user-dto.ts"])]
    [InlineData(data: [FileNameConvention.Pascal, "UserDto.ts"])]
    [InlineData(data: [FileNameConvention.Camel, "userDto.ts"])]
    [InlineData(data: [FileNameConvention.Snake, "user_dto.ts"])]
    public async Task GenerateAsyncUsesConfiguredFileNameConventionForSourceFilenameFallback(
        FileNameConvention fileNameConvention,
        string expectedFileName)
    {
        var directory = CreateProjectDirectory();
        try
        {
            var projectPath = Path.Combine(path1: directory, path2: "Sample.csproj");
            var templatePath = Path.Combine(path1: directory, path2: "Models.tst");
            await File.WriteAllTextAsync(path: projectPath, contents: "<Project />");

            var userModel = CreateClassMetadata(name: "UserDto");
            var metadataProvider = new CapturingMetadataProvider(
                types: [userModel],
                sourceFiles:
                [
                    new SourceFileMetadata(Path: Path.Combine(path1: directory, path2: "UserDto.cs"), Types: [userModel]),
                ]);
            var generator = new TypewriterGenerator(
                templateDiscovery: new StaticTemplateDiscovery(
                    templates: new TemplateFile(
                        Path: templatePath,
                        Content: """
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
                    ProjectPath: projectPath,
                    TemplatePath: templatePath,
                    Mode: GenerationMode.Generate,
                    Configuration: CreateConfiguration(fileNameConvention: fileNameConvention)),
                cancellationToken: CancellationToken.None);

            result.Success.Should().BeTrue(because: string.Join(separator: Environment.NewLine, values: result.Diagnostics.Select(selector: diagnostic => diagnostic.Message)));
            var generatedFile = result.GeneratedFiles.Should().ContainSingle().Which;
            generatedFile.Path.Should().Be(Path.Combine(path1: directory, path2: expectedFileName));
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(directory: directory);
        }
    }

    [Theory]
    [InlineData(data: [FileNameConvention.Kebab, "user-profile.ts"])]
    [InlineData(data: [FileNameConvention.Pascal, "UserProfile.ts"])]
    [InlineData(data: [FileNameConvention.Camel, "userProfile.ts"])]
    [InlineData(data: [FileNameConvention.Snake, "user_profile.ts"])]
    public async Task GenerateAsyncUsesConfiguredFileNameConventionForTemplateFilenameFallback(
        FileNameConvention fileNameConvention,
        string expectedFileName)
    {
        var directory = CreateProjectDirectory();
        try
        {
            var projectPath = Path.Combine(path1: directory, path2: "Sample.csproj");
            var templatePath = Path.Combine(path1: directory, path2: "UserProfile.tst");
            await File.WriteAllTextAsync(path: projectPath, contents: "<Project />");

            var generator = new TypewriterGenerator(
                templateDiscovery: new StaticTemplateDiscovery(
                    templates: new TemplateFile(
                        Path: templatePath,
                        Content: "export const generated = true;")),
                metadataProvider: new CapturingMetadataProvider(),
                fileWriter: new PassthroughFileWriter());

            var result = await generator.GenerateAsync(
                request: new GenerationRequest(
                    WorkspacePath: directory,
                    ProjectPath: projectPath,
                    TemplatePath: templatePath,
                    Mode: GenerationMode.Generate,
                    Configuration: CreateConfiguration(fileNameConvention: fileNameConvention)),
                cancellationToken: CancellationToken.None);

            result.Success.Should().BeTrue(because: string.Join(separator: Environment.NewLine, values: result.Diagnostics.Select(selector: diagnostic => diagnostic.Message)));
            var generatedFile = result.GeneratedFiles.Should().ContainSingle().Which;
            generatedFile.Path.Should().Be(Path.Combine(path1: directory, path2: expectedFileName));
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(directory: directory);
        }
    }

    [Fact]
    public async Task GenerateAsyncDoesNotApplyFileNameConventionToExplicitOutputPath()
    {
        var directory = CreateProjectDirectory();
        try
        {
            var projectPath = Path.Combine(path1: directory, path2: "Sample.csproj");
            var templatePath = Path.Combine(path1: directory, path2: "UserProfile.tst");
            await File.WriteAllTextAsync(path: projectPath, contents: "<Project />");

            var generator = new TypewriterGenerator(
                templateDiscovery: new StaticTemplateDiscovery(
                    templates: new TemplateFile(
                        Path: templatePath,
                        Content: """
                                 // output: CustomFile.ts
                                 export const generated = true;
                                 """)),
                metadataProvider: new CapturingMetadataProvider(),
                fileWriter: new PassthroughFileWriter());

            var result = await generator.GenerateAsync(
                request: new GenerationRequest(
                    WorkspacePath: directory,
                    ProjectPath: projectPath,
                    TemplatePath: templatePath,
                    Mode: GenerationMode.Generate,
                    Configuration: CreateConfiguration(fileNameConvention: FileNameConvention.Kebab)),
                cancellationToken: CancellationToken.None);

            result.Success.Should().BeTrue(because: string.Join(separator: Environment.NewLine, values: result.Diagnostics.Select(selector: diagnostic => diagnostic.Message)));
            var generatedFile = result.GeneratedFiles.Should().ContainSingle().Which;
            generatedFile.Path.Should().Be(Path.Combine(path1: directory, path2: "CustomFile.ts"));
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(directory: directory);
        }
    }

    [Fact]
    public async Task GenerateAsyncUsesOutputFilenameFactoryFromCompiledTemplateSettings()
    {
        var directory = CreateProjectDirectory();
        try
        {
            var projectPath = Path.Combine(path1: directory, path2: "Sample.csproj");
            var templatePath = Path.Combine(path1: directory, path2: "Services.tst");
            await File.WriteAllTextAsync(path: projectPath, contents: "<Project />");

            var metadataProvider = new CapturingMetadataProvider(
                types:
                [
                    CreateClassMetadata(name: "UsersController"),
                ]);
            var generator = new TypewriterGenerator(
                templateDiscovery: new StaticTemplateDiscovery(
                    templates: new TemplateFile(
                        Path: templatePath,
                        Content: """
                                 ${
                                     Template(Settings settings)
                                     {
                                         settings.OutputFilenameFactory = file => $"api-{file.Classes.First().Name.Replace("Controller", string.Empty).ToLowerInvariant()}.service.ts";
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
                    ProjectPath: projectPath,
                    TemplatePath: templatePath,
                    Mode: GenerationMode.Generate,
                    Configuration: TypewriterConfiguration.Default),
                cancellationToken: CancellationToken.None);

            result.Success.Should().BeTrue(because: string.Join(separator: Environment.NewLine, values: result.Diagnostics.Select(selector: diagnostic => diagnostic.Message)));
            var generatedFile = result.GeneratedFiles.Should().ContainSingle().Which;
            generatedFile.Path.Should().Be(Path.Combine(path1: directory, path2: "api-users.service.ts"));
            generatedFile.Content.Should().Contain("export class UsersController");
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(directory: directory);
        }
    }

    [Fact]
    public async Task GenerateAsyncFansOutBySourceFileWhenOutputFilenameFactoryIsConfigured()
    {
        var directory = CreateProjectDirectory();
        try
        {
            var projectPath = Path.Combine(path1: directory, path2: "Sample.csproj");
            var templatePath = Path.Combine(path1: directory, path2: "Services.tst");
            await File.WriteAllTextAsync(path: projectPath, contents: "<Project />");

            var usersController = CreateClassMetadata(name: "UsersController");
            var ordersController = CreateClassMetadata(name: "OrdersController");
            var userModel = CreateClassMetadata(name: "UserDto");
            var metadataProvider = new CapturingMetadataProvider(
                types: [usersController, ordersController, userModel],
                sourceFiles:
                [
                    new SourceFileMetadata(Path: Path.Combine(path1: directory, path2: "UsersController.cs"), Types: [usersController]),
                    new SourceFileMetadata(Path: Path.Combine(path1: directory, path2: "OrdersController.cs"), Types: [ordersController]),
                    new SourceFileMetadata(Path: Path.Combine(path1: directory, path2: "UserDto.cs"), Types: [userModel]),
                ]);
            var generator = new TypewriterGenerator(
                templateDiscovery: new StaticTemplateDiscovery(
                    templates: new TemplateFile(
                        Path: templatePath,
                        Content: """
                                 ${
                                     Template(Settings settings)
                                     {
                                         settings.OutputFilenameFactory = file => $"{file.Classes.First().Name.Replace("Controller", string.Empty).ToLowerInvariant()}.service.ts";
                                     }

                                     bool IncludeClass(Class c) => c.Name.EndsWith("Controller");
                                 }
                                 // static header should not create a file for source files with no matching classes
                                 $Classes($IncludeClass)[
                                 export class $Name {
                                 }
                                 ]
                                 """)),
                metadataProvider: metadataProvider,
                fileWriter: new PassthroughFileWriter());

            var result = await generator.GenerateAsync(
                request: new GenerationRequest(
                    WorkspacePath: directory,
                    ProjectPath: projectPath,
                    TemplatePath: templatePath,
                    Mode: GenerationMode.Generate,
                    Configuration: TypewriterConfiguration.Default),
                cancellationToken: CancellationToken.None);

            result.Success.Should().BeTrue(because: string.Join(separator: Environment.NewLine, values: result.Diagnostics.Select(selector: diagnostic => diagnostic.Message)));
            result.GeneratedFiles.Select(selector: file => file.Path).Order(comparer: StringComparer.OrdinalIgnoreCase).Should().Equal(
                Path.Combine(path1: directory, path2: "orders.service.ts"),
                Path.Combine(path1: directory, path2: "users.service.ts"));
            result.GeneratedFiles.Should().Contain(
                file => file.Path.EndsWith(value: "users.service.ts", comparisonType: StringComparison.OrdinalIgnoreCase)
                        && file.Content.Contains(value: "export class UsersController", comparisonType: StringComparison.Ordinal)
                        && !file.Content.Contains(value: "OrdersController", comparisonType: StringComparison.Ordinal));
            result.GeneratedFiles.Should().Contain(
                file => file.Path.EndsWith(value: "orders.service.ts", comparisonType: StringComparison.OrdinalIgnoreCase)
                        && file.Content.Contains(value: "export class OrdersController", comparisonType: StringComparison.Ordinal)
                        && !file.Content.Contains(value: "UsersController", comparisonType: StringComparison.Ordinal));
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(directory: directory);
        }
    }

    [Fact]
    public async Task GenerateAsyncReportsDuplicateOutputPathsAcrossTemplates()
    {
        var directory = CreateProjectDirectory();
        try
        {
            var projectPath = Path.Combine(path1: directory, path2: "Sample.csproj");
            await File.WriteAllTextAsync(path: projectPath, contents: "<Project />");

            var firstTemplatePath = Path.Combine(path1: directory, path2: "First.tst");
            var secondTemplatePath = Path.Combine(path1: directory, path2: "Second.tst");
            var generator = new TypewriterGenerator(
                templateDiscovery: new StaticTemplateDiscovery(
                    new TemplateFile(
                        Path: firstTemplatePath,
                        Content: """
                                 // output: generated.ts
                                 export const first = true;
                                 """),
                    new TemplateFile(
                        Path: secondTemplatePath,
                        Content: """
                                 // output: generated.ts
                                 export const second = true;
                                 """)),
                metadataProvider: new CapturingMetadataProvider(),
                fileWriter: new PassthroughFileWriter());

            var result = await generator.GenerateAsync(
                request: new GenerationRequest(
                    WorkspacePath: directory,
                    ProjectPath: projectPath,
                    TemplatePath: null,
                    Mode: GenerationMode.Generate,
                    Configuration: TypewriterConfiguration.Default),
                cancellationToken: CancellationToken.None);

            result.Success.Should().BeTrue(because: string.Join(separator: Environment.NewLine, values: result.Diagnostics.Select(selector: diagnostic => diagnostic.Message)));
            var diagnostic = result.Diagnostics.Should().ContainSingle(candidate => candidate.Code == "TW0008").Which;
            diagnostic.Severity.Should().Be(DiagnosticSeverity.Warning);
            diagnostic.Message.Should().Contain("more than one template");
            result.GeneratedFiles.Should().HaveCount(2);
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(directory: directory);
        }
    }

    private static async Task<string> CreateProjectWithTemplateAsync(
        string directory,
        string projectName)
    {
        var projectDirectory = Path.Combine(path1: directory, path2: projectName);
        Directory.CreateDirectory(path: projectDirectory);
        var projectPath = Path.Combine(path1: projectDirectory, path2: $"{projectName}.csproj");
        await File.WriteAllTextAsync(path: projectPath, contents: "<Project />");
        await File.WriteAllTextAsync(
            path: Path.Combine(path1: projectDirectory, path2: "Models.tst"),
            contents: """
                      // output: generated.ts
                      export const projectPath = "$ProjectPath";
                      """);
        return projectPath;
    }

    private static TypewriterConfiguration CreateConfiguration(FileNameConvention fileNameConvention) =>
        TypewriterConfiguration.Default with
        {
            Output = TypewriterConfiguration.Default.Output with
            {
                FileNameConvention = fileNameConvention,
            },
        };

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

    private sealed class ThrowingTemplateDiscovery : ITemplateDiscovery
    {
        public Task<IReadOnlyList<TemplateFile>> FindTemplatesAsync(
            WorkspaceContext workspace,
            GenerationRequest request,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException(message: "Template discovery should not run after project resolution fails.");
    }

    private sealed class CapturingMetadataProvider : IProjectMetadataProvider
    {
        private readonly List<ProjectContext> _projects = [];
        private readonly IReadOnlyList<SourceFileMetadata> _sourceFiles;
        private readonly IReadOnlyList<TypeMetadata> _types;

        public CapturingMetadataProvider()
            : this(types: [])
        {
        }

        public CapturingMetadataProvider(
            IReadOnlyList<TypeMetadata> types,
            IReadOnlyList<SourceFileMetadata>? sourceFiles = null)
        {
            _types = types;
            _sourceFiles = sourceFiles ?? [];
        }

        public ProjectContext? Project { get; private set; }

        public IReadOnlyList<ProjectContext> Projects => _projects;

        public Task<ProjectMetadata> GetMetadataAsync(
            ProjectContext project,
            CancellationToken cancellationToken)
        {
            Project = project;
            _projects.Add(item: project);
            return Task.FromResult(result: new ProjectMetadata(ProjectPath: project.ProjectPath, SourceFiles: _sourceFiles, Types: _types, Diagnostics: []));
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
