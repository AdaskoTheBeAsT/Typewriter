using System.Globalization;
using AwesomeAssertions;
using Typewriter.Abstractions;
using Typewriter.Engine;
using Xunit;

namespace Typewriter.Engine.Tests;

public sealed class TemplateCodeModelAdapterFactoryTests
{
    [Theory]
    [InlineData(0, 1, true)]
    [InlineData(0, 1, false)]
    [InlineData(1, 2, true)]
    [InlineData(1, 2, false)]
    [InlineData(2, 1, true)]
    [InlineData(2, 1, false)]
    public void CreateFileResolvesSameNameBaseClassByGenericArity(
        int derivedArity,
        int baseArity,
        bool derivedFirst)
    {
        var metadata = CreateMetadata(kind: TypeMetadataKind.Class, derivedArity: derivedArity, baseArity: baseArity, derivedFirst: derivedFirst);
        var factory = new TemplateCodeModelAdapterFactory(metadata: metadata, templatePath: "models.tst");

        var file = factory.CreateFile(project: metadata);

        var derived = file.Classes.Single(predicate: type => type.TypeParameters.Count == derivedArity);
        var baseClass = derived.BaseClass.Should().BeOfType<Typewriter.CodeModel.Class>().Which;
        baseClass.TypeParameters.Should().HaveCount(baseArity);
        baseClass.TypeArguments.Should().HaveCount(baseArity);
        baseClass.TypeArguments.Should().OnlyContain(argument => argument.Name == "string");
        baseClass.BaseClass.Should().BeNull();
        derived.Type.BaseClass.Should().NotBeNull();
        derived.Type.BaseClass.TypeParameters.Should().HaveCount(baseArity);
    }

    [Theory]
    [InlineData(0, 1, true)]
    [InlineData(0, 1, false)]
    [InlineData(1, 2, true)]
    [InlineData(1, 2, false)]
    [InlineData(2, 1, true)]
    [InlineData(2, 1, false)]
    public void CreateFileResolvesSameNameBaseRecordByGenericArity(
        int derivedArity,
        int baseArity,
        bool derivedFirst)
    {
        var metadata = CreateMetadata(kind: TypeMetadataKind.Record, derivedArity: derivedArity, baseArity: baseArity, derivedFirst: derivedFirst);
        var factory = new TemplateCodeModelAdapterFactory(metadata: metadata, templatePath: "models.tst");

        var file = factory.CreateFile(project: metadata);

        var derived = file.Records.Single(predicate: type => type.TypeParameters.Count == derivedArity);
        var baseRecord = derived.BaseRecord.Should().BeOfType<Typewriter.CodeModel.Record>().Which;
        baseRecord.TypeParameters.Should().HaveCount(baseArity);
        baseRecord.TypeArguments.Should().HaveCount(baseArity);
        baseRecord.BaseRecord.Should().BeNull();
    }

    [Theory]
    [InlineData(0, 1, true)]
    [InlineData(0, 1, false)]
    [InlineData(1, 2, true)]
    [InlineData(1, 2, false)]
    [InlineData(2, 1, true)]
    [InlineData(2, 1, false)]
    public void CreateFileResolvesSameNameBaseInterfaceByGenericArity(
        int derivedArity,
        int baseArity,
        bool derivedFirst)
    {
        var metadata = CreateMetadata(kind: TypeMetadataKind.Interface, derivedArity: derivedArity, baseArity: baseArity, derivedFirst: derivedFirst);
        var factory = new TemplateCodeModelAdapterFactory(metadata: metadata, templatePath: "models.tst");

        var file = factory.CreateFile(project: metadata);

        var derived = file.Interfaces.Single(predicate: type => type.TypeParameters.Count == derivedArity);
        var baseInterface = derived.Interfaces.Should().ContainSingle().Which;
        baseInterface.TypeParameters.Should().HaveCount(baseArity);
        baseInterface.TypeArguments.Should().HaveCount(baseArity);
        baseInterface.Interfaces.Should().BeEmpty();
    }

    [Fact]
    public void CreateFileDoesNotResolveMissingBaseArityToSameNameDerivedClass()
    {
        var metadata = CreateMetadata(kind: TypeMetadataKind.Class, derivedArity: 1, baseArity: 2, derivedFirst: true);
        metadata = metadata with { Types = [metadata.Types[0]] };
        var factory = new TemplateCodeModelAdapterFactory(metadata: metadata, templatePath: "models.tst");

        var file = factory.CreateFile(project: metadata);

        var derived = file.Classes.Should().ContainSingle().Which;
        var baseClass = derived.BaseClass.Should().BeOfType<Typewriter.CodeModel.Class>().Which;
        baseClass.TypeParameters.Should().BeEmpty();
        baseClass.TypeArguments.Should().HaveCount(2);
        baseClass.BaseClass.Should().BeNull();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TryAdaptResolvesTypeReferenceMetadataByGenericArity(bool structFirst)
    {
        var classMetadata = CreateType(kind: TypeMetadataKind.Class, arity: 0);
        var structMetadata = CreateType(kind: TypeMetadataKind.Struct, arity: 1);
        var metadata = new ProjectMetadata(
            ProjectPath: "Sample.csproj",
            SourceFiles: [],
            Types: structFirst ? [structMetadata, classMetadata] : [classMetadata, structMetadata],
            Diagnostics: []);
        var factory = new TemplateCodeModelAdapterFactory(metadata: metadata, templatePath: "models.tst");
        var classReference = CreateReference(name: "Factory", fullName: "Sample.Factory");
        var structReference = classReference with
        {
            TypeArguments = [CreateReference(name: "String", fullName: "System.String")],
        };

        factory.TryAdapt(context: classReference, targetType: typeof(Typewriter.CodeModel.Type), adapted: out var classType).Should().BeTrue();
        factory.TryAdapt(context: structReference, targetType: typeof(Typewriter.CodeModel.Type), adapted: out var structType).Should().BeTrue();

        classType.Should().BeAssignableTo<Typewriter.CodeModel.Type>().Which.IsStruct.Should().BeFalse();
        structType.Should().BeAssignableTo<Typewriter.CodeModel.Type>().Which.IsStruct.Should().BeTrue();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RenderCompilesIssue112HelperWithSameNameGenericBase(bool derivedFirst)
    {
        var metadata = CreateMetadata(kind: TypeMetadataKind.Class, derivedArity: 1, baseArity: 2, derivedFirst: derivedFirst);
        const string Template = """
            ${
                string ModelName(Class model) => model.Name;
            }
            $Classes[export interface $ModelName {}][,]
            """;
        var diagnostics = new List<GenerationDiagnostic>();
        var document = TemplateDocument.Parse(template: new TemplateFile(Path: "models.tst", Content: Template), diagnostics: diagnostics);
        var renderer = new TemplateRenderer(typeMapper: new TypeScriptTypeMapper());

        var output = renderer.Render(template: document, metadata: metadata, diagnostics: diagnostics);

        diagnostics.Should().BeEmpty();
        output.Trim().Should().Be("export interface Factory {},export interface Factory {}");
    }

    private static ProjectMetadata CreateMetadata(
        TypeMetadataKind kind,
        int derivedArity,
        int baseArity,
        bool derivedFirst)
    {
        var baseType = CreateType(kind: kind, arity: baseArity);
        var derivedType = CreateType(kind: kind, arity: derivedArity) with
        {
            BaseTypes =
            [
                CreateReference(name: baseType.Name, fullName: baseType.FullName) with
                {
                    TypeArguments = Enumerable.Repeat(
                        element: CreateReference(name: "String", fullName: "System.String"),
                        count: baseArity).ToArray(),
                },
            ],
        };
        return new ProjectMetadata(
            ProjectPath: "Sample.csproj",
            SourceFiles: [],
            Types: derivedFirst ? [derivedType, baseType] : [baseType, derivedType],
            Diagnostics: []);
    }

    private static TypeMetadata CreateType(TypeMetadataKind kind, int arity)
    {
        return new TypeMetadata(
            Name: "Factory",
            FullName: "Sample.Factory",
            Namespace: "Sample",
            Kind: kind,
            Accessibility: MetadataAccessibility.Public,
            Properties: [],
            Attributes: [],
            BaseTypes: [],
            EnumValues: [],
            IsNullableAware: true)
        {
            TypeParameters = Enumerable.Range(start: 0, count: arity)
                .Select(selector: index => new TypeParameterMetadata(Name: "T" + index.ToString(provider: CultureInfo.InvariantCulture)))
                .ToArray(),
        };
    }

    private static TypeMetadataReference CreateReference(string name, string fullName)
    {
        return new TypeMetadataReference(
            Name: name,
            FullName: fullName,
            Namespace: fullName[..fullName.LastIndexOf(value: '.')],
            IsNullable: false,
            IsCollection: false,
            IsDictionary: false,
            IsEnum: false,
            IsPrimitive: false,
            IsDateLike: false,
            ElementType: null,
            TypeArguments: []);
    }
}
