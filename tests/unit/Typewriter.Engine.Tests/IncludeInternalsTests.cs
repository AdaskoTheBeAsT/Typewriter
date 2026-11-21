using AwesomeAssertions;
using Typewriter.Abstractions;
using Typewriter.Configuration;
using Typewriter.Engine;
using Xunit;
using CodeClass = Typewriter.CodeModel.Class;
using CodeFile = Typewriter.CodeModel.File;
using CodeInterface = Typewriter.CodeModel.Interface;
using CodeRecord = Typewriter.CodeModel.Record;
using CodeStruct = Typewriter.CodeModel.Struct;
using CodeType = Typewriter.CodeModel.Type;

namespace Typewriter.Engine.Tests;

[Collection("Compiled template cache")]
public sealed class IncludeInternalsTests
{
    [Fact]
    public void SettingsIncludeInternalsDefaultsToFalseAndSupportsFluentToggle()
    {
        var settings = new Settings();
        settings.IncludeInternalsGeneration.Should().BeFalse();

        settings.IncludeInternals().Should().BeSameAs(settings);
        settings.IncludeInternalsGeneration.Should().BeTrue();

        settings.IncludeInternals(includeInternals: false).Should().BeSameAs(settings);
        settings.IncludeInternalsGeneration.Should().BeFalse();
    }

    [Theory]
    [InlineData(false, "PubClass|PubRecord|PubStruct|IPubContract|PubEnum|PubCallback|PubClass,PubRecord,PubStruct,IPubContract,PubEnum")]
    [InlineData(true, "PubClass,IntClass|PubRecord,IntRecord|PubStruct,IntStruct|IPubContract,IIntContract|PubEnum,IntEnum|PubCallback,IntCallback|PubClass,IntClass,PubRecord,IntRecord,PubStruct,IntStruct,IPubContract,IIntContract,PubEnum,IntEnum")]
    public void TopLevelCollectionsHonorIncludeInternalsInTemplatesAndCodeModel(
        bool includeInternals,
        string expected)
    {
        var metadata = CreateTopLevelVisibilityProject();
        const string UncompiledTemplate = "$Classes(Class)[$Name][,]|$Records[$Name][,]|$Structs[$Name][,]|$Interfaces[$Name][,]|$Enums[$Name][,]|$Delegates[$Name][,]|$Types[$Name][,]";

        Render(template: UncompiledTemplate, metadata: metadata, includeInternals: includeInternals)
            .Should().Be(expected);

        var settings = new Settings();
        if (includeInternals)
        {
            settings.IncludeInternals();
        }

        var factory = new TemplateCodeModelAdapterFactory(metadata: metadata, settings: settings);
        var file = factory.CreateFile(project: metadata);
        var actualCodeModel = string.Join(
            separator: '|',
            string.Join(separator: ',', values: file.Classes.Select(selector: item => item.Name)),
            string.Join(separator: ',', values: file.Records.Select(selector: item => item.Name)),
            string.Join(separator: ',', values: file.Structs.Select(selector: item => item.Name)),
            string.Join(separator: ',', values: file.Interfaces.Select(selector: item => item.Name)),
            string.Join(separator: ',', values: file.Enums.Select(selector: item => item.Name)),
            string.Join(separator: ',', values: file.Delegates.Select(selector: item => item.Name)),
            string.Join(separator: ',', values: file.Types.Select(selector: item => item.Name)));

        actualCodeModel.Should().Be(expected);
    }

    [Theory]
    [InlineData(TypeMetadataKind.Class, "Classes", false)]
    [InlineData(TypeMetadataKind.Class, "Classes", true)]
    [InlineData(TypeMetadataKind.Record, "Records", false)]
    [InlineData(TypeMetadataKind.Record, "Records", true)]
    [InlineData(TypeMetadataKind.Struct, "Structs", false)]
    [InlineData(TypeMetadataKind.Struct, "Structs", true)]
    public void MemberAndNestedCollectionsHonorIncludeInternalsAcrossDeclarations(
        TypeMetadataKind kind,
        string rootCollection,
        bool includeInternals)
    {
        var owner = CreateRichOwnerDeclaration(kind: kind);
        var metadata = CreateSingleTypeProject(type: owner);
        var expected = includeInternals
            ? "PubProp,IntProp|PubMethod,IntMethod|PubConst,IntConst|PubField,IntField|PubStaticField,IntStaticField|PubEvent,IntEvent|PubNestedDelegate,IntNestedDelegate|PubNestedClass,IntNestedClass|PubNestedRecord,IntNestedRecord|PubNestedStruct,IntNestedStruct|IPubNested,IIntNested|PubNestedEnum,IntNestedEnum"
            : "PubProp|PubMethod|PubConst|PubField|PubStaticField|PubEvent|PubNestedDelegate|PubNestedClass|PubNestedRecord|PubNestedStruct|IPubNested|PubNestedEnum";

        var template = "$" + rootCollection + "(Owner)[$Properties[$Name][,]|$Methods[$Name][,]|$Constants[$Name][,]|$Fields[$Name][,]|$StaticReadOnlyFields[$Name][,]|$Events[$Name][,]|$Delegates[$Name][,]|$NestedClasses[$Name][,]|$NestedRecords[$Name][,]|$NestedStructs[$Name][,]|$NestedInterfaces[$Name][,]|$NestedEnums[$Name][,]]";
        Render(template: template, metadata: metadata, includeInternals: includeInternals)
            .Should().Be(expected);

        var settings = new Settings();
        if (includeInternals)
        {
            settings.IncludeInternals();
        }

        var factory = new TemplateCodeModelAdapterFactory(metadata: metadata, settings: settings);
        FormatRichMembersFromAdaptedDeclaration(factory: factory, owner: owner, kind: kind)
            .Should().Be(expected);

        factory.TryAdapt(context: owner, targetType: typeof(CodeType), adapted: out var adaptedType).Should().BeTrue();
        var codeType = adaptedType.Should().BeOfType<CodeType>().Which;
        var expectedCodeType = includeInternals
            ? "PubProp,IntProp|PubMethod,IntMethod|PubConst,IntConst|PubField,IntField|PubStaticField,IntStaticField|PubNestedDelegate,IntNestedDelegate|PubNestedClass,IntNestedClass|PubNestedRecord,IntNestedRecord|PubNestedStruct,IntNestedStruct|IPubNested,IIntNested|PubNestedEnum,IntNestedEnum"
            : "PubProp|PubMethod|PubConst|PubField|PubStaticField|PubNestedDelegate|PubNestedClass|PubNestedRecord|PubNestedStruct|IPubNested|PubNestedEnum";
        FormatCodeTypeMembers(type: codeType).Should().Be(expectedCodeType);
    }

    [Theory]
    [InlineData(false, "PubProp|PubMethod|PubEvent|PubNestedStruct")]
    [InlineData(true, "PubProp,IntProp|PubMethod,IntMethod|PubEvent,IntEvent|PubNestedStruct,IntNestedStruct")]
    public void InterfaceMembersHonorIncludeInternals(bool includeInternals, string expected)
    {
        var owner = CreateRichOwnerDeclaration(kind: TypeMetadataKind.Interface);
        var metadata = CreateSingleTypeProject(type: owner);

        Render(
            template: "$Interfaces(Owner)[$Properties[$Name][,]|$Methods[$Name][,]|$Events[$Name][,]|$NestedStructs[$Name][,]]",
            metadata: metadata,
            includeInternals: includeInternals)
            .Should().Be(expected);

        var settings = new Settings();
        if (includeInternals)
        {
            settings.IncludeInternals();
        }

        var factory = new TemplateCodeModelAdapterFactory(metadata: metadata, settings: settings);
        factory.TryAdapt(context: owner, targetType: typeof(CodeInterface), adapted: out var adapted).Should().BeTrue();
        var model = adapted.Should().BeOfType<CodeInterface>().Which;
        var actual = string.Join(
            separator: '|',
            string.Join(separator: ',', values: model.Properties.Select(selector: item => item.Name)),
            string.Join(separator: ',', values: model.Methods.Select(selector: item => item.Name)),
            string.Join(separator: ',', values: model.Events.Select(selector: item => item.Name)),
            string.Join(separator: ',', values: model.NestedStructs.Select(selector: item => item.Name)));

        actual.Should().Be(expected);
    }

    [Fact]
    public void CompiledTemplateConstructorWithFileSeesInternalsAfterSettingsIncludeInternalsCall()
    {
        var metadata = CreateTopLevelVisibilityProject();
        const string Template = """
            ${
                private string summary = string.Empty;

                Template(Settings settings, File file)
                {
                    settings.IncludeInternals();
                    summary = string.Join(",", file.Classes.Select(c => c.Name));
                }

                string Summary(File file) => summary;
            }
            $Summary|$Classes(Class)[$Name][,]
            """;

        Render(template: Template, metadata: metadata).Should().Be("PubClass,IntClass|PubClass,IntClass");
    }

    [Fact]
    public void BuiltInMemberAndInheritanceFiltersRespectIncludeInternals()
    {
        var stringType = CreatePrimitiveReference(name: "String", fullName: "System.String");
        var internalInterface = new TypeMetadata(
            Name: "IIntContract",
            FullName: "Sample.IIntContract",
            Namespace: "Sample",
            Kind: TypeMetadataKind.Interface,
            Accessibility: MetadataAccessibility.Internal,
            Properties: [],
            Attributes: [],
            BaseTypes: [],
            EnumValues: [],
            IsNullableAware: true);
        var publicInterface = internalInterface with
        {
            Name = "IPubContract",
            FullName = "Sample.IPubContract",
            Accessibility = MetadataAccessibility.Public,
        };
        var model = new TypeMetadata(
            Name: "InternalMembersOnly",
            FullName: "Sample.InternalMembersOnly",
            Namespace: "Sample",
            Kind: TypeMetadataKind.Class,
            Accessibility: MetadataAccessibility.Public,
            Properties:
            [
                new PropertyMetadata(
                    Name: "Secret",
                    FullName: "Sample.InternalMembersOnly.Secret",
                    Type: stringType,
                    Accessibility: MetadataAccessibility.Internal,
                    HasGetter: true,
                    HasSetter: true,
                    IsRequired: false,
                    Attributes: []),
            ],
            Attributes: [],
            BaseTypes: [CreateTypeReference(type: internalInterface), CreateTypeReference(type: publicInterface)],
            EnumValues: [],
            IsNullableAware: true)
        {
            Methods =
            [
                new MethodMetadata(
                    Name: "RunInternal",
                    FullName: "Sample.InternalMembersOnly.RunInternal",
                    ReturnType: stringType,
                    Accessibility: MetadataAccessibility.Internal,
                    IsStatic: false,
                    IsAbstract: false,
                    IsGeneric: false,
                    Parameters: [],
                    Attributes: [],
                    ParentTypeFullName: "Sample.InternalMembersOnly"),
            ],
            Constants =
            [
                new ConstantMetadata(
                    Name: "InternalVersion",
                    FullName: "Sample.InternalMembersOnly.InternalVersion",
                    Accessibility: MetadataAccessibility.Internal,
                    Type: stringType,
                    Value: "1",
                    Attributes: [],
                    ParentTypeFullName: "Sample.InternalMembersOnly"),
            ],
        };
        var metadata = new ProjectMetadata(
            ProjectPath: "Sample.csproj",
            SourceFiles: [],
            Types: [model, internalInterface, publicInterface],
            Diagnostics: []);

        const string Template = "$Classes(HasProperties)[$Name]|$Classes(HasMethods)[$Name]|$Classes(HasConstants)[$Name]|$Classes(:IIntContract)[$Name]|$Classes(InternalMembersOnly)[$Interfaces[$Name][,]]";

        Render(template: Template, metadata: metadata, includeInternals: false)
            .Should().Be("||||IPubContract");
        Render(template: Template, metadata: metadata, includeInternals: true)
            .Should().Be("InternalMembersOnly|InternalMembersOnly|InternalMembersOnly|InternalMembersOnly|IIntContract,IPubContract");
    }

    [Fact]
    public async Task GeneratorSkipsInternalOnlySourceFilesByDefaultAndSupportsPerTemplateIncludeInternals()
    {
        var directory = Directory.CreateTempSubdirectory(prefix: "TypewriterIncludeInternalsGenTests-").FullName;
        try
        {
            var projectPath = Path.Combine(path1: directory, path2: "Sample.csproj");
            await File.WriteAllTextAsync(path: projectPath, contents: "<Project />");

            var publicSourcePath = Path.Combine(path1: directory, path2: "PublicModel.cs");
            var internalSourcePath = Path.Combine(path1: directory, path2: "InternalModel.cs");
            var defaultTemplatePath = Path.Combine(path1: directory, path2: "PublicTemplates", path3: "Models.tst");
            var internalsTemplatePath = Path.Combine(path1: directory, path2: "InternalTemplates", path3: "Models.tst");
            Directory.CreateDirectory(path: Path.GetDirectoryName(path: defaultTemplatePath)!);
            Directory.CreateDirectory(path: Path.GetDirectoryName(path: internalsTemplatePath)!);

            var stringType = CreatePrimitiveReference(name: "String", fullName: "System.String");
            var publicType = new TypeMetadata(
                Name: "PublicModel",
                FullName: "Sample.PublicModel",
                Namespace: "Sample",
                Kind: TypeMetadataKind.Class,
                Accessibility: MetadataAccessibility.Public,
                Properties:
                [
                    new PropertyMetadata(
                        Name: "Title",
                        FullName: "Sample.PublicModel.Title",
                        Type: stringType,
                        Accessibility: MetadataAccessibility.Public,
                        HasGetter: true,
                        HasSetter: true,
                        IsRequired: false,
                        Attributes: []),
                    new PropertyMetadata(
                        Name: "InternalNote",
                        FullName: "Sample.PublicModel.InternalNote",
                        Type: stringType,
                        Accessibility: MetadataAccessibility.Internal,
                        HasGetter: true,
                        HasSetter: true,
                        IsRequired: false,
                        Attributes: []),
                ],
                Attributes: [],
                BaseTypes: [],
                EnumValues: [],
                IsNullableAware: true)
            {
                Location = new SourceLocation(Path: publicSourcePath, Line: 1, Column: 1),
                FileLocations = [publicSourcePath],
            };
            var internalType = new TypeMetadata(
                Name: "InternalModel",
                FullName: "Sample.InternalModel",
                Namespace: "Sample",
                Kind: TypeMetadataKind.Class,
                Accessibility: MetadataAccessibility.Internal,
                Properties:
                [
                    new PropertyMetadata(
                        Name: "Secret",
                        FullName: "Sample.InternalModel.Secret",
                        Type: stringType,
                        Accessibility: MetadataAccessibility.Public,
                        HasGetter: true,
                        HasSetter: true,
                        IsRequired: false,
                        Attributes: []),
                ],
                Attributes: [],
                BaseTypes: [],
                EnumValues: [],
                IsNullableAware: true)
            {
                Location = new SourceLocation(Path: internalSourcePath, Line: 1, Column: 1),
                FileLocations = [internalSourcePath],
            };

            var projectMetadata = new ProjectMetadata(
                ProjectPath: projectPath,
                SourceFiles:
                [
                    new SourceFileMetadata(Path: publicSourcePath, Types: [publicType]),
                    new SourceFileMetadata(Path: internalSourcePath, Types: [internalType]),
                ],
                Types: [publicType, internalType],
                Diagnostics: []);

            var templates = new[]
            {
                new TemplateFile(
                    Path: defaultTemplatePath,
                    Content: "$Classes[export interface $Name { $Properties[$name: $Type;][] }]"),
                new TemplateFile(
                    Path: internalsTemplatePath,
                    Content: """
                             ${
                                 Template(Settings settings)
                                 {
                                     settings.IncludeInternals();
                                 }
                             }
                             $Classes[export interface $Name { $Properties[$name: $Type;][] }]
                             """),
            };

            var generator = new TypewriterGenerator(
                templateDiscovery: new FixedTemplateDiscovery(templates: templates),
                metadataProvider: new FixedMetadataProvider(metadata: projectMetadata),
                fileWriter: new InMemoryFileWriter());

            var result = await generator.GenerateAsync(
                request: new GenerationRequest(
                    WorkspacePath: directory,
                    ProjectPath: projectPath,
                    TemplatePath: null,
                    Mode: GenerationMode.Generate,
                    Configuration: TypewriterConfiguration.Default),
                cancellationToken: CancellationToken.None);

            result.Success.Should().BeTrue();
            result.GeneratedFiles.Should().HaveCount(3);

            var publicDefaultFile = result.GeneratedFiles.Should()
                .ContainSingle(file => file.Path == Path.Combine(path1: Path.GetDirectoryName(path: defaultTemplatePath)!, path2: "PublicModel.ts"))
                .Which;
            publicDefaultFile.Content.Should().Contain("title: string;");
            publicDefaultFile.Content.Should().NotContain("internalNote");

            result.GeneratedFiles.Should()
                .NotContain(file => file.Path == Path.Combine(path1: Path.GetDirectoryName(path: defaultTemplatePath)!, path2: "InternalModel.ts"));

            var publicWithInternalsFile = result.GeneratedFiles.Should()
                .ContainSingle(file => file.Path == Path.Combine(path1: Path.GetDirectoryName(path: internalsTemplatePath)!, path2: "PublicModel.ts"))
                .Which;
            publicWithInternalsFile.Content.Should().Contain("title: string;");
            publicWithInternalsFile.Content.Should().Contain("internalNote: string;");

            var internalWithInternalsFile = result.GeneratedFiles.Should()
                .ContainSingle(file => file.Path == Path.Combine(path1: Path.GetDirectoryName(path: internalsTemplatePath)!, path2: "InternalModel.ts"))
                .Which;
            internalWithInternalsFile.Content.Should().Contain("export interface InternalModel");
            internalWithInternalsFile.Content.Should().Contain("secret: string;");
        }
        finally
        {
            Directory.Delete(path: directory, recursive: true);
        }
    }

    private static string Render(
        string template,
        ProjectMetadata metadata,
        bool includeInternals = false)
    {
        var diagnostics = new List<GenerationDiagnostic>();
        var document = TemplateDocument.Parse(
            template: new TemplateFile(
                Path: Path.Combine(path1: Path.GetTempPath(), path2: "include-internals-test.tst"),
                Content: template),
            diagnostics: diagnostics);
        var defaults = TemplateRenderDefaults.Default with { IncludeInternalsGeneration = includeInternals };
        var output = new TemplateRenderer(typeMapper: new TypeScriptTypeMapper()).Render(
            template: document,
            metadata: metadata,
            diagnostics: diagnostics,
            defaults: defaults);
        diagnostics.Should().BeEmpty();
        return output.Trim();
    }

    private static string FormatRichMembersFromAdaptedDeclaration(
        TemplateCodeModelAdapterFactory factory,
        TypeMetadata owner,
        TypeMetadataKind kind)
    {
        switch (kind)
        {
            case TypeMetadataKind.Class:
            {
                factory.TryAdapt(context: owner, targetType: typeof(CodeClass), adapted: out var adapted).Should().BeTrue();
                var model = adapted.Should().BeOfType<CodeClass>().Which;
                return string.Join(
                    separator: '|',
                    string.Join(separator: ',', values: model.Properties.Select(selector: item => item.Name)),
                    string.Join(separator: ',', values: model.Methods.Select(selector: item => item.Name)),
                    string.Join(separator: ',', values: model.Constants.Select(selector: item => item.Name)),
                    string.Join(separator: ',', values: model.Fields.Select(selector: item => item.Name)),
                    string.Join(separator: ',', values: model.StaticReadOnlyFields.Select(selector: item => item.Name)),
                    string.Join(separator: ',', values: model.Events.Select(selector: item => item.Name)),
                    string.Join(separator: ',', values: model.Delegates.Select(selector: item => item.Name)),
                    string.Join(separator: ',', values: model.NestedClasses.Select(selector: item => item.Name)),
                    string.Join(separator: ',', values: model.NestedRecords.Select(selector: item => item.Name)),
                    string.Join(separator: ',', values: model.NestedStructs.Select(selector: item => item.Name)),
                    string.Join(separator: ',', values: model.NestedInterfaces.Select(selector: item => item.Name)),
                    string.Join(separator: ',', values: model.NestedEnums.Select(selector: item => item.Name)));
            }

            case TypeMetadataKind.Record:
            {
                factory.TryAdapt(context: owner, targetType: typeof(CodeRecord), adapted: out var adapted).Should().BeTrue();
                var model = adapted.Should().BeOfType<CodeRecord>().Which;
                return string.Join(
                    separator: '|',
                    string.Join(separator: ',', values: model.Properties.Select(selector: item => item.Name)),
                    string.Join(separator: ',', values: model.Methods.Select(selector: item => item.Name)),
                    string.Join(separator: ',', values: model.Constants.Select(selector: item => item.Name)),
                    string.Join(separator: ',', values: model.Fields.Select(selector: item => item.Name)),
                    string.Join(separator: ',', values: model.StaticReadOnlyFields.Select(selector: item => item.Name)),
                    string.Join(separator: ',', values: model.Events.Select(selector: item => item.Name)),
                    string.Join(separator: ',', values: model.Delegates.Select(selector: item => item.Name)),
                    string.Join(separator: ',', values: model.NestedClasses.Select(selector: item => item.Name)),
                    string.Join(separator: ',', values: model.NestedRecords.Select(selector: item => item.Name)),
                    string.Join(separator: ',', values: model.NestedStructs.Select(selector: item => item.Name)),
                    string.Join(separator: ',', values: model.NestedInterfaces.Select(selector: item => item.Name)),
                    string.Join(separator: ',', values: model.NestedEnums.Select(selector: item => item.Name)));
            }

            default:
            {
                factory.TryAdapt(context: owner, targetType: typeof(CodeStruct), adapted: out var adapted).Should().BeTrue();
                var model = adapted.Should().BeOfType<CodeStruct>().Which;
                return string.Join(
                    separator: '|',
                    string.Join(separator: ',', values: model.Properties.Select(selector: item => item.Name)),
                    string.Join(separator: ',', values: model.Methods.Select(selector: item => item.Name)),
                    string.Join(separator: ',', values: model.Constants.Select(selector: item => item.Name)),
                    string.Join(separator: ',', values: model.Fields.Select(selector: item => item.Name)),
                    string.Join(separator: ',', values: model.StaticReadOnlyFields.Select(selector: item => item.Name)),
                    string.Join(separator: ',', values: model.Events.Select(selector: item => item.Name)),
                    string.Join(separator: ',', values: model.Delegates.Select(selector: item => item.Name)),
                    string.Join(separator: ',', values: model.NestedClasses.Select(selector: item => item.Name)),
                    string.Join(separator: ',', values: model.NestedRecords.Select(selector: item => item.Name)),
                    string.Join(separator: ',', values: model.NestedStructs.Select(selector: item => item.Name)),
                    string.Join(separator: ',', values: model.NestedInterfaces.Select(selector: item => item.Name)),
                    string.Join(separator: ',', values: model.NestedEnums.Select(selector: item => item.Name)));
            }
        }
    }

    private static string FormatCodeTypeMembers(CodeType type) =>
        string.Join(
            separator: '|',
            string.Join(separator: ',', values: type.Properties.Select(selector: item => item.Name)),
            string.Join(separator: ',', values: type.Methods.Select(selector: item => item.Name)),
            string.Join(separator: ',', values: type.Constants.Select(selector: item => item.Name)),
            string.Join(separator: ',', values: type.Fields.Select(selector: item => item.Name)),
            string.Join(separator: ',', values: type.StaticReadOnlyFields.Select(selector: item => item.Name)),
            string.Join(separator: ',', values: type.Delegates.Select(selector: item => item.Name)),
            string.Join(separator: ',', values: type.NestedClasses.Select(selector: item => item.Name)),
            string.Join(separator: ',', values: type.NestedRecords.Select(selector: item => item.Name)),
            string.Join(separator: ',', values: type.NestedStructs.Select(selector: item => item.Name)),
            string.Join(separator: ',', values: type.NestedInterfaces.Select(selector: item => item.Name)),
            string.Join(separator: ',', values: type.NestedEnums.Select(selector: item => item.Name)));

    private static ProjectMetadata CreateTopLevelVisibilityProject()
    {
        var stringType = CreatePrimitiveReference(name: "String", fullName: "System.String");
        var types = new[]
        {
            CreateSimpleType(name: "PubClass", kind: TypeMetadataKind.Class, accessibility: MetadataAccessibility.Public),
            CreateSimpleType(name: "IntClass", kind: TypeMetadataKind.Class, accessibility: MetadataAccessibility.Internal),
            CreateSimpleType(name: "PubRecord", kind: TypeMetadataKind.Record, accessibility: MetadataAccessibility.Public),
            CreateSimpleType(name: "IntRecord", kind: TypeMetadataKind.Record, accessibility: MetadataAccessibility.Internal),
            CreateSimpleType(name: "PubStruct", kind: TypeMetadataKind.Struct, accessibility: MetadataAccessibility.Public),
            CreateSimpleType(name: "IntStruct", kind: TypeMetadataKind.Struct, accessibility: MetadataAccessibility.Internal),
            CreateSimpleType(name: "IPubContract", kind: TypeMetadataKind.Interface, accessibility: MetadataAccessibility.Public),
            CreateSimpleType(name: "IIntContract", kind: TypeMetadataKind.Interface, accessibility: MetadataAccessibility.Internal),
            CreateSimpleType(name: "PubEnum", kind: TypeMetadataKind.Enum, accessibility: MetadataAccessibility.Public),
            CreateSimpleType(name: "IntEnum", kind: TypeMetadataKind.Enum, accessibility: MetadataAccessibility.Internal),
        };
        var delegates = new[]
        {
            new DelegateMetadata(
                Name: "PubCallback",
                FullName: "Sample.PubCallback",
                Accessibility: MetadataAccessibility.Public,
                ReturnType: stringType,
                IsGeneric: false,
                Parameters: [],
                Attributes: [],
                ParentTypeFullName: string.Empty)
            {
                Location = new SourceLocation(Path: "Models.cs", Line: 1, Column: 1),
            },
            new DelegateMetadata(
                Name: "IntCallback",
                FullName: "Sample.IntCallback",
                Accessibility: MetadataAccessibility.Internal,
                ReturnType: stringType,
                IsGeneric: false,
                Parameters: [],
                Attributes: [],
                ParentTypeFullName: string.Empty)
            {
                Location = new SourceLocation(Path: "Models.cs", Line: 2, Column: 1),
            },
        };
        return new ProjectMetadata(
            ProjectPath: "Sample.csproj",
            SourceFiles: [new SourceFileMetadata(Path: "Models.cs", Types: types) { Delegates = delegates }],
            Types: types,
            Diagnostics: [])
        {
            Delegates = delegates,
        };
    }

    private static TypeMetadata CreateRichOwnerDeclaration(TypeMetadataKind kind)
    {
        var stringType = CreatePrimitiveReference(name: "String", fullName: "System.String");
        const string OwnerFullName = "Sample.Owner";

        static TypeMetadata Nested(string name, TypeMetadataKind nestedKind, MetadataAccessibility accessibility) =>
            CreateSimpleType(name: name, kind: nestedKind, accessibility: accessibility) with
            {
                FullName = OwnerFullName + "." + name,
                ContainingTypeFullName = OwnerFullName,
            };

        return CreateSimpleType(name: "Owner", kind: kind, accessibility: MetadataAccessibility.Public) with
        {
            Properties =
            [
                new PropertyMetadata(
                    Name: "PubProp",
                    FullName: OwnerFullName + ".PubProp",
                    Type: stringType,
                    Accessibility: MetadataAccessibility.Public,
                    HasGetter: true,
                    HasSetter: true,
                    IsRequired: false,
                    Attributes: [])
                {
                    ParentTypeFullName = OwnerFullName,
                },
                new PropertyMetadata(
                    Name: "IntProp",
                    FullName: OwnerFullName + ".IntProp",
                    Type: stringType,
                    Accessibility: MetadataAccessibility.Internal,
                    HasGetter: true,
                    HasSetter: true,
                    IsRequired: false,
                    Attributes: [])
                {
                    ParentTypeFullName = OwnerFullName,
                },
            ],
            Methods =
            [
                new MethodMetadata(
                    Name: "PubMethod",
                    FullName: OwnerFullName + ".PubMethod",
                    ReturnType: stringType,
                    Accessibility: MetadataAccessibility.Public,
                    IsStatic: false,
                    IsAbstract: false,
                    IsGeneric: false,
                    Parameters: [],
                    Attributes: [],
                    ParentTypeFullName: OwnerFullName),
                new MethodMetadata(
                    Name: "IntMethod",
                    FullName: OwnerFullName + ".IntMethod",
                    ReturnType: stringType,
                    Accessibility: MetadataAccessibility.Internal,
                    IsStatic: false,
                    IsAbstract: false,
                    IsGeneric: false,
                    Parameters: [],
                    Attributes: [],
                    ParentTypeFullName: OwnerFullName),
            ],
            Constants =
            [
                new ConstantMetadata(
                    Name: "PubConst",
                    FullName: OwnerFullName + ".PubConst",
                    Accessibility: MetadataAccessibility.Public,
                    Type: stringType,
                    Value: "1",
                    Attributes: [],
                    ParentTypeFullName: OwnerFullName),
                new ConstantMetadata(
                    Name: "IntConst",
                    FullName: OwnerFullName + ".IntConst",
                    Accessibility: MetadataAccessibility.Internal,
                    Type: stringType,
                    Value: "2",
                    Attributes: [],
                    ParentTypeFullName: OwnerFullName),
            ],
            Fields =
            [
                new FieldMetadata(
                    Name: "PubField",
                    FullName: OwnerFullName + ".PubField",
                    Accessibility: MetadataAccessibility.Public,
                    Type: stringType,
                    Attributes: [],
                    ParentTypeFullName: OwnerFullName),
                new FieldMetadata(
                    Name: "IntField",
                    FullName: OwnerFullName + ".IntField",
                    Accessibility: MetadataAccessibility.Internal,
                    Type: stringType,
                    Attributes: [],
                    ParentTypeFullName: OwnerFullName),
            ],
            StaticReadOnlyFields =
            [
                new StaticReadOnlyFieldMetadata(
                    Name: "PubStaticField",
                    FullName: OwnerFullName + ".PubStaticField",
                    Accessibility: MetadataAccessibility.Public,
                    Type: stringType,
                    Value: "pub",
                    Attributes: [],
                    ParentTypeFullName: OwnerFullName),
                new StaticReadOnlyFieldMetadata(
                    Name: "IntStaticField",
                    FullName: OwnerFullName + ".IntStaticField",
                    Accessibility: MetadataAccessibility.Internal,
                    Type: stringType,
                    Value: "int",
                    Attributes: [],
                    ParentTypeFullName: OwnerFullName),
            ],
            Events =
            [
                new EventMetadata(
                    Name: "PubEvent",
                    FullName: OwnerFullName + ".PubEvent",
                    Accessibility: MetadataAccessibility.Public,
                    Type: stringType,
                    Attributes: [],
                    ParentTypeFullName: OwnerFullName),
                new EventMetadata(
                    Name: "IntEvent",
                    FullName: OwnerFullName + ".IntEvent",
                    Accessibility: MetadataAccessibility.Internal,
                    Type: stringType,
                    Attributes: [],
                    ParentTypeFullName: OwnerFullName),
            ],
            Delegates =
            [
                new DelegateMetadata(
                    Name: "PubNestedDelegate",
                    FullName: OwnerFullName + ".PubNestedDelegate",
                    Accessibility: MetadataAccessibility.Public,
                    ReturnType: stringType,
                    IsGeneric: false,
                    Parameters: [],
                    Attributes: [],
                    ParentTypeFullName: OwnerFullName),
                new DelegateMetadata(
                    Name: "IntNestedDelegate",
                    FullName: OwnerFullName + ".IntNestedDelegate",
                    Accessibility: MetadataAccessibility.Internal,
                    ReturnType: stringType,
                    IsGeneric: false,
                    Parameters: [],
                    Attributes: [],
                    ParentTypeFullName: OwnerFullName),
            ],
            NestedClasses =
            [
                Nested(name: "PubNestedClass", nestedKind: TypeMetadataKind.Class, accessibility: MetadataAccessibility.Public),
                Nested(name: "IntNestedClass", nestedKind: TypeMetadataKind.Class, accessibility: MetadataAccessibility.Internal),
            ],
            NestedRecords =
            [
                Nested(name: "PubNestedRecord", nestedKind: TypeMetadataKind.Record, accessibility: MetadataAccessibility.Public),
                Nested(name: "IntNestedRecord", nestedKind: TypeMetadataKind.Record, accessibility: MetadataAccessibility.Internal),
            ],
            NestedStructs =
            [
                Nested(name: "PubNestedStruct", nestedKind: TypeMetadataKind.Struct, accessibility: MetadataAccessibility.Public),
                Nested(name: "IntNestedStruct", nestedKind: TypeMetadataKind.Struct, accessibility: MetadataAccessibility.Internal),
            ],
            NestedInterfaces =
            [
                Nested(name: "IPubNested", nestedKind: TypeMetadataKind.Interface, accessibility: MetadataAccessibility.Public),
                Nested(name: "IIntNested", nestedKind: TypeMetadataKind.Interface, accessibility: MetadataAccessibility.Internal),
            ],
            NestedEnums =
            [
                Nested(name: "PubNestedEnum", nestedKind: TypeMetadataKind.Enum, accessibility: MetadataAccessibility.Public),
                Nested(name: "IntNestedEnum", nestedKind: TypeMetadataKind.Enum, accessibility: MetadataAccessibility.Internal),
            ],
        };
    }

    private static ProjectMetadata CreateSingleTypeProject(TypeMetadata type) =>
        new(
            ProjectPath: "Sample.csproj",
            SourceFiles: [new SourceFileMetadata(Path: "Models.cs", Types: [type])],
            Types: [type],
            Diagnostics: []);

    private static TypeMetadata CreateSimpleType(
        string name,
        TypeMetadataKind kind,
        MetadataAccessibility accessibility) =>
        new(
            Name: name,
            FullName: "Sample." + name,
            Namespace: "Sample",
            Kind: kind,
            Accessibility: accessibility,
            Properties: [],
            Attributes: [],
            BaseTypes: [],
            EnumValues: [],
            IsNullableAware: true)
        {
            Location = new SourceLocation(Path: "Models.cs", Line: 1, Column: 1),
            FileLocations = ["Models.cs"],
        };

    private static TypeMetadataReference CreatePrimitiveReference(string name, string fullName) =>
        new(
            Name: name,
            FullName: fullName,
            Namespace: "System",
            IsNullable: false,
            IsCollection: false,
            IsDictionary: false,
            IsEnum: false,
            IsPrimitive: true,
            IsDateLike: false,
            ElementType: null,
            TypeArguments: []);

    private static TypeMetadataReference CreateTypeReference(TypeMetadata type) =>
        new(
            Name: type.Name,
            FullName: type.FullName,
            Namespace: type.Namespace,
            IsNullable: false,
            IsCollection: false,
            IsDictionary: false,
            IsEnum: type.Kind == TypeMetadataKind.Enum,
            IsPrimitive: false,
            IsDateLike: false,
            ElementType: null,
            TypeArguments: []);

    private sealed class FixedTemplateDiscovery(IReadOnlyList<TemplateFile> templates) : ITemplateDiscovery
    {
        public Task<IReadOnlyList<TemplateFile>> FindTemplatesAsync(
            WorkspaceContext workspace,
            GenerationRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(result: templates);
    }

    private sealed class FixedMetadataProvider(ProjectMetadata metadata) : IProjectMetadataProvider
    {
        public Task<ProjectMetadata> GetMetadataAsync(
            ProjectContext project,
            CancellationToken cancellationToken) =>
            Task.FromResult(result: metadata);
    }

    private sealed class InMemoryFileWriter : IGeneratedFileWriter
    {
        public Task<GeneratedFile> WriteAsync(
            GeneratedFile file,
            GenerationRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(result: file with { Changed = true });
    }
}
