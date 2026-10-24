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
    [InlineData(TypeMetadataKind.Class)]
    [InlineData(TypeMetadataKind.Record)]
    [InlineData(TypeMetadataKind.Struct)]
    [InlineData(TypeMetadataKind.Interface)]
    public void TryAdaptExpandsSelfReferentialInterfacesLazily(TypeMetadataKind kind)
    {
        var reference = CreateReference(name: "Factory", fullName: "Sample.Factory");
        var contractReference = CreateReference(name: "IContract", fullName: "Sample.IContract") with
        {
            TypeArguments = [reference],
        };
        var declaration = CreateType(kind: kind, arity: 0) with
        {
            BaseTypes = [contractReference],
        };
        var contract = CreateType(kind: TypeMetadataKind.Interface, arity: 1) with
        {
            Name = "IContract",
            FullName = "Sample.IContract",
        };
        var metadata = new ProjectMetadata(
            ProjectPath: "Sample.csproj",
            SourceFiles: [],
            Types: [declaration, contract],
            Diagnostics: []);
        var factory = new TemplateCodeModelAdapterFactory(metadata: metadata, templatePath: "models.tst");

        factory.TryAdapt(context: reference, targetType: typeof(Typewriter.CodeModel.Type), adapted: out var adapted).Should().BeTrue();

        var type = adapted.Should().BeAssignableTo<Typewriter.CodeModel.Type>().Which;
        var interfaces = type.Interfaces;
        type.Interfaces.Should().BeSameAs(interfaces);
        for (var depth = 0; depth < 5; depth++)
        {
            type.IsStruct.Should().Be(kind == TypeMetadataKind.Struct);
            var implemented = type.Interfaces.Should().ContainSingle().Which;
            implemented.Name.Should().Be("IContract");
            type = implemented.TypeArguments.Should().ContainSingle().Which;
            type.FullName.Should().Be("Sample.Factory");
        }
    }

    [Fact]
    public void TryAdaptUsesInterfacesCarriedByExternalTypeReferences()
    {
        var metadata = new ProjectMetadata(ProjectPath: "Sample.csproj", SourceFiles: [], Types: [], Diagnostics: []);
        var factory = new TemplateCodeModelAdapterFactory(metadata: metadata, templatePath: "models.tst");
        var contract = CreateReference(name: "Contract", fullName: "External.Contract") with
        {
            TypeArguments = [CreateReference(name: "String", fullName: "System.String")],
        };
        var reference = CreateReference(name: "Factory", fullName: "External.Factory") with
        {
            Interfaces = [contract],
        };

        factory.TryAdapt(context: reference, targetType: typeof(Typewriter.CodeModel.Type), adapted: out var adapted).Should().BeTrue();

        var type = adapted.Should().BeAssignableTo<Typewriter.CodeModel.Type>().Which;
        var implemented = type.Interfaces.Should().ContainSingle().Which;
        implemented.FullName.Should().Be("External.Contract");
        implemented.TypeArguments.Should().ContainSingle().Which.Name.Should().Be("string");
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

    [Fact]
    public void CreateFilePreservesConstructedTypeArgumentsAcrossInProjectInterfaceAndBaseClassHierarchy()
    {
        var openT = CreateReference(name: "T", fullName: "T");
        var stringRef = CreateReference(name: "String", fullName: "System.String") with
        {
            IsPrimitive = true,
        };
        var intRef = CreateReference(name: "Int32", fullName: "System.Int32") with
        {
            IsPrimitive = true,
        };

        var baseContract = new TypeMetadata(
            Name: "IBaseContract",
            FullName: "Sample.IBaseContract",
            Namespace: "Sample",
            Kind: TypeMetadataKind.Interface,
            Accessibility: MetadataAccessibility.Public,
            Properties: [],
            Attributes: [],
            BaseTypes: [],
            EnumValues: [],
            IsNullableAware: true)
        {
            TypeParameters = [new TypeParameterMetadata(Name: "T")],
        };

        var derivedContract = new TypeMetadata(
            Name: "IFactoryContract",
            FullName: "Sample.IFactoryContract",
            Namespace: "Sample",
            Kind: TypeMetadataKind.Interface,
            Accessibility: MetadataAccessibility.Public,
            Properties: [],
            Attributes: [],
            BaseTypes:
            [
                CreateReference(name: "IBaseContract", fullName: "Sample.IBaseContract") with
                {
                    TypeArguments = [openT],
                },
            ],
            EnumValues: [],
            IsNullableAware: true)
        {
            TypeParameters = [new TypeParameterMetadata(Name: "T")],
        };

        var genericBase2 = new TypeMetadata(
            Name: "Factory",
            FullName: "Sample.Factory",
            Namespace: "Sample",
            Kind: TypeMetadataKind.Class,
            Accessibility: MetadataAccessibility.Public,
            Properties: [],
            Attributes: [],
            BaseTypes: [],
            EnumValues: [],
            IsNullableAware: true)
        {
            TypeParameters =
            [
                new TypeParameterMetadata(Name: "T"),
                new TypeParameterMetadata(Name: "TResult"),
            ],
        };

        var genericBase1 = new TypeMetadata(
            Name: "Factory",
            FullName: "Sample.Factory",
            Namespace: "Sample",
            Kind: TypeMetadataKind.Class,
            Accessibility: MetadataAccessibility.Public,
            Properties: [],
            Attributes: [],
            BaseTypes:
            [
                CreateReference(name: "Factory", fullName: "Sample.Factory") with
                {
                    TypeArguments = [openT, intRef],
                },
                CreateReference(name: "IFactoryContract", fullName: "Sample.IFactoryContract") with
                {
                    TypeArguments = [openT],
                },
            ],
            EnumValues: [],
            IsNullableAware: true)
        {
            TypeParameters = [new TypeParameterMetadata(Name: "T")],
        };

        var concreteFactory = new TypeMetadata(
            Name: "Factory",
            FullName: "Sample.Factory",
            Namespace: "Sample",
            Kind: TypeMetadataKind.Class,
            Accessibility: MetadataAccessibility.Public,
            Properties:
            [
                new PropertyMetadata(
                    Name: "Contract",
                    FullName: "Sample.Factory.Contract",
                    Type: CreateReference(name: "IFactoryContract", fullName: "Sample.IFactoryContract") with
                    {
                        TypeArguments = [stringRef],
                    },
                    Accessibility: MetadataAccessibility.Public,
                    HasGetter: true,
                    HasSetter: true,
                    IsRequired: false,
                    Attributes: []),
            ],
            Attributes: [],
            BaseTypes:
            [
                CreateReference(name: "Factory", fullName: "Sample.Factory") with
                {
                    TypeArguments = [stringRef],
                },
            ],
            EnumValues: [],
            IsNullableAware: true);

        var metadata = new ProjectMetadata(
            ProjectPath: "Sample.csproj",
            SourceFiles: [],
            Types: [baseContract, derivedContract, genericBase2, genericBase1, concreteFactory],
            Diagnostics: []);
        var factory = new TemplateCodeModelAdapterFactory(metadata: metadata, templatePath: "models.tst");

        var file = factory.CreateFile(project: metadata);

        var concreteClass = file.Classes.Single(predicate: c => !c.IsGeneric);
        var firstBase = concreteClass.BaseClass;
        firstBase.Should().NotBeNull();
        firstBase.TypeArguments.Select(selector: a => a.Name).Should().Equal("string");
        firstBase.Interfaces.Should().ContainSingle().Which.TypeArguments.Select(selector: a => a.Name).Should().Equal("string");

        var secondBase = firstBase.BaseClass;
        secondBase.Should().NotBeNull();
        secondBase.TypeArguments.Select(selector: a => a.Name).Should().Equal("string", "number");

        var contractProperty = concreteClass.Properties.Should().ContainSingle().Which;
        var contractInterface = contractProperty.Type.Interfaces.Should().ContainSingle().Which;
        contractInterface.Name.Should().Be("IBaseContract");
        contractInterface.TypeArguments.Select(selector: a => a.Name).Should().Equal("string");
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
        var lastDotIndex = fullName.LastIndexOf(value: '.');
        return new TypeMetadataReference(
            Name: name,
            FullName: fullName,
            Namespace: lastDotIndex < 0 ? string.Empty : fullName[..lastDotIndex],
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
