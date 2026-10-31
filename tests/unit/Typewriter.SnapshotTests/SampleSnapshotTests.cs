using System.Globalization;
using AwesomeAssertions;
using Typewriter.Abstractions;
using Typewriter.Engine;
using Typewriter.Roslyn;
using Xunit;

namespace Typewriter.SnapshotTests;

public sealed class SampleSnapshotTests
{
    [Theory]
    [InlineData(data: ["SimpleApi", "SimpleApi.csproj", "Models.tst", "generated/models.ts"])]
    [InlineData(data: ["NullableModels", "NullableModels.csproj", "Models.tst", "generated/models.ts"])]
    [InlineData(data: ["RecordsAndEnums", "RecordsAndEnums.csproj", "Models.tst", "generated/models.ts"])]
    [InlineData(data: ["MultiProjectSolution", "Api/Api.csproj", "Api/Models.tst", "Api/generated/models.ts"])]
    [InlineData(data: ["WebApiServices", "WebApiServices.csproj", "Services.tst", "generated/users.service.ts"])]
    [InlineData(data: ["WebApiServices", "WebApiServices.csproj", "Constants.tst", "generated/constants.ts"])]
    [InlineData(data: ["SignalRHubs", "SignalRHubs.csproj", "SignalRHubs.tst", "generated/signalr-chat.service.ts"])]
    [InlineData(data: ["issue66", "App/App.csproj", "App/Models.tst", "App/generated/models.ts"])]
    [InlineData(data: ["issue67", "App/App.csproj", "App/Models.tst", "App/generated/models.ts"])]
    [InlineData(data: ["issue68", "Core/Core.csproj", "Core/Models.tst", "Core/generated/models.ts"])]
    [InlineData(data: ["issue69", "LegacyApp/LegacyApp.csproj", "LegacyApp/Models.tst", "LegacyApp/generated/models.ts"])]
    [InlineData(data: ["issue69v2", "MyProject/MyProject.csproj", "MyProject/_Resources/_Services.tst", "MyProject/_Resources/generated/services.ts"])]
    [InlineData(data: ["issue74", "App/App.csproj", "App/Models.tst", "App/MyModel.ts"])]
    [InlineData(data: ["issue75", "App/App.csproj", "App/Models.tst", "App/MyModel.ts"])]
    [InlineData(data: ["issue81", "App/App.csproj", "App/Models.tst", "App/MyModel.ts"])]
    [InlineData(data: ["issue90", "App/App.csproj", "App/Models.tst", "App/Models.ts"])]
    [InlineData(data: ["issue96", "App/App.csproj", "App/Models.tst", "App/DerivedRecordVm.ts"])]
    [InlineData(data: ["issue114", "Issue114.csproj", "Models.tst", "Model.ts"])]
    [InlineData(data: ["issue115", "Issue115.csproj", "Models.tst", "TestModel.ts"])]
    public Task GenerateAsyncMatchesModelsSnapshot(
        string sampleName,
        string projectPath,
        string templatePath,
        string outputPath) =>
        AssertGeneratedSnapshotAsync(sampleName: sampleName, projectPath: projectPath, templatePath: templatePath, outputPath: outputPath);

    [Theory]
    [InlineData("en-US")]
    [InlineData("pl-PL")]
    [InlineData("fr-FR")]
    public async Task GenerateAsyncPreservesAttributeArgumentValueTypes(string cultureName)
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var workspace = Directory.CreateTempSubdirectory(prefix: "TypewriterIssue125SnapshotTests-");
        try
        {
            // Separate workspaces prevent metadata cached under one culture from masking another.
            var fixture = Path.Combine(path1: FindRepositoryRoot(), path2: "samples", path3: "issue125");
            string[] fileNames = ["Issue125.csproj", "Models.cs", "Models.tst", "Models.ts"];
            foreach (var fileName in fileNames)
            {
#pragma warning disable SEC0116
                File.Copy(
                    sourceFileName: Path.Combine(path1: fixture, path2: fileName),
                    destFileName: Path.Combine(path1: workspace.FullName, path2: fileName));
#pragma warning restore SEC0116
            }

            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name: cultureName);

            await AssertGeneratedSnapshotAsync(
                sampleName: "issue125",
                projectPath: "Issue125.csproj",
                templatePath: "Models.tst",
                outputPath: "Models.ts",
                sampleDirectory: workspace.FullName);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            await DeleteDirectoryWithRetryAsync(directory: workspace.FullName);
        }
    }

    private static async Task AssertGeneratedSnapshotAsync(
        string sampleName,
        string projectPath,
        string templatePath,
        string outputPath,
        string? sampleDirectory = null)
    {
        sampleDirectory ??= Path.Combine(path1: FindRepositoryRoot(), path2: "samples", path3: sampleName);
        var absoluteProjectPath = ResolveSamplePath(sampleDirectory: sampleDirectory, relativePath: projectPath);
        var absoluteTemplatePath = ResolveSamplePath(sampleDirectory: sampleDirectory, relativePath: templatePath);
        var expectedOutputPath = ResolveSamplePath(sampleDirectory: sampleDirectory, relativePath: outputPath);

        var generator = new TypewriterGenerator(
            templateDiscovery: new FileSystemTemplateDiscovery(),
            metadataProvider: new CSharpProjectMetadataProvider(),
            fileWriter: new FileSystemGeneratedFileWriter());

        var configuration = TypewriterConfiguration.Default with
        {
            Output = TypewriterConfiguration.Default.Output with
            {
                DryRun = true,
                Newline = "lf",
            },
        };

        var result = await generator.GenerateAsync(
            request: new GenerationRequest(
                WorkspacePath: sampleDirectory,
                ProjectPath: absoluteProjectPath,
                TemplatePath: absoluteTemplatePath,
                Mode: GenerationMode.Generate,
                Configuration: configuration),
            cancellationToken: CancellationToken.None).ConfigureAwait(continueOnCapturedContext: false);

        result.Success.Should().BeTrue(because: FormatDiagnostics(diagnostics: result.Diagnostics));

        var generatedFile = result.GeneratedFiles.Should().ContainSingle().Which;
        generatedFile.Path.Should().Be(expectedOutputPath);
        generatedFile.Content.Should().StartWith(GeneratedFileHeader.Value);
        generatedFile.Content.Should().NotContain("\r\n");

        var expected = await File.ReadAllTextAsync(path: expectedOutputPath).ConfigureAwait(continueOnCapturedContext: false);
        generatedFile.Content.Should().Be(expected);
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

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(path: AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(path: Path.Combine(path1: directory.FullName, path2: "AdaskoTheBeAsT.Typewriter.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return AppContext.BaseDirectory;
    }

    private static string ResolveSamplePath(
        string sampleDirectory,
        string relativePath) =>
        Path.Combine(
            paths: [sampleDirectory, .. relativePath.Split(separator: ['/', '\\'], options: StringSplitOptions.RemoveEmptyEntries)]);

    private static string FormatDiagnostics(IEnumerable<GenerationDiagnostic> diagnostics) =>
        string.Join(
            separator: Environment.NewLine,
            values: diagnostics.Select(
                selector: diagnostic =>
                    string.Create(
                        provider: CultureInfo.InvariantCulture,
                        handler: $"{diagnostic.Severity}: {diagnostic.Code} {diagnostic.Message} {diagnostic.File}:{diagnostic.Line}:{diagnostic.Column}")));
}
