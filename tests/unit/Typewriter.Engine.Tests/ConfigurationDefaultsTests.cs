using Typewriter.Abstractions;
using Typewriter.Configuration;
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
    public void RenderUsesConfiguredGuidType()
    {
        var metadata = CreateGuidIdMetadata();
        var document = new TemplateDocument(Path: "models.tst", Content: "$Classes[$Properties[$name: $Type = $Type[$Default];]]", OutputPath: "models.ts");
        var diagnostics = new List<GenerationDiagnostic>();
        var renderer = new TemplateRenderer(typeMapper: new TypeScriptTypeMapper());
        var defaults = TemplateRenderDefaults.FromConfiguration(
            configuration: CreateConfiguration(strictNull: true, guidType: "uuid"));

        var output = renderer.Render(template: document, metadata: metadata, diagnostics: diagnostics, defaults: defaults);

        diagnostics.Should().BeEmpty();
        output.Should().Contain("id: uuid = \"00000000-0000-0000-0000-000000000000\";");
    }

    [Fact]
    public void RenderUsesUint8ArrayGuidInitializerAutomatically()
    {
        var metadata = CreateGuidIdMetadata();
        var document = new TemplateDocument(Path: "models.tst", Content: "$Classes[$Properties[$name: $Type = $Type[$Default];]]", OutputPath: "models.ts");
        var diagnostics = new List<GenerationDiagnostic>();
        var renderer = new TemplateRenderer(typeMapper: new TypeScriptTypeMapper());
        var defaults = TemplateRenderDefaults.FromConfiguration(
            configuration: CreateConfiguration(strictNull: true, guidType: "Uint8Array"));

        var output = renderer.Render(template: document, metadata: metadata, diagnostics: diagnostics, defaults: defaults);

        diagnostics.Should().BeEmpty();
        output.Should().Contain("id: Uint8Array = new Uint8Array(16);");
    }

    [Fact]
    public void RenderUsesConfiguredGuidInitializer()
    {
        var metadata = CreateGuidIdMetadata();
        var document = new TemplateDocument(Path: "models.tst", Content: "$Classes[$Properties[$name: $Type = $Type[$Default];]]", OutputPath: "models.ts");
        var diagnostics = new List<GenerationDiagnostic>();
        var renderer = new TemplateRenderer(typeMapper: new TypeScriptTypeMapper());
        var defaults = TemplateRenderDefaults.FromConfiguration(
            configuration: CreateConfiguration(
                strictNull: true,
                guidType: "UUID",
                guidInitializer: "UUID.nil()"));

        var output = renderer.Render(template: document, metadata: metadata, diagnostics: diagnostics, defaults: defaults);

        diagnostics.Should().BeEmpty();
        output.Should().Contain("id: UUID = UUID.nil();");
    }

    [Fact]
    public void OutputConfigurationLegacyConstructorUsesDateTypeForDateOnlyType()
    {
        var output = new OutputConfiguration(
            Newline: "crlf",
            Encoding: "utf-8-bom",
            WriteOnlyWhenChanged: false,
            DryRun: true,
            FileNameConvention: FileNameConvention.Camel,
            StrictNull: false,
            IndentStyle: IndentStyle.Space,
            IndentSize: 2,
            InsertFinalNewline: true,
            TrimTrailingWhitespace: true,
            QuoteStyle: QuoteStyle.Single,
            DateType: "Dayjs",
            DecimalType: "Decimal");

        output.DateInitializer.Should().Be(TypeScriptTypeMapper.DefaultDateInitializer);
        output.DateOnlyType.Should().Be("Dayjs");
        output.DateOnlyInitializer.Should().Be(TypeScriptTypeMapper.DefaultDateOnlyInitializer);
        output.TimeOnlyType.Should().Be(TypeScriptTypeMapper.DefaultTimeOnlyType);
        output.TimeOnlyInitializer.Should().Be(TypeScriptTypeMapper.DefaultTimeOnlyInitializer);
        output.GuidType.Should().Be(TypeScriptTypeMapper.DefaultGuidType);
        output.GuidInitializer.Should().Be(TypeScriptTypeMapper.DefaultGuidInitializer);
        output.DecimalType.Should().Be("Decimal");
        output.DecimalInitializer.Should().Be(TypeScriptTypeMapper.DefaultDecimalInitializer);

        output.Deconstruct(
            out var newline,
            out _,
            out _,
            out _,
            out _,
            out _,
            out _,
            out _,
            out _,
            out _,
            out _,
            out var dateType,
            out var decimalType);

        newline.Should().Be("crlf");
        dateType.Should().Be("Dayjs");
        decimalType.Should().Be("Decimal");
    }

    [Fact]
    public void CompiledTypeDefaultValueUsesGuidDefault()
    {
        var metadata = CreateGuidIdMetadata();
        var diagnostics = new List<GenerationDiagnostic>();
        var document = TemplateDocument.Parse(
            template: new TemplateFile(
                Path: Path.Combine(path1: Path.GetTempPath(), path2: "compiled-guid-default.tst"),
                Content: """
                         ${
                             Template(Settings settings)
                             {
                                 settings.UseStringLiteralCharacter('\'');
                             }

                             string Defaults(Type type)
                             {
                                 return type.Default() + "|" + type.DefaultValue;
                             }
                         }
                         // output: compiled-guid-default.ts
                         $Classes[$Properties[$name=$Type[$Defaults];]]
                         """),
            diagnostics: diagnostics);
        var renderer = new TemplateRenderer(typeMapper: new TypeScriptTypeMapper());

        var output = renderer.Render(template: document, metadata: metadata, diagnostics: diagnostics);

        diagnostics.Should().BeEmpty();
        output.Should().Contain("id='00000000-0000-0000-0000-000000000000'|'00000000-0000-0000-0000-000000000000';");
    }

    [Fact]
    public void CompiledTypeDefaultValueUsesUint8ArrayGuidInitializer()
    {
        var metadata = CreateGuidIdMetadata();
        var diagnostics = new List<GenerationDiagnostic>();
        var document = TemplateDocument.Parse(
            template: new TemplateFile(
                Path: Path.Combine(path1: Path.GetTempPath(), path2: "compiled-uuid-default.tst"),
                Content: """
                         ${
                             Template(Settings settings)
                             {
                                 settings.UseGuidType("Uint8Array");
                             }

                             string Defaults(Type type)
                             {
                                 return type.Default() + "|" + type.DefaultValue;
                             }
                         }
                         // output: compiled-uuid-default.ts
                         $Classes[$Properties[$name=$Type[$Defaults];]]
                         """),
            diagnostics: diagnostics);
        var renderer = new TemplateRenderer(typeMapper: new TypeScriptTypeMapper());

        var output = renderer.Render(template: document, metadata: metadata, diagnostics: diagnostics);

        diagnostics.Should().BeEmpty();
        output.Should().Contain("id=new Uint8Array(16)|new Uint8Array(16);");
    }

    [Fact]
    public void TemplateOverrideUsesConfiguredGuidType()
    {
        var metadata = CreateGuidIdMetadata();
        var diagnostics = new List<GenerationDiagnostic>();
        var document = TemplateDocument.Parse(
            template: new TemplateFile(
                Path: Path.Combine(path1: Path.GetTempPath(), path2: "configured-guid.tst"),
                Content: """
                         ${
                             Template(Settings settings)
                             {
                                 settings.UseGuidType("UUID");
                                 settings.UseGuidInitializer("UUID.nil()");
                             }
                         }
                         // output: configured-guid.ts
                         $Classes[$Properties[$name: $Type = $Type[$Default];]]
                         """),
            diagnostics: diagnostics);
        var renderer = new TemplateRenderer(typeMapper: new TypeScriptTypeMapper());

        var output = renderer.Render(template: document, metadata: metadata, diagnostics: diagnostics);

        diagnostics.Should().BeEmpty();
        output.Should().Contain("id: UUID = UUID.nil();");
    }

    [Fact]
    public void RenderUsesConfiguredDateTypeForDefaults()
    {
        var metadata = CreateDateMetadata();
        var document = new TemplateDocument(Path: "models.tst", Content: "$Classes[$Properties[$name: $Type = $Type[$Default];]]", OutputPath: "models.ts");
        var diagnostics = new List<GenerationDiagnostic>();
        var renderer = new TemplateRenderer(typeMapper: new TypeScriptTypeMapper());
        var defaults = TemplateRenderDefaults.FromConfiguration(
            configuration: CreateConfiguration(strictNull: true, dateType: "Dayjs", dateInitializer: "dayjs()"));

        var output = renderer.Render(template: document, metadata: metadata, diagnostics: diagnostics, defaults: defaults);

        diagnostics.Should().BeEmpty();
        output.Should().Contain("createdAt: Dayjs = dayjs();");
    }

    [Fact]
    public void DateLibraryProfileMapsSemanticDateKinds()
    {
        var metadata = CreateSemanticDateMetadata();
        var document = new TemplateDocument(Path: "models.tst", Content: "$Classes[$Properties[$name: $Type = $Type[$Default];]]", OutputPath: "models.ts");
        var diagnostics = new List<GenerationDiagnostic>();
        var renderer = new TemplateRenderer(typeMapper: new TypeScriptTypeMapper());
        var defaults = TemplateRenderDefaults.FromConfiguration(
            configuration: CreateConfiguration(strictNull: true, dateLibrary: DateLibrary.JsJoda));

        var output = renderer.Render(template: document, metadata: metadata, diagnostics: diagnostics, defaults: defaults);

        diagnostics.Should().BeEmpty();
        output.Should().Contain("createdAt: LocalDateTime = LocalDateTime.now();");
        output.Should().Contain("submittedAt: Instant = Instant.now();");
        output.Should().Contain("birthDate: LocalDate = LocalDate.now();");
        output.Should().Contain("startsAt: LocalTime = LocalTime.now();");
        output.Should().Contain("elapsed: Duration = Duration.ZERO;");
        output.Should().Contain("billingPeriod: Period = Period.ZERO;");
        output.Should().Contain("reportingMonth: YearMonth = YearMonth.now();");
        output.Should().Contain("anniversary: MonthDay = MonthDay.now();");
    }

    [Fact]
    public void FrontendRuntimeTypeOverridesAmbiguousDateTimeSemantics()
    {
        var metadata = CreateSemanticDateMetadata(overrideCreatedAtAsInstant: true);
        var document = new TemplateDocument(Path: "models.tst", Content: "$Classes[$Properties[$name: $Type = $Type[$Default];]]", OutputPath: "models.ts");
        var diagnostics = new List<GenerationDiagnostic>();
        var renderer = new TemplateRenderer(typeMapper: new TypeScriptTypeMapper());
        var defaults = TemplateRenderDefaults.FromConfiguration(
            configuration: CreateConfiguration(strictNull: true, dateLibrary: DateLibrary.Temporal));

        var output = renderer.Render(template: document, metadata: metadata, diagnostics: diagnostics, defaults: defaults);

        diagnostics.Should().BeEmpty();
        output.Should().Contain("createdAt: Temporal.Instant = Temporal.Now.instant();");
        output.Should().Contain("billingPeriod: Temporal.Duration = Temporal.Duration.from(\"P0D\");");
    }

    [Fact]
    public void TemplateCanSelectDateLibraryAndExposeItsImports()
    {
        var metadata = CreateDateMetadata();
        var diagnostics = new List<GenerationDiagnostic>();
        var settings = new Settings();
        settings.UseDateLibrary(DateLibrary.Luxon);
        var document = TemplateDocument.Parse(
            template: new TemplateFile(
                Path: Path.Combine(path1: Path.GetTempPath(), path2: "date-library.tst"),
                Content: """
                         ${
                             Template(Settings settings)
                             {
                                 settings.UseDateLibrary(DateLibrary.Luxon);
                             }
                         }
                         // output: date-library.ts
                         $Classes[$Properties[$name: $Type = $Type[$Default];]]
                         """),
            diagnostics: diagnostics);
        var renderer = new TemplateRenderer(typeMapper: new TypeScriptTypeMapper());

        var output = renderer.Render(template: document, metadata: metadata, diagnostics: diagnostics);

        diagnostics.Should().BeEmpty();
        settings.DateLibraryImportsGeneration.Should().Be("import { DateTime, Duration } from 'luxon';");
        output.Should().Contain("createdAt: DateTime = DateTime.now();");
    }

    [Fact]
    public void TemplateOverrideUsesConfiguredDateTypeAndInitializer()
    {
        var metadata = CreateDateMetadata();
        var diagnostics = new List<GenerationDiagnostic>();
        var document = TemplateDocument.Parse(
            template: new TemplateFile(
                Path: Path.Combine(path1: Path.GetTempPath(), path2: "configured-date.tst"),
                Content: """
                         ${
                             Template(Settings settings)
                             {
                                 settings.UseDateType("DateTime");
                                 settings.UseDateInitializer("DateTime.now()");
                             }
                         }
                         // output: configured-date.ts
                         $Classes[$Properties[$name: $Type = $Type[$Default];]]
                         """),
            diagnostics: diagnostics);
        var renderer = new TemplateRenderer(typeMapper: new TypeScriptTypeMapper());

        var output = renderer.Render(template: document, metadata: metadata, diagnostics: diagnostics);

        diagnostics.Should().BeEmpty();
        output.Should().Contain("createdAt: DateTime = DateTime.now();");
    }

    [Fact]
    public void ConfiguredDateInitializerFlowsIntoCompiledTypeDefaultHelper()
    {
        var metadata = CreateDateMetadata();
        var diagnostics = new List<GenerationDiagnostic>();
        var document = TemplateDocument.Parse(
            template: new TemplateFile(
                Path: Path.Combine(path1: Path.GetTempPath(), path2: "compiled-date-default.tst"),
                Content: """
                         ${
                             Template(Settings settings)
                             {
                                 settings.UseDateType("Dayjs");
                                 settings.UseDateInitializer("dayjs()");
                             }

                             string Initializer(Type type)
                             {
                                 return type.Default();
                             }
                         }
                         // output: compiled-date-default.ts
                         $Classes[$Properties[$name: $Type = $Type[$Initializer];]]
                         """),
            diagnostics: diagnostics);
        var renderer = new TemplateRenderer(typeMapper: new TypeScriptTypeMapper());

        var output = renderer.Render(template: document, metadata: metadata, diagnostics: diagnostics);

        diagnostics.Should().BeEmpty();
        output.Should().Contain("createdAt: Dayjs = dayjs();");
    }

    [Fact]
    public void TemplateOverrideUsesConfiguredDateOnlyAndTimeOnlyTypesAndInitializers()
    {
        var metadata = CreateDateOnlyTimeOnlyMetadata();
        var diagnostics = new List<GenerationDiagnostic>();
        var document = TemplateDocument.Parse(
            template: new TemplateFile(
                Path: Path.Combine(path1: Path.GetTempPath(), path2: "configured-date-only-time-only.tst"),
                Content: """
                         ${
                             Template(Settings settings)
                             {
                                 settings.UseDateOnlyType("Temporal.PlainDate");
                                 settings.UseDateOnlyInitializer("Temporal.Now.plainDateISO()");
                                 settings.UseTimeOnlyType("Temporal.PlainTime");
                                 settings.UseTimeOnlyInitializer("Temporal.Now.plainTimeISO()");
                             }

                             string Initializer(Type type)
                             {
                                 return type.Default();
                             }
                         }
                         // output: configured-date-only-time-only.ts
                         $Classes[$Properties[$name: $Type = $Type[$Initializer];]]
                         """),
            diagnostics: diagnostics);
        var renderer = new TemplateRenderer(typeMapper: new TypeScriptTypeMapper());

        var output = renderer.Render(template: document, metadata: metadata, diagnostics: diagnostics);

        diagnostics.Should().BeEmpty();
        output.Should().Contain("birthDate: Temporal.PlainDate = Temporal.Now.plainDateISO();");
        output.Should().Contain("startsAt: Temporal.PlainTime = Temporal.Now.plainTimeISO();");
        output.Should().Contain("localDate: Temporal.PlainDate = Temporal.Now.plainDateISO();");
        output.Should().Contain("localTime: Temporal.PlainTime = Temporal.Now.plainTimeISO();");
    }

    [Fact]
    public void RenderUsesConfiguredQuoteStyleForDefaultTimeOnlyInitializer()
    {
        var metadata = CreateDateOnlyTimeOnlyMetadata();
        var document = new TemplateDocument(Path: "models.tst", Content: "$Classes[$Properties[$name = $Type[$Default];]]", OutputPath: "models.ts");
        var diagnostics = new List<GenerationDiagnostic>();
        var renderer = new TemplateRenderer(typeMapper: new TypeScriptTypeMapper());
        var defaults = TemplateRenderDefaults.FromConfiguration(
            configuration: CreateConfiguration(strictNull: true, quoteStyle: QuoteStyle.Single));

        var output = renderer.Render(template: document, metadata: metadata, diagnostics: diagnostics, defaults: defaults);

        diagnostics.Should().BeEmpty();
        output.Should().Contain("startsAt = '00:00:00';");
        output.Should().Contain("localTime = '00:00:00';");
    }

    [Fact]
    public void CompiledTypeDefaultUsesConfiguredQuoteStyleForDefaultTimeOnlyInitializer()
    {
        var metadata = CreateDateOnlyTimeOnlyMetadata();
        var diagnostics = new List<GenerationDiagnostic>();
        var document = TemplateDocument.Parse(
            template: new TemplateFile(
                Path: Path.Combine(path1: Path.GetTempPath(), path2: "compiled-time-only-default.tst"),
                Content: """
                         ${
                             Template(Settings settings)
                             {
                                 settings.UseStringLiteralCharacter('\'');
                             }

                             string Defaults(Type type)
                             {
                                 return type.Default() + "|" + type.DefaultValue;
                             }
                         }
                         // output: compiled-time-only-default.ts
                         $Classes[$Properties[$name=$Type[$Defaults];]]
                         """),
            diagnostics: diagnostics);
        var renderer = new TemplateRenderer(typeMapper: new TypeScriptTypeMapper());

        var output = renderer.Render(template: document, metadata: metadata, diagnostics: diagnostics);

        diagnostics.Should().BeEmpty();
        output.Should().Contain("startsAt='00:00:00'|'00:00:00';");
        output.Should().Contain("localTime='00:00:00'|'00:00:00';");
    }

    [Fact]
    public void RenderUsesConfiguredDecimalTypeForDefaults()
    {
        var metadata = CreateDecimalMetadata();
        var document = new TemplateDocument(Path: "models.tst", Content: "$Classes[$Properties[$name: $Type = $Type[$Default];]]", OutputPath: "models.ts");
        var diagnostics = new List<GenerationDiagnostic>();
        var renderer = new TemplateRenderer(typeMapper: new TypeScriptTypeMapper());
        var defaults = TemplateRenderDefaults.FromConfiguration(
            configuration: CreateConfiguration(strictNull: true, decimalType: "Decimal"));

        var output = renderer.Render(template: document, metadata: metadata, diagnostics: diagnostics, defaults: defaults);

        diagnostics.Should().BeEmpty();
        output.Should().Contain("amount: Decimal = new Decimal(0);");
    }

    [Fact]
    public void RenderUsesConfiguredDecimalInitializer()
    {
        var metadata = CreateDecimalMetadata();
        var document = new TemplateDocument(Path: "models.tst", Content: "$Classes[$Properties[$name: $Type = $Type[$Default];]]", OutputPath: "models.ts");
        var diagnostics = new List<GenerationDiagnostic>();
        var renderer = new TemplateRenderer(typeMapper: new TypeScriptTypeMapper());
        var defaults = TemplateRenderDefaults.FromConfiguration(
            configuration: CreateConfiguration(
                strictNull: true,
                decimalType: "Decimal",
                decimalInitializer: "Decimal.zero()"));

        var output = renderer.Render(template: document, metadata: metadata, diagnostics: diagnostics, defaults: defaults);

        diagnostics.Should().BeEmpty();
        output.Should().Contain("amount: Decimal = Decimal.zero();");
    }

    [Fact]
    public void TemplateOverrideUsesConfiguredDecimalType()
    {
        var metadata = CreateDecimalMetadata();
        var diagnostics = new List<GenerationDiagnostic>();
        var document = TemplateDocument.Parse(
            template: new TemplateFile(
                Path: Path.Combine(path1: Path.GetTempPath(), path2: "configured-decimal.tst"),
                Content: """
                         ${
                             Template(Settings settings)
                             {
                                 settings.UseDecimalType("Decimal");
                                 settings.UseDecimalInitializer("Decimal.zero()");
                             }
                         }
                         // output: configured-decimal.ts
                         $Classes[$Properties[$name: $Type = $Type[$Default];]]
                         """),
            diagnostics: diagnostics);
        var renderer = new TemplateRenderer(typeMapper: new TypeScriptTypeMapper());

        var output = renderer.Render(template: document, metadata: metadata, diagnostics: diagnostics);

        diagnostics.Should().BeEmpty();
        output.Should().Contain("amount: Decimal = Decimal.zero();");
    }

    [Fact]
    public void CompiledTypeDefaultValueUsesConfiguredDecimalInitializer()
    {
        var metadata = CreateDecimalMetadata();
        var diagnostics = new List<GenerationDiagnostic>();
        var document = TemplateDocument.Parse(
            template: new TemplateFile(
                Path: Path.Combine(path1: Path.GetTempPath(), path2: "compiled-decimal-default.tst"),
                Content: """
                         ${
                             Template(Settings settings)
                             {
                                 settings.UseDecimalType("Decimal");
                                 settings.UseDecimalInitializer("Decimal.zero()");
                             }

                             string Defaults(Type type)
                             {
                                 return type.Default() + "|" + type.DefaultValue;
                             }
                         }
                         // output: compiled-decimal-default.ts
                         $Classes[$Properties[$name=$Type[$Defaults];]]
                         """),
            diagnostics: diagnostics);
        var renderer = new TemplateRenderer(typeMapper: new TypeScriptTypeMapper());

        var output = renderer.Render(template: document, metadata: metadata, diagnostics: diagnostics);

        diagnostics.Should().BeEmpty();
        output.Should().Contain("amount=Decimal.zero()|Decimal.zero();");
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
        QuoteStyle quoteStyle = QuoteStyle.Double,
        string dateType = "Date",
        string dateInitializer = "new Date()",
        string dateOnlyType = "Date",
        string dateOnlyInitializer = "new Date()",
        string timeOnlyType = "string",
        string timeOnlyInitializer = "\"00:00:00\"",
        string guidType = "string",
        string guidInitializer = "auto",
        string decimalType = "number",
        string decimalInitializer = "auto",
        DateLibrary dateLibrary = DateLibrary.Legacy) =>
        TypewriterConfiguration.Default with
        {
            Output = TypewriterConfiguration.Default.Output with
            {
                StrictNull = strictNull,
                Encoding = encoding,
                QuoteStyle = quoteStyle,
                DateLibrary = dateLibrary,
                DateType = dateType,
                DateInitializer = dateInitializer,
                DateOnlyType = dateOnlyType,
                DateOnlyInitializer = dateOnlyInitializer,
                TimeOnlyType = timeOnlyType,
                TimeOnlyInitializer = timeOnlyInitializer,
                GuidType = guidType,
                GuidInitializer = guidInitializer,
                DecimalType = decimalType,
                DecimalInitializer = decimalInitializer,
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

    private static ProjectMetadata CreateDateMetadata()
    {
        var dateType = new TypeMetadataReference(
            Name: "DateTime",
            FullName: "System.DateTime",
            Namespace: "System",
            IsNullable: false,
            IsCollection: false,
            IsDictionary: false,
            IsEnum: false,
            IsPrimitive: false,
            IsDateLike: true,
            ElementType: null,
            TypeArguments: []);
        return new ProjectMetadata(
            ProjectPath: "Sample.csproj",
            SourceFiles: [],
            Types:
            [
                new TypeMetadata(
                    Name: "AuditInfo",
                    FullName: "Sample.AuditInfo",
                    Namespace: "Sample",
                    Kind: TypeMetadataKind.Class,
                    Accessibility: MetadataAccessibility.Public,
                    Properties:
                    [
                        new PropertyMetadata(
                            Name: "CreatedAt",
                            FullName: "Sample.AuditInfo.CreatedAt",
                            Type: dateType,
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

    private static ProjectMetadata CreateSemanticDateMetadata(bool overrideCreatedAtAsInstant = false)
    {
        static TypeMetadataReference DateType(string name, string fullName) =>
            new(
                Name: name,
                FullName: fullName,
                Namespace: fullName[..fullName.LastIndexOf(value: '.')],
                IsNullable: false,
                IsCollection: false,
                IsDictionary: false,
                IsEnum: false,
                IsPrimitive: false,
                IsDateLike: true,
                ElementType: null,
                TypeArguments: []);

        var instantOverride = new AttributeMetadata(
            Name: "FrontendRuntimeType",
            FullName: "AdaskoTheBeAsT.Typewriter.Annotations.FrontendRuntimeTypeAttribute",
            Arguments: [new AttributeArgumentMetadata(Name: null, Value: "3")]);
        var properties = new (string Name, TypeMetadataReference Type)[]
        {
            ("CreatedAt", DateType(name: "DateTime", fullName: "System.DateTime")),
            ("SubmittedAt", DateType(name: "DateTimeOffset", fullName: "System.DateTimeOffset")),
            ("BirthDate", DateType(name: "DateOnly", fullName: "System.DateOnly")),
            ("StartsAt", DateType(name: "TimeOnly", fullName: "System.TimeOnly")),
            ("Elapsed", DateType(name: "Duration", fullName: "NodaTime.Duration")),
            ("BillingPeriod", DateType(name: "Period", fullName: "NodaTime.Period")),
            ("ReportingMonth", DateType(name: "YearMonth", fullName: "NodaTime.YearMonth")),
            ("Anniversary", DateType(name: "AnnualDate", fullName: "NodaTime.AnnualDate")),
        };

        return new ProjectMetadata(
            ProjectPath: "Sample.csproj",
            SourceFiles: [],
            Types:
            [
                new TypeMetadata(
                    Name: "Schedule",
                    FullName: "Sample.Schedule",
                    Namespace: "Sample",
                    Kind: TypeMetadataKind.Class,
                    Accessibility: MetadataAccessibility.Public,
                    Properties: properties
                        .Select(
                            selector: item => new PropertyMetadata(
                                Name: item.Name,
                                FullName: $"Sample.Schedule.{item.Name}",
                                Type: item.Type,
                                Accessibility: MetadataAccessibility.Public,
                                HasGetter: true,
                                HasSetter: true,
                                IsRequired: false,
                                Attributes: overrideCreatedAtAsInstant && item.Name == "CreatedAt"
                                    ? [instantOverride]
                                    : []))
                        .ToArray(),
                    Attributes: [],
                    BaseTypes: [],
                    EnumValues: [],
                    IsNullableAware: true),
            ],
            Diagnostics: []);
    }

    private static ProjectMetadata CreateDateOnlyTimeOnlyMetadata()
    {
        var dateOnlyType = new TypeMetadataReference(
            Name: "DateOnly",
            FullName: "System.DateOnly",
            Namespace: "System",
            IsNullable: false,
            IsCollection: false,
            IsDictionary: false,
            IsEnum: false,
            IsPrimitive: false,
            IsDateLike: true,
            ElementType: null,
            TypeArguments: []);
        var timeOnlyType = dateOnlyType with
        {
            Name = "TimeOnly",
            FullName = "System.TimeOnly",
        };
        var localDateType = dateOnlyType with
        {
            Name = "LocalDate",
            FullName = "NodaTime.LocalDate",
            Namespace = "NodaTime",
        };
        var localTimeType = dateOnlyType with
        {
            Name = "LocalTime",
            FullName = "NodaTime.LocalTime",
            Namespace = "NodaTime",
        };
        return new ProjectMetadata(
            ProjectPath: "Sample.csproj",
            SourceFiles: [],
            Types:
            [
                new TypeMetadata(
                    Name: "Schedule",
                    FullName: "Sample.Schedule",
                    Namespace: "Sample",
                    Kind: TypeMetadataKind.Class,
                    Accessibility: MetadataAccessibility.Public,
                    Properties:
                    [
                        CreateProperty(name: "BirthDate", type: dateOnlyType),
                        CreateProperty(name: "StartsAt", type: timeOnlyType),
                        CreateProperty(name: "LocalDate", type: localDateType),
                        CreateProperty(name: "LocalTime", type: localTimeType),
                    ],
                    Attributes: [],
                    BaseTypes: [],
                    EnumValues: [],
                    IsNullableAware: true),
            ],
            Diagnostics: []);

        static PropertyMetadata CreateProperty(
            string name,
            TypeMetadataReference type) =>
            new(
                Name: name,
                FullName: "Sample.Schedule." + name,
                Type: type,
                Accessibility: MetadataAccessibility.Public,
                HasGetter: true,
                HasSetter: true,
                IsRequired: false,
                Attributes: []);
    }

    private static ProjectMetadata CreateDecimalMetadata()
    {
        var decimalType = new TypeMetadataReference(
            Name: "Decimal",
            FullName: "System.Decimal",
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
                    Name: "Payment",
                    FullName: "Sample.Payment",
                    Namespace: "Sample",
                    Kind: TypeMetadataKind.Class,
                    Accessibility: MetadataAccessibility.Public,
                    Properties:
                    [
                        new PropertyMetadata(
                            Name: "Amount",
                            FullName: "Sample.Payment.Amount",
                            Type: decimalType,
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
