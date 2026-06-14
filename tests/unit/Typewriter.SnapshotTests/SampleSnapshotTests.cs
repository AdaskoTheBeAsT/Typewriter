using System.Globalization;
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
    public async Task GenerateAsyncMatchesModelsSnapshot(
        string sampleName,
        string projectPath,
        string templatePath,
        string outputPath)
    {
        var repositoryRoot = FindRepositoryRoot();
        var sampleDirectory = Path.Combine(path1: repositoryRoot, path2: "samples", path3: sampleName);
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
            cancellationToken: CancellationToken.None);

        Assert.True(condition: result.Success, userMessage: FormatDiagnostics(diagnostics: result.Diagnostics));

        var generatedFile = Assert.Single(collection: result.GeneratedFiles);
        Assert.Equal(expected: expectedOutputPath, actual: generatedFile.Path);
        Assert.StartsWith(expectedStartString: GeneratedFileHeader.Value, actualString: generatedFile.Content, comparisonType: StringComparison.Ordinal);
        Assert.DoesNotContain(expectedSubstring: "\r\n", actualString: generatedFile.Content, comparisonType: StringComparison.Ordinal);

        var expected = await File.ReadAllTextAsync(path: expectedOutputPath);
        Assert.Equal(expected: NormalizeLineEndings(value: expected), actual: NormalizeLineEndings(value: generatedFile.Content));
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

    private static string NormalizeLineEndings(string value) =>
        value.Replace(oldValue: "\r\n", newValue: "\n", comparisonType: StringComparison.Ordinal)
            .Replace(oldChar: '\r', newChar: '\n');

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
