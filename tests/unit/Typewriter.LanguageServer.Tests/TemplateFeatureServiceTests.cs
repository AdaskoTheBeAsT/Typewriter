using Xunit;

namespace Typewriter.LanguageServer.Tests;

public sealed class TemplateFeatureServiceTests
{
    [Fact]
    public async Task GetCompletionsAsyncReturnsTemplateHelpersAndMetadata()
    {
        var directory = CreateProjectDirectory();
        try
        {
            var project = await CreateSimpleProjectAsync(directory: directory);
            const string Template = """
                ${
                    string FormatName(Typewriter.CodeModel.Class @class) => @class.Name;
                }
                $
                """;
            var document = CreateDocument(path: project.TemplatePath, text: Template);
            var settings = CreateSettings(directory: directory, projectPath: project.ProjectPath);

            using var service = new TemplateFeatureService();
            var completions = await service.GetCompletionsAsync(document: document, settings: settings, position: new LspPosition(Line: 3, Character: 1), cancellationToken: CancellationToken.None);

            completions.Items.Should().Contain(item => item.Label == "Classes");
            completions.Items.Should().Contain(item => item.Label == "Structs");
            completions.Items.Should().Contain(item => item.Label == "Customer");
            completions.Items.Should().Contain(item => item.Label == "Coordinate");
            completions.Items.Should().Contain(item => item.Label == "DisplayName");
            completions.Items.Should().Contain(item => item.Label == "index");
            completions.Items.Should().Contain(item => item.Label == "IsIndexer");
            completions.Items.Should().Contain(item => item.Label == "FormatName");
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(directory: directory);
        }
    }

    [Fact]
    public async Task GetCompletionsAsyncUsesNearestAncestorProjectWhenWorkspaceContainsMultipleProjects()
    {
        var directory = CreateProjectDirectory();
        try
        {
            var firstProjectPath = Path.Combine(path1: directory, path2: "First", path3: "First.csproj");
            Directory.CreateDirectory(path: Path.GetDirectoryName(path: firstProjectPath)!);
            await File.WriteAllTextAsync(path: firstProjectPath, contents: "<Project />");
            var project = await CreateNestedSimpleProjectAsync(directory: directory, projectName: "Second");
            const string Template = "$";
            var document = CreateDocument(path: project.TemplatePath, text: Template);
            var settings = new LanguageServerSettings(
                RootPath: directory,
                WorkspacePath: directory,
                ProjectPath: null,
                Framework: "net10.0",
                AllProjects: false);

            using var service = new TemplateFeatureService();
            var completions = await service.GetCompletionsAsync(
                document: document,
                settings: settings,
                position: new LspPosition(Line: 0, Character: 1),
                cancellationToken: CancellationToken.None);

            completions.Items.Should().Contain(item => item.Label == "Customer");
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(directory: directory);
        }
    }

    [Fact]
    public async Task GetCompletionsAsyncReturnsCSharpItemsInsideHelperBlock()
    {
        var directory = CreateProjectDirectory();
        try
        {
            const string Template = """
                ${
                    str
                }
                """;
            var templatePath = Path.Combine(path1: directory, path2: "Models.tst");
            var document = CreateDocument(path: templatePath, text: Template);
            var settings = new LanguageServerSettings(
                RootPath: directory,
                WorkspacePath: directory,
                ProjectPath: null,
                Framework: null,
                AllProjects: false);
            var position = PositionOf(text: Template, marker: "str");

            using var service = new TemplateFeatureService();
            var completions = await service.GetCompletionsAsync(
                document: document,
                settings: settings,
                position: position with { Character = position.Character + 3 },
                cancellationToken: CancellationToken.None);

            completions.Items.Should().Contain(item => item.Label == "string");
            completions.Items.Should().Contain(item => item.Label == "Class");
            completions.Items.Should().Contain(item => item.Label == "File");
            completions.Items.Should().Contain(item => item.Label == "Struct");
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(directory: directory);
        }
    }

    [Fact]
    public async Task GetCompletionsAsyncReturnsTypeScriptItemsInsideOutputRegion()
    {
        var directory = CreateProjectDirectory();
        try
        {
            const string Template = "export inter";
            var templatePath = Path.Combine(path1: directory, path2: "Models.tst");
            var document = CreateDocument(path: templatePath, text: Template);
            var settings = new LanguageServerSettings(
                RootPath: directory,
                WorkspacePath: directory,
                ProjectPath: null,
                Framework: null,
                AllProjects: false);
            var position = PositionOf(text: Template, marker: "inter");

            using var service = new TemplateFeatureService();
            var completions = await service.GetCompletionsAsync(
                document: document,
                settings: settings,
                position: position with { Character = position.Character + 5 },
                cancellationToken: CancellationToken.None);

            completions.Items.Should().Contain(item => item.Label == "interface");
            completions.Items.Should().Contain(item => item.Label == "export");
            completions.Items.Should().Contain(item => item.Label == "$Classes");
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(directory: directory);
        }
    }

    [Fact]
    public void GetSemanticTokensClassifiesEmbeddedCSharpTypeScriptAndTemplateTokens()
    {
        const string Template = """
            ${
                string FormatName(Typewriter.CodeModel.Class @class) => @class.Name;
            }

            export interface $Name {
              title: string;
            }
            """;
        var document = CreateDocument(path: Path.Combine(path1: Path.GetTempPath(), path2: "Models.tst"), text: Template);

        var tokens = new TemplateSemanticTokenService().GetSemanticTokens(document: document);
        var decoded = DecodeSemanticTokens(document: document, semanticTokens: tokens);

        decoded.Should().Contain(token => token.Text == "string" && token.Type == "type");
        decoded.Should().Contain(token => token.Text == "FormatName" && token.Type == "method");
        decoded.Should().Contain(token => token.Text == "export" && token.Type == "keyword");
        decoded.Should().Contain(token => token.Text == "interface" && token.Type == "interface");
        decoded.Should().Contain(token => token.Text == "$Name" && token.Type == "macro");
    }

    [Fact]
    public async Task GetHoverAsyncReturnsMetadataDocumentation()
    {
        var directory = CreateProjectDirectory();
        try
        {
            var project = await CreateSimpleProjectAsync(directory: directory);
            const string Template = "$Classes(Name=Customer)[$Name]";
            var document = CreateDocument(path: project.TemplatePath, text: Template);
            var settings = CreateSettings(directory: directory, projectPath: project.ProjectPath);

            using var service = new TemplateFeatureService();
            var hover = await service.GetHoverAsync(
                document: document,
                settings: settings,
                position: PositionOf(text: Template, marker: "Customer"),
                cancellationToken: CancellationToken.None);

            hover.Should().NotBeNull();
            hover!.Contents.Value.Should().Contain("Sample.Customer");
            hover.Contents.Value.Should().Contain("Customer docs.");
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(directory: directory);
        }
    }

    [Fact]
    public async Task GetDefinitionsAsyncReturnsCSharpSourceLocation()
    {
        var directory = CreateProjectDirectory();
        try
        {
            var project = await CreateSimpleProjectAsync(directory: directory);
            const string Template = "$Classes(Name=Customer)[$Name]";
            var document = CreateDocument(path: project.TemplatePath, text: Template);
            var settings = CreateSettings(directory: directory, projectPath: project.ProjectPath);

            using var service = new TemplateFeatureService();
            var locations = await service.GetDefinitionsAsync(
                document: document,
                settings: settings,
                position: PositionOf(text: Template, marker: "Customer"),
                cancellationToken: CancellationToken.None);

            var location = locations.Should().ContainSingle().Which;
            location.Uri.Should().Be(UriFromPath(path: project.SourcePath));
            location.Range.Start.Line.Should().BeGreaterThanOrEqualTo(2);
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(directory: directory);
        }
    }

    [Fact]
    public async Task GetDefinitionsAsyncReturnsGeneratedFileForOutputDirective()
    {
        var directory = CreateProjectDirectory();
        try
        {
            var project = await CreateSimpleProjectAsync(directory: directory);
            var generatedPath = Path.Combine(path1: directory, path2: "generated", path3: "models.ts");
            const string Template = """
                // output: generated/models.ts
                $Classes[$Name]
                """;
            var document = CreateDocument(path: project.TemplatePath, text: Template);
            var settings = CreateSettings(directory: directory, projectPath: project.ProjectPath);

            using var service = new TemplateFeatureService();
            var locations = await service.GetDefinitionsAsync(
                document: document,
                settings: settings,
                position: PositionOf(text: Template, marker: "generated/models.ts"),
                cancellationToken: CancellationToken.None);

            var location = locations.Should().ContainSingle().Which;
            location.Uri.Should().Be(UriFromPath(path: generatedPath));
            location.Range.Start.Line.Should().Be(0);
            location.Range.Start.Character.Should().Be(0);
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(directory: directory);
        }
    }

    [Fact]
    public void EmbeddedCSharpDocumentMapsPositionsBidirectionally()
    {
        const string Template = """
            ${
                string FormatName(Class c) => c.Name;
            }
            """;
        var virtualDoc = EmbeddedCSharpDocument.Create(templateText: Template);
        virtualDoc.Should().NotBeNull();

        // The "string" keyword in the helper block should be mappable
        var stringIndex = Template.IndexOf(value: "string", comparisonType: StringComparison.Ordinal);
        virtualDoc!.TryMapToVirtual(templateOffset: stringIndex, virtualOffset: out var virtualOffset).Should().BeTrue();
        virtualOffset.Should().BeGreaterThanOrEqualTo(0);
        virtualDoc.TryMapToTemplate(virtualOffset: virtualOffset, templateOffset: out var roundTripped).Should().BeTrue();
        roundTripped.Should().Be(stringIndex);
    }

    [Fact]
    public void EmbeddedCSharpDocumentReturnsNullForTemplateWithoutHelperBlocks()
    {
        const string Template = "export interface $Name { }";
        var virtualDoc = EmbeddedCSharpDocument.Create(templateText: Template);
        virtualDoc.Should().BeNull();
    }

    [Fact]
    public void EmbeddedCSharpDocumentProducesCompilableSource()
    {
        const string Template = """
            ${
                string s = "test";
                s.
            }
            """;
        var virtualDoc = EmbeddedCSharpDocument.Create(templateText: Template);
        virtualDoc.Should().NotBeNull();
        virtualDoc!.Source.Should().Contain("string s");
    }

    [Fact]
    public async Task EmbeddedCSharpHoverReturnsSignature()
    {
        const string Template = """
            ${
                string FormatName(Class c) => c.Name;
            }
            """;
        var directory = CreateProjectDirectory();
        try
        {
            var templatePath = Path.Combine(path1: directory, path2: "Test.tst");
            var document = CreateDocument(path: templatePath, text: Template);
            var nameIndex = Template.IndexOf(value: "FormatName", comparisonType: StringComparison.Ordinal);

            using var service = new EmbeddedCSharpLanguageService();
            var hover = await service.GetHoverAsync(
                document: document,
                templateOffset: nameIndex,
                cancellationToken: CancellationToken.None);

            hover.Should().NotBeNull();
            hover!.Contents.Value.Should().Contain("FormatName");
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(directory: directory);
        }
    }

    [Fact]
    public async Task EmbeddedCSharpRequestsCanRunInParallel()
    {
        const string FirstTemplate = """
            ${
                str
            }
            """;
        const string SecondTemplate = """
            ${
                str
            }
            """;
        var directory = CreateProjectDirectory();
        try
        {
            var firstDocument = CreateDocument(path: Path.Combine(path1: directory, path2: "First.tst"), text: FirstTemplate);
            var secondDocument = CreateDocument(path: Path.Combine(path1: directory, path2: "Second.tst"), text: SecondTemplate);
            var firstOffset = FirstTemplate.IndexOf(value: "str", comparisonType: StringComparison.Ordinal) + "str".Length;
            var secondOffset = SecondTemplate.IndexOf(value: "str", comparisonType: StringComparison.Ordinal) + "str".Length;

#pragma warning disable IDISP013 // Service must stay alive while parallel requests complete
            using var service = new EmbeddedCSharpLanguageService();
            var tasks = Enumerable.Range(start: 0, count: 8)
                .Select(
                    selector: index => service.GetCompletionsAsync(
                        document: index % 2 == 0 ? firstDocument : secondDocument,
                        templateOffset: index % 2 == 0 ? firstOffset : secondOffset,
                        cancellationToken: CancellationToken.None))
                .ToArray();
            var completionResults = await Task.WhenAll(tasks: tasks);
#pragma warning restore IDISP013

            foreach (var completions in completionResults)
            {
                completions.Should().NotBeNull();
                completions.Items.Should().Contain(item => item.Label == "Class");
            }
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(directory: directory);
        }
    }

    [Fact]
    public async Task GetCompletionsAsyncUsesRoslynForCSharpHelperBlocks()
    {
        var directory = CreateProjectDirectory();
        try
        {
            const string Template = """
                ${
                    usi
                }
                """;
            var templatePath = Path.Combine(path1: directory, path2: "Test.tst");
            var document = CreateDocument(path: templatePath, text: Template);
            var settings = new LanguageServerSettings(
                RootPath: directory,
                WorkspacePath: directory,
                ProjectPath: null,
                Framework: null,
                AllProjects: false);
            var position = PositionOf(text: Template, marker: "usi");

            using var service = new TemplateFeatureService();
            var completions = await service.GetCompletionsAsync(
                document: document,
                settings: settings,
                position: position with { Character = position.Character + 3 },
                cancellationToken: CancellationToken.None);

            completions.Items.Should().Contain(item => item.Label == "using");
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(directory: directory);
        }
    }

    [Fact]
    public async Task GetCompletionsAsyncTypeScriptRegionIncludesBuiltInTypes()
    {
        var directory = CreateProjectDirectory();
        try
        {
            const string Template = "export interface $Name { pro: Pro";
            var templatePath = Path.Combine(path1: directory, path2: "Test.tst");
            var document = CreateDocument(path: templatePath, text: Template);
            var settings = new LanguageServerSettings(
                RootPath: directory,
                WorkspacePath: directory,
                ProjectPath: null,
                Framework: null,
                AllProjects: false);
            var position = PositionOf(text: Template, marker: "Pro");

            using var service = new TemplateFeatureService();
            var completions = await service.GetCompletionsAsync(
                document: document,
                settings: settings,
                position: position with { Character = position.Character + 3 },
                cancellationToken: CancellationToken.None);

            completions.Items.Should().Contain(item => item.Label == "Promise");
            completions.Items.Should().Contain(item => item.Label == "Partial");
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(directory: directory);
        }
    }

    private static async Task<SampleProject> CreateSimpleProjectAsync(string directory)
    {
        Directory.CreateDirectory(path: directory);
        var projectPath = Path.Combine(path1: directory, path2: "Sample.csproj");
        var templatePath = Path.Combine(path1: directory, path2: "Models.tst");
        var sourcePath = Path.Combine(path1: directory, path2: "Models", path3: "Customer.cs");
        Directory.CreateDirectory(path: Path.GetDirectoryName(path: sourcePath)!);

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
                      """).ConfigureAwait(continueOnCapturedContext: false);
        await File.WriteAllTextAsync(
            path: sourcePath,
            contents: """
                      namespace Sample;

                      /// <summary>Customer docs.</summary>
                      public sealed class Customer
                      {
                          /// <summary>Display name docs.</summary>
                          public required string DisplayName { get; init; }

                              public string this[int index] => DisplayName;
                          }

                          public struct Coordinate
                          {
                              public int X { get; init; }
                      }
                      """).ConfigureAwait(continueOnCapturedContext: false);
        await File.WriteAllTextAsync(
            path: templatePath,
            contents: """
                      // output: generated/models.ts
                      $Classes[$Name]
                      """).ConfigureAwait(continueOnCapturedContext: false);

        return new SampleProject(ProjectPath: projectPath, TemplatePath: templatePath, SourcePath: sourcePath);
    }

    private static async Task<SampleProject> CreateNestedSimpleProjectAsync(
        string directory,
        string projectName)
    {
        var projectDirectory = Path.Combine(path1: directory, path2: projectName);
        Directory.CreateDirectory(path: projectDirectory);
        var projectPath = Path.Combine(path1: projectDirectory, path2: $"{projectName}.csproj");
        var templatePath = Path.Combine(path1: projectDirectory, path2: "Templates", path3: "Models.tst");
        var sourcePath = Path.Combine(path1: projectDirectory, path2: "Models", path3: "Customer.cs");
        Directory.CreateDirectory(path: Path.GetDirectoryName(path: templatePath)!);
        Directory.CreateDirectory(path: Path.GetDirectoryName(path: sourcePath)!);

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
                      """).ConfigureAwait(continueOnCapturedContext: false);
        await File.WriteAllTextAsync(
            path: sourcePath,
            contents: """
                      namespace Sample;

                      public sealed class Customer
                      {
                          public required string DisplayName { get; init; }
                      }
                      """).ConfigureAwait(continueOnCapturedContext: false);
        await File.WriteAllTextAsync(
            path: templatePath,
            contents: """
                      // output: generated/models.ts
                      $Classes[$Name]
                      """).ConfigureAwait(continueOnCapturedContext: false);

        return new SampleProject(ProjectPath: projectPath, TemplatePath: templatePath, SourcePath: sourcePath);
    }

    private static LanguageServerSettings CreateSettings(
        string directory,
        string projectPath) =>
        new(
            RootPath: directory,
            WorkspacePath: directory,
            ProjectPath: projectPath,
            Framework: "net10.0",
            AllProjects: false);

    private static TextDocumentState CreateDocument(
        string path,
        string text) =>
        new(
            Uri: UriFromPath(path: path),
            Path: path,
            Text: text,
            Version: 1);

    private static LspPosition PositionOf(
        string text,
        string marker)
    {
        var offset = text.IndexOf(value: marker, comparisonType: StringComparison.Ordinal);
        offset.Should().BeGreaterThanOrEqualTo(0, because: $"marker should exist: {marker}");

        var line = 0;
        var character = 0;
        for (var index = 0; index < offset; index++)
        {
            if (text[index: index] == '\n')
            {
                line++;
                character = 0;
                continue;
            }

            character++;
        }

        return new LspPosition(Line: line, Character: character);
    }

    private static IReadOnlyList<DecodedSemanticToken> DecodeSemanticTokens(
        TextDocumentState document,
        LspSemanticTokens semanticTokens)
    {
        var decoded = new List<DecodedSemanticToken>();
        var line = 0;
        var character = 0;
        for (var index = 0; index < semanticTokens.Data.Length; index += 5)
        {
            line += semanticTokens.Data[index];
            character = semanticTokens.Data[index] == 0
                ? character + semanticTokens.Data[index + 1]
                : semanticTokens.Data[index + 1];
            var length = semanticTokens.Data[index + 2];
            var tokenType = semanticTokens.Data[index + 3];
            var offset = document.GetOffset(position: new LspPosition(Line: line, Character: character));
            decoded.Add(
                item: new DecodedSemanticToken(
                    Text: document.Text.Substring(startIndex: offset, length: length),
                    Type: TemplateSemanticTokenService.TokenTypes[tokenType]));
        }

        return decoded;
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
                await Task.Delay(millisecondsDelay: 100).ConfigureAwait(continueOnCapturedContext: false);
            }
            catch (UnauthorizedAccessException) when (attempt < MaxAttempts)
            {
                await Task.Delay(millisecondsDelay: 100).ConfigureAwait(continueOnCapturedContext: false);
            }
        }
    }

    private static string UriFromPath(string path) => new Uri(uriString: path).AbsoluteUri;

    private sealed record SampleProject(
        string ProjectPath,
        string TemplatePath,
        string SourcePath);

    private sealed record DecodedSemanticToken(
        string Text,
        string Type);
}
