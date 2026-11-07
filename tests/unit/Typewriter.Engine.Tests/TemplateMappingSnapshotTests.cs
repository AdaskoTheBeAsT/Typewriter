using AwesomeAssertions;
using Typewriter.Abstractions;
using Typewriter.Configuration;
using Typewriter.Extensions.Types;
using Xunit;
using CodeAttribute = Typewriter.CodeModel.Attribute;
using CodeClass = Typewriter.CodeModel.Class;
using CodeFile = Typewriter.CodeModel.File;
using CodeType = Typewriter.CodeModel.Type;

namespace Typewriter.Engine.Tests;

public sealed class TemplateMappingSnapshotTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HostConstructorsRetainPreConstructorFileMappings(bool acceptsFile)
    {
        var project = CreateProject();
        using var helper = CreateHelper(project: project, hostType: acceptsFile ? typeof(FileHost) : typeof(SettingsHost));

        helper.Settings.DateTypeGeneration.Should().Be("AfterDate");
        helper.TryInvoke(methodName: "DescribeFile", context: project, value: out var fileDescription, error: out var error).Should().BeTrue();
        error.Should().BeNull();
        fileDescription.Should().Be("BeforeDate:before():\"\"");
        helper.ResolveConfiguredOutputPath(metadata: project, error: out error).Should().Be("BeforeDate:before():\"\"");
        error.Should().BeNull();
        helper.TryInvoke(methodName: "DescribeClass", context: project.Types[0], value: out var classDescription, error: out error).Should().BeTrue();
        error.Should().BeNull();
        classDescription.Should().Be("AfterDate:after():''");

        var file = helper.AdapterFactory.CreateFile(project: project);
        AssertClassMappings(model: file.Classes.Single(predicate: item => item.Name == "Model"), prefix: "Before", quote: '"');
        if (acceptsFile)
        {
            helper.TryInvoke(methodName: "ConstructorDescription", context: project, value: out var constructorDescription, error: out error).Should().BeTrue();
            constructorDescription.Should().Be(fileDescription);
        }
    }

    [Fact]
    public void ParameterlessHostsAlsoReserveTheOriginalFileMappingBoundary()
    {
        var project = CreateProject();
        using var helper = CreateHelper(project: project, hostType: typeof(ParameterlessHost));

        Configure(settings: helper.Settings, prefix: "After", quote: '\'');

        var file = helper.AdapterFactory.CreateFile(project: project);
        Describe(file: file).Should().Be("BeforeDate:before():\"\"");
        AssertClassMappings(model: file.Classes.Single(predicate: item => item.Name == "Model"), prefix: "Before", quote: '"');
    }

    [Fact]
    public void DirectClassTypeAndAttributeAdaptationsCaptureTheirOwnBoundary()
    {
        var project = CreateProject();
        var settings = new Settings();
        Configure(settings: settings, prefix: "Before", quote: '"');
        var factory = new TemplateCodeModelAdapterFactory(metadata: project, settings: settings);
        var model = Adapt<CodeClass>(factory: factory, context: project.Types[0]);
        var type = Adapt<CodeType>(factory: factory, context: project.Types[0]);
        var attribute = Adapt<CodeAttribute>(factory: factory, context: project.Types[0].Attributes[0]);

        Configure(settings: settings, prefix: "After", quote: '\'');

        AssertClassMappings(model: model, prefix: "Before", quote: '"');
        AssertClassMappings(model: type.NestedClasses.Single(), prefix: "Before", quote: '"', nested: true);
        AssertDate(type: type.BaseClass!.Properties[0].Type, prefix: "Before");
        AssertDate(type: type.TypeArguments[0], prefix: "Before");
        AssertDate(type: type.Attributes.Single().Type!, prefix: "Before");
        AssertDate(type: attribute.Type!, prefix: "Before");
        AssertDate(type: attribute.Arguments.Single().Type!, prefix: "Before");
        AssertDate(type: attribute.Arguments.Single().TypeValue!, prefix: "Before");
        AssertClassMappings(model: Adapt<CodeClass>(factory: factory, context: project.Types[0]), prefix: "After", quote: '\'');
        model.NestedClasses.Single().Parent.Should().BeSameAs(model);
        model.NestedClasses.Single().Type.Parent.Should().BeSameAs(model);
        type.FileLocations.Should().Equal("Models.cs", "Partial.cs");
    }

    [Fact]
    public void ElementAndTypeArgumentsAreCapturedButMappedInterfacesRemainLive()
    {
        var contract = AdapterTestData.CreateType(name: "IContract") with
        {
            Kind = TypeMetadataKind.Interface,
            Properties = CreateProperties(),
        };
        var project = AdapterTestData.CreateProject(types: [contract]);
        var settings = new Settings();
        Configure(settings: settings, prefix: "Before", quote: '"');
        var factory = new TemplateCodeModelAdapterFactory(metadata: project, settings: settings);
        var reference = CreateCollection() with
        {
            Interfaces = [AdapterTestData.CreateReference(name: contract.Name, fullName: contract.FullName) with { IsPrimitive = false }],
        };
        var first = Adapt<CodeType>(factory: factory, context: reference);
        var interfacesBeforeMutation = first.Interfaces;
        var second = Adapt<CodeType>(factory: factory, context: reference);

        Configure(settings: settings, prefix: "After", quote: '\'');

        AssertDate(type: first.ElementType!, prefix: "Before");
        AssertDate(type: first.TypeArguments[0], prefix: "Before");
        AssertDate(type: interfacesBeforeMutation.Single().Properties[0].Type, prefix: "Before");
        AssertDate(type: second.Interfaces.Single().Properties[0].Type, prefix: "After");
        first.Settings.Should().BeSameAs(settings);
    }

    [Fact]
    public void LegacyDefaultExtensionsStayLiveWhileResolvedDefaultsStayCaptured()
    {
        var settings = new Settings();
        Configure(settings: settings, prefix: "Before", quote: '"');
        var factory = new TemplateCodeModelAdapterFactory(metadata: CreateProject(), settings: settings);
        var date = Adapt<CodeType>(factory: factory, context: CreateDateReference());
        var resolved = Adapt<CodeType>(
            factory: factory,
            context: new TypeMappingContext(Reference: CreateDateReference(), RuntimeType: FrontendRuntimeTypeKind.String));

        Configure(settings: settings, prefix: "After", quote: '\'');

        date.DefaultValue.Should().Be("before()");
        date.Default().Should().Be("after()");
        resolved.DefaultValue.Should().Be("\"\"");
        resolved.Default().Should().Be("\"\"");
        resolved.Settings.Should().BeSameAs(settings);
    }

    [Fact]
    public void DateLibraryResolvedDefaultFlagUsesTheOriginalMappingSettings()
    {
        var settings = new Settings();
        settings.UseDateLibrary(library: DateLibrary.Temporal);
        var factory = new TemplateCodeModelAdapterFactory(metadata: CreateProject(), settings: settings);
        var collection = Adapt<CodeType>(factory: factory, context: CreateCollection());
        var originalDefault = settings.PlainDateTimeInitializerGeneration;

        settings.UseDateLibrary(library: DateLibrary.Legacy);
        settings.UseDateInitializer(dateInitializer: "after()");

        var element = collection.ElementType!;
        element.DefaultValue.Should().Be(originalDefault);
        element.Default().Should().Be(originalDefault);
        element.Settings.Should().BeSameAs(settings);
    }

    [Fact]
    public void DeferredTypeArgumentsCaptureAllScalarMappingsAndInitializers()
    {
        var settings = new Settings();
        ConfigureScalars(settings: settings, prefix: "Before");
        var reference = AdapterTestData.CreateReference(name: "Scalars", fullName: "Sample.Scalars") with
        {
            IsPrimitive = false,
            TypeArguments =
            [
                AdapterTestData.CreateReference(name: "Guid", fullName: "System.Guid"),
                AdapterTestData.CreateReference(name: "Decimal", fullName: "System.Decimal"),
                AdapterTestData.CreateReference(name: "DateOnly", fullName: "System.DateOnly"),
                AdapterTestData.CreateReference(name: "TimeOnly", fullName: "System.TimeOnly"),
                CreateDateReference() with { IsNullable = true },
            ],
        };
        var factory = new TemplateCodeModelAdapterFactory(metadata: CreateProject(), settings: settings);
        var type = Adapt<CodeType>(factory: factory, context: reference);

        ConfigureScalars(settings: settings, prefix: "After");
        settings.DisableStrictNullGeneration();

        type.TypeArguments.Select(selector: item => item.Name).Should().Equal("BeforeGuid", "BeforeDecimal", "BeforeDateOnly", "BeforeTimeOnly", "Date");
        type.TypeArguments.Select(selector: item => item.DefaultValue).Should().Equal("BeforeGuid()", "BeforeDecimal()", "BeforeDateOnly()", "BeforeTimeOnly()", "null");
        type.TypeArguments[4].IsNullable.Should().BeTrue();
    }

    [Fact]
    public void SettingsSubclassesPreserveEagerVirtualGetterTimingAndOrder()
    {
        var settings = new SequencedSettings();
        var factory = new TemplateCodeModelAdapterFactory(metadata: AdapterTestData.CreateProject(types: []), settings: settings);
        var type = Adapt<CodeType>(factory: factory, context: CreateCollection());

        settings.Reads.Should().Be(3);
        settings.Prefix = "Changed";

        type.TypeArguments[0].Name.Should().Be("Initial1");
        type.Name.Should().Be("Initial2[]");
        type.ElementType!.Name.Should().Be("Initial3");
        type.Settings.Should().BeSameAs(settings);
        settings.Reads.Should().Be(3);
    }

    [Fact]
    public void SettingsSubclassesEagerlyBuildNestedBaseContainingAndAttributeViews()
    {
        var settings = new SequencedSettings();
        var project = CreateProject();
        var factory = new TemplateCodeModelAdapterFactory(metadata: project, settings: settings);
        var model = Adapt<CodeClass>(factory: factory, context: project.Types[0]);
        var readsAfterAdaptation = settings.Reads;
        readsAfterAdaptation.Should().BePositive();

        settings.Prefix = "Changed";

        model.NestedClasses.Single().Properties[0].Type.Name.Should().StartWith("Initial");
        model.NestedClasses.Single().ContainingClass!.TypeArguments[0].Name.Should().StartWith("Initial");
        model.BaseClass!.Properties[0].Type.Name.Should().StartWith("Initial");
        model.Attributes.Single().Type!.Name.Should().StartWith("Initial");
        model.TypeArguments[0].Name.Should().StartWith("Initial");
        settings.Reads.Should().Be(readsAfterAdaptation);
    }

    [Fact]
    public void PreparingFilesWithCustomSettingsRemainsEager()
    {
        var settings = new SequencedSettings();
        var project = CreateProject();
        var factory = new TemplateCodeModelAdapterFactory(metadata: project, settings: settings);

        factory.PrepareFile(project: project);
        var readsBeforeConstructor = settings.Reads;
        settings.Prefix = "Changed";
        var file = factory.CreateFile(project: project);

        file.Classes.Single(predicate: item => item.Name == "Model").NestedClasses.Single().Properties[0].Type.Name.Should().StartWith("Initial");
        settings.Reads.Should().Be(readsBeforeConstructor);
    }

    private static CompiledTemplateHelper CreateHelper(ProjectMetadata project, System.Type hostType) =>
        TemplateRuntimeCompiler.CreateHelper(
            templatePath: "models.tst",
            metadata: project,
            diagnostics: [],
            defaults: TemplateRenderDefaults.Default with
            {
                DateTypeGeneration = "BeforeDate",
                DateInitializerGeneration = "before()",
            },
            hostType: hostType,
            loadContext: new TemplateAssemblyLoadContext(name: "SnapshotTests", referencePaths: []),
            unloadLoadContextOnDispose: true,
            metadataIndex: ProjectMetadataIndex.Create(metadata: project));

    private static T Adapt<T>(TemplateCodeModelAdapterFactory factory, object context)
        where T : Typewriter.CodeModel.Item
    {
        factory.TryAdapt(context: context, targetType: typeof(T), adapted: out var adapted).Should().BeTrue();
        return adapted.Should().BeAssignableTo<T>().Which;
    }

    private static void Configure(Settings settings, string prefix, char quote)
    {
        settings.UseDateType(dateType: prefix + "Date");
        settings.UseDateInitializer(dateInitializer: prefix.Equals(value: "Before", comparisonType: StringComparison.Ordinal) ? "before()" : "after()");
        settings.UseStringLiteralCharacter(ch: quote);
    }

    private static void ConfigureScalars(Settings settings, string prefix)
    {
        settings.UseGuidType(guidType: prefix + "Guid");
        settings.UseGuidInitializer(guidInitializer: prefix + "Guid()");
        settings.UseDecimalType(decimalType: prefix + "Decimal");
        settings.UseDecimalInitializer(decimalInitializer: prefix + "Decimal()");
        settings.UseDateOnlyType(dateOnlyType: prefix + "DateOnly");
        settings.UseDateOnlyInitializer(dateOnlyInitializer: prefix + "DateOnly()");
        settings.UseTimeOnlyType(timeOnlyType: prefix + "TimeOnly");
        settings.UseTimeOnlyInitializer(timeOnlyInitializer: prefix + "TimeOnly()");
    }

    private static TypeMetadataReference CreateDateReference() =>
        AdapterTestData.CreateReference(name: "DateTime", fullName: "System.DateTime") with { IsDateLike = true, IsPrimitive = false };

    private static TypeMetadataReference CreateCollection() =>
        AdapterTestData.CreateReference(name: "List", fullName: "System.Collections.Generic.List") with
        {
            IsPrimitive = false,
            IsCollection = true,
            ElementType = CreateDateReference(),
            TypeArguments = [CreateDateReference()],
        };

    private static IReadOnlyList<PropertyMetadata> CreateProperties() =>
        [CreateProperty(name: "Date", type: CreateDateReference()), CreateProperty(name: "Text", type: AdapterTestData.CreateReference())];

    private static PropertyMetadata CreateProperty(string name, TypeMetadataReference type) =>
        new(
            Name: name,
            FullName: "Sample.Model." + name,
            Type: type,
            Accessibility: MetadataAccessibility.Public,
            HasGetter: true,
            HasSetter: true,
            IsRequired: false,
            Attributes: []);

    private static ProjectMetadata CreateProject()
    {
        var argument = new AttributeArgumentMetadata(Name: "Date", Value: "value")
        {
            Type = CreateDateReference(),
            TypeValue = CreateDateReference(),
        };
        var attribute = new AttributeMetadata(Name: "Example", FullName: "Sample.Example", Arguments: [argument]) { Type = CreateDateReference() };
        var nested = AdapterTestData.CreateType(name: "Nested") with
        {
            FullName = "Sample.Model.Nested",
            ContainingTypeFullName = "Sample.Model",
            Properties = CreateProperties(),
        };
        var baseType = AdapterTestData.CreateType(name: "Base") with { Properties = CreateProperties() };
        var model = AdapterTestData.CreateType() with
        {
            Properties = CreateProperties(),
            Attributes = [attribute],
            TypeArguments = [CreateDateReference()],
            NestedClasses = [nested],
            BaseTypes = [AdapterTestData.CreateReference(name: baseType.Name, fullName: baseType.FullName) with { IsPrimitive = false }],
            FileLocations = ["Models.cs", "Partial.cs"],
        };
        var source = new SourceFileMetadata(Path: "Models.cs", Types: [model, baseType]);
        return AdapterTestData.CreateProject(types: [model, baseType], sources: [source]);
    }

    private static void AssertClassMappings(CodeClass model, string prefix, char quote, bool nested = false)
    {
        AssertDate(type: model.Properties[0].Type, prefix: prefix);
        model.Properties[1].Type.DefaultValue.Should().Be($"{quote}{quote}");
        if (nested)
        {
            AssertDate(type: model.ContainingClass!.TypeArguments[0], prefix: prefix);
            return;
        }

        AssertClassMappings(model: model.NestedClasses.Single(), prefix: prefix, quote: quote, nested: true);
        AssertDate(type: model.BaseClass!.Properties[0].Type, prefix: prefix);
        AssertDate(type: model.Type.BaseClass!.Properties[0].Type, prefix: prefix);
        AssertDate(type: model.TypeArguments[0], prefix: prefix);
        AssertDate(type: model.Attributes.Single().Type!, prefix: prefix);
        AssertDate(type: model.Attributes.Single().Arguments[0].TypeValue!, prefix: prefix);
    }

    private static void AssertDate(CodeType type, string prefix)
    {
        type.Name.Should().Be(prefix + "Date");
        type.DefaultValue.Should().Be(prefix.Equals(value: "Before", comparisonType: StringComparison.Ordinal) ? "before()" : "after()");
    }

    private static string Describe(CodeFile file) =>
        Describe(model: file.Classes.Single(predicate: item => item.Name == "Model").NestedClasses.Single());

    private static string Describe(CodeClass model) =>
        $"{model.Properties[0].Type.Name}:{model.Properties[0].Type.DefaultValue}:{model.Properties[1].Type.DefaultValue}";

#pragma warning disable S1144 // Template host constructors and named helpers are invoked through reflection.
    private sealed class FileHost
    {
        private readonly string _constructorDescription;
        private readonly CodeFile _file;

        public FileHost(Settings settings, CodeFile file)
        {
            Configure(settings: settings, prefix: "After", quote: '\'');
            _constructorDescription = Describe(file: file);
            _file = file;
            settings.OutputFilenameFactory = Describe;
        }

        public static string DescribeFile(CodeFile file) => Describe(file: file);

        public static string DescribeClass(CodeClass model) => Describe(model: model.NestedClasses.Single());

        public string ConstructorDescription(CodeFile file)
        {
            file.Should().BeSameAs(_file);
            return _constructorDescription;
        }
    }

    private sealed class SettingsHost
    {
        private SettingsHost(Settings settings)
        {
            Configure(settings: settings, prefix: "After", quote: '\'');
            settings.OutputFilenameFactory = Describe;
        }

        public static string DescribeFile(CodeFile file) => Describe(file: file);

        public static string DescribeClass(CodeClass model) => Describe(model: model.NestedClasses.Single());
    }
#pragma warning restore S1144

#pragma warning disable S2094 // An empty host verifies the pre-constructor File boundary for parameterless templates.
    private sealed class ParameterlessHost
    {
    }
#pragma warning restore S2094

    private sealed class SequencedSettings : Settings
    {
        public string Prefix { get; set; } = "Initial";

        public int Reads { get; private set; }

        public override string DateTypeGeneration => Prefix + (++Reads).ToString(provider: System.Globalization.CultureInfo.InvariantCulture);
    }
}
