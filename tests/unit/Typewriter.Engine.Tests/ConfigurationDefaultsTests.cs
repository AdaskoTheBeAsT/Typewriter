using Typewriter.Abstractions;
using Typewriter.Engine;
using Xunit;

namespace Typewriter.Engine.Tests;

public sealed class ConfigurationDefaultsTests
{
    [Fact]
    public void RenderHonorsStrictNullDisabledFromConfiguration()
    {
        var metadata = CreateNullableEmailMetadata();
        var document = new TemplateDocument(Path: "models.tst", Content: "$Classes[$Properties[$name: $Type;]]", OutputPath: "models.ts");
        var diagnostics = new List<GenerationDiagnostic>();
        var renderer = new TemplateRenderer(typeMapper: new TypeScriptTypeMapper());
        var defaults = TemplateRenderDefaults.FromConfiguration(configuration: CreateConfiguration(strictNull: false));

        var output = renderer.Render(template: document, metadata: metadata, diagnostics: diagnostics, defaults: defaults);

        diagnostics.Should().BeEmpty();
        output.Should().Contain("email: string;");
        output.Should().NotContain("| null");
    }

    [Fact]
    public void RenderKeepsStrictNullByDefault()
    {
        var metadata = CreateNullableEmailMetadata();
        var document = new TemplateDocument(Path: "models.tst", Content: "$Classes[$Properties[$name: $Type;]]", OutputPath: "models.ts");
        var diagnostics = new List<GenerationDiagnostic>();
        var renderer = new TemplateRenderer(typeMapper: new TypeScriptTypeMapper());

        var output = renderer.Render(template: document, metadata: metadata, diagnostics: diagnostics);

        diagnostics.Should().BeEmpty();
        output.Should().Contain("email: string | null;");
    }

    [Fact]
    public void ConfigurationStrictNullFlowsIntoCompiledTemplateSettings()
    {
        var metadata = CreateNullableEmailMetadata();
        var diagnostics = new List<GenerationDiagnostic>();
        var document = TemplateDocument.Parse(
            template: new TemplateFile(
                Path: Path.Combine(path1: Path.GetTempPath(), path2: "models.tst"),
                Content: """
                         ${
                             Template(Settings settings)
                             {
                             }
                         }
                         // output: models.ts
                         $Classes[$Properties[$name: $Type;]]
                         """),
            diagnostics: diagnostics);
        var renderer = new TemplateRenderer(typeMapper: new TypeScriptTypeMapper());
        var defaults = TemplateRenderDefaults.FromConfiguration(configuration: CreateConfiguration(strictNull: false));

        var output = renderer.Render(template: document, metadata: metadata, diagnostics: diagnostics, defaults: defaults);

        diagnostics.Should().BeEmpty();
        output.Should().Contain("email: string;");
        output.Should().NotContain("| null");
    }

    [Fact]
    public void TemplateOverrideDisablesStrictNullGeneration()
    {
        var metadata = CreateNullableEmailMetadata();
        var diagnostics = new List<GenerationDiagnostic>();
        var document = TemplateDocument.Parse(
            template: new TemplateFile(
                Path: Path.Combine(path1: Path.GetTempPath(), path2: "models.tst"),
                Content: """
                         ${
                             Template(Settings settings)
                             {
                                 settings.DisableStrictNullGeneration();
                             }
                         }
                         // output: models.ts
                         $Classes[$Properties[$name: $Type;]]
                         """),
            diagnostics: diagnostics);
        var renderer = new TemplateRenderer(typeMapper: new TypeScriptTypeMapper());

        var output = renderer.Render(template: document, metadata: metadata, diagnostics: diagnostics, defaults: TemplateRenderDefaults.FromConfiguration(configuration: CreateConfiguration(strictNull: true)));

        diagnostics.Should().BeEmpty();
        output.Should().Contain("email: string;");
        output.Should().NotContain("| null");
    }

    [Fact]
    public void RenderResultCarriesUtf8BomFromConfigurationAndTemplateOverride()
    {
        var metadata = CreateNullableEmailMetadata();
        var renderer = new TemplateRenderer(typeMapper: new TypeScriptTypeMapper());
        var bomDefaults = TemplateRenderDefaults.FromConfiguration(
            configuration: CreateConfiguration(strictNull: true, encoding: "utf-8-bom"));

        var configuredDiagnostics = new List<GenerationDiagnostic>();
        var configuredDocument = TemplateDocument.Parse(
            template: new TemplateFile(
                Path: Path.Combine(path1: Path.GetTempPath(), path2: "configured.tst"),
                Content: """
                         ${
                             Template(Settings settings)
                             {
                             }
                         }
                         // output: configured.ts
                         $Classes[$Name]
                         """),
            diagnostics: configuredDiagnostics);
        var configuredResult = renderer.RenderTemplate(template: configuredDocument, metadata: metadata, diagnostics: configuredDiagnostics, defaults: bomDefaults);

        var overriddenDiagnostics = new List<GenerationDiagnostic>();
        var overriddenDocument = TemplateDocument.Parse(
            template: new TemplateFile(
                Path: Path.Combine(path1: Path.GetTempPath(), path2: "overridden.tst"),
                Content: """
                         ${
                             Template(Settings settings)
                             {
                                 settings.DisableUtf8BomGeneration();
                             }
                         }
                         // output: overridden.ts
                         $Classes[$Name]
                         """),
            diagnostics: overriddenDiagnostics);
        var overriddenResult = renderer.RenderTemplate(template: overriddenDocument, metadata: metadata, diagnostics: overriddenDiagnostics, defaults: bomDefaults);

        configuredDiagnostics.Should().BeEmpty();
        overriddenDiagnostics.Should().BeEmpty();
        configuredResult.Utf8Bom.Should().BeTrue();
        overriddenResult.Utf8Bom.Should().BeFalse();
    }

    [Fact]
    public void RenderUsesConfiguredQuoteStyleForDefaults()
    {
        var metadata = CreateGuidIdMetadata();
        var document = new TemplateDocument(Path: "models.tst", Content: "$Classes[$Properties[$name = $Type[$Default];]]", OutputPath: "models.ts");
        var diagnostics = new List<GenerationDiagnostic>();
        var renderer = new TemplateRenderer(typeMapper: new TypeScriptTypeMapper());
        var defaults = TemplateRenderDefaults.FromConfiguration(
            configuration: CreateConfiguration(strictNull: true, quoteStyle: QuoteStyle.Single));

        var output = renderer.Render(template: document, metadata: metadata, diagnostics: diagnostics, defaults: defaults);

        diagnostics.Should().BeEmpty();
        output.Should().Contain("id = '00000000-0000-0000-0000-000000000000';");
    }

    [Fact]
    public void ConfiguredQuoteStyleFlowsIntoCompiledTemplateAndTemplateOverrideWins()
    {
        var metadata = CreateGuidIdMetadata();
        var renderer = new TemplateRenderer(typeMapper: new TypeScriptTypeMapper());
        var backtickDefaults = TemplateRenderDefaults.FromConfiguration(
            configuration: CreateConfiguration(strictNull: true, quoteStyle: QuoteStyle.Backtick));

        var configuredDiagnostics = new List<GenerationDiagnostic>();
        var configuredDocument = TemplateDocument.Parse(
            template: new TemplateFile(
                Path: Path.Combine(path1: Path.GetTempPath(), path2: "configured-quotes.tst"),
                Content: """
                         ${
                             Template(Settings settings)
                             {
                             }
                         }
                         // output: configured-quotes.ts
                         $Classes[$Properties[$name = $Type[$Default];]]
                         """),
            diagnostics: configuredDiagnostics);
        var configuredOutput = renderer.Render(template: configuredDocument, metadata: metadata, diagnostics: configuredDiagnostics, defaults: backtickDefaults);

        var overriddenDiagnostics = new List<GenerationDiagnostic>();
        var overriddenDocument = TemplateDocument.Parse(
            template: new TemplateFile(
                Path: Path.Combine(path1: Path.GetTempPath(), path2: "overridden-quotes.tst"),
                Content: """
                         ${
                             Template(Settings settings)
                             {
                                 settings.UseStringLiteralCharacter('\'');
                             }
                         }
                         // output: overridden-quotes.ts
                         $Classes[$Properties[$name = $Type[$Default];]]
                         """),
            diagnostics: overriddenDiagnostics);
        var overriddenOutput = renderer.Render(template: overriddenDocument, metadata: metadata, diagnostics: overriddenDiagnostics, defaults: backtickDefaults);

        configuredDiagnostics.Should().BeEmpty();
        overriddenDiagnostics.Should().BeEmpty();
        configuredOutput.Should().Contain("id = `00000000-0000-0000-0000-000000000000`;");
        overriddenOutput.Should().Contain("id = '00000000-0000-0000-0000-000000000000';");
    }

    [Fact]
    public void TemplateLogIsSurfacedAsDiagnostics()
    {
        var metadata = CreateNullableEmailMetadata();
        var diagnostics = new List<GenerationDiagnostic>();
        var templatePath = Path.Combine(path1: Path.GetTempPath(), path2: "logging.tst");
        var document = TemplateDocument.Parse(
            template: new TemplateFile(
                Path: templatePath,
                Content: """
                         ${
                             Template(Settings settings)
                             {
                                 settings.Log.LogInfo("hello {0}", "world");
                                 settings.Log.LogWarning("careful");
                             }
                         }
                         // output: logging.ts
                         $Classes[$Name]
                         """),
            diagnostics: diagnostics);
        var renderer = new TemplateRenderer(typeMapper: new TypeScriptTypeMapper());

        _ = renderer.Render(template: document, metadata: metadata, diagnostics: diagnostics);

        diagnostics.Should().Contain(
            diagnostic => diagnostic.Severity == DiagnosticSeverity.Info
                          && diagnostic.Code == "TW0007"
                          && diagnostic.File == templatePath
                          && diagnostic.Message == "hello world");
        diagnostics.Should().Contain(
            diagnostic => diagnostic.Severity == DiagnosticSeverity.Warning
                          && diagnostic.Code == "TW0007"
                          && diagnostic.Message == "careful");
    }

    [Fact]
    public async Task WriterRespectsTemplateBomOverride()
    {
        var directory = Directory.CreateTempSubdirectory(prefix: "typewriter-bom-tests").FullName;
        try
        {
            var writer = new FileSystemGeneratedFileWriter();

            var bomDisabledPath = Path.Combine(path1: directory, path2: "no-bom.ts");
            await writer.WriteAsync(
                file: new GeneratedFile(Path: bomDisabledPath, Content: "export const a = 1;", Changed: true, Utf8Bom: false),
                request: CreateRequest(directory: directory, encoding: "utf-8-bom"),
                cancellationToken: CancellationToken.None);

            var bomEnabledPath = Path.Combine(path1: directory, path2: "bom.ts");
            await writer.WriteAsync(
                file: new GeneratedFile(Path: bomEnabledPath, Content: "export const a = 1;", Changed: true, Utf8Bom: true),
                request: CreateRequest(directory: directory, encoding: "utf-8"),
                cancellationToken: CancellationToken.None);

            var configuredBomPath = Path.Combine(path1: directory, path2: "configured-bom.ts");
            await writer.WriteAsync(
                file: new GeneratedFile(Path: configuredBomPath, Content: "export const a = 1;", Changed: true),
                request: CreateRequest(directory: directory, encoding: "utf-8-bom"),
                cancellationToken: CancellationToken.None);

            StartsWithUtf8Bom(bytes: await File.ReadAllBytesAsync(path: bomDisabledPath)).Should().BeFalse();
            StartsWithUtf8Bom(bytes: await File.ReadAllBytesAsync(path: bomEnabledPath)).Should().BeTrue();
            StartsWithUtf8Bom(bytes: await File.ReadAllBytesAsync(path: configuredBomPath)).Should().BeTrue();
        }
        finally
        {
            Directory.Delete(path: directory, recursive: true);
        }
    }

    private static bool StartsWithUtf8Bom(byte[] bytes) =>
        bytes.Length >= 3
        && bytes[0] == 0xEF
        && bytes[1] == 0xBB
        && bytes[2] == 0xBF;

    private static GenerationRequest CreateRequest(
        string directory,
        string encoding) =>
        new(
            WorkspacePath: directory,
            ProjectPath: null,
            TemplatePath: null,
            Mode: GenerationMode.Generate,
            Configuration: CreateConfiguration(strictNull: true, encoding: encoding));

    private static TypewriterConfiguration CreateConfiguration(
        bool strictNull,
        string encoding = "utf-8",
        QuoteStyle quoteStyle = QuoteStyle.Double) =>
        TypewriterConfiguration.Default with
        {
            Output = TypewriterConfiguration.Default.Output with
            {
                StrictNull = strictNull,
                Encoding = encoding,
                QuoteStyle = quoteStyle,
            },
        };

    private static ProjectMetadata CreateGuidIdMetadata()
    {
        var guidType = new TypeMetadataReference(
            Name: "Guid",
            FullName: "System.Guid",
            Namespace: "System",
            IsNullable: false,
            IsCollection: false,
            IsDictionary: false,
            IsEnum: false,
            IsPrimitive: true,
            IsDateLike: false,
            ElementType: null,
            TypeArguments: []);
        return new ProjectMetadata(
            ProjectPath: "Sample.csproj",
            SourceFiles: [],
            Types:
            [
                new TypeMetadata(
                    Name: "User",
                    FullName: "Sample.User",
                    Namespace: "Sample",
                    Kind: TypeMetadataKind.Class,
                    Accessibility: MetadataAccessibility.Public,
                    Properties:
                    [
                        new PropertyMetadata(
                            Name: "Id",
                            FullName: "Sample.User.Id",
                            Type: guidType,
                            Accessibility: MetadataAccessibility.Public,
                            HasGetter: true,
                            HasSetter: true,
                            IsRequired: false,
                            Attributes: []),
                    ],
                    Attributes: [],
                    BaseTypes: [],
                    EnumValues: [],
                    IsNullableAware: true),
            ],
            Diagnostics: []);
    }

    private static ProjectMetadata CreateNullableEmailMetadata()
    {
        var stringType = new TypeMetadataReference(
            Name: "String",
            FullName: "System.String",
            Namespace: "System",
            IsNullable: false,
            IsCollection: false,
            IsDictionary: false,
            IsEnum: false,
            IsPrimitive: true,
            IsDateLike: false,
            ElementType: null,
            TypeArguments: []);
        var nullableStringType = stringType with { IsNullable = true };
        return new ProjectMetadata(
            ProjectPath: "Sample.csproj",
            SourceFiles: [],
            Types:
            [
                new TypeMetadata(
                    Name: "User",
                    FullName: "Sample.User",
                    Namespace: "Sample",
                    Kind: TypeMetadataKind.Class,
                    Accessibility: MetadataAccessibility.Public,
                    Properties:
                    [
                        new PropertyMetadata(
                            Name: "Email",
                            FullName: "Sample.User.Email",
                            Type: nullableStringType,
                            Accessibility: MetadataAccessibility.Public,
                            HasGetter: true,
                            HasSetter: true,
                            IsRequired: false,
                            Attributes: []),
                    ],
                    Attributes: [],
                    BaseTypes: [],
                    EnumValues: [],
                    IsNullableAware: true),
            ],
            Diagnostics: []);
    }
}
