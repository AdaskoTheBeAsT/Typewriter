using System.Globalization;
using AwesomeAssertions;
using Typewriter.Abstractions;
using Typewriter.Engine;
using Xunit;

namespace Typewriter.Engine.Tests;

public sealed class TemplateCodeModelAdapterFactoryTests
{
    [Theory]
    [InlineData("Boolean", "False", false)]
    [InlineData("Boolean", "True", true)]
    [InlineData("Byte", "255", byte.MaxValue)]
    [InlineData("SByte", "-128", sbyte.MinValue)]
    [InlineData("Int16", "-32768", short.MinValue)]
    [InlineData("UInt16", "65535", ushort.MaxValue)]
    [InlineData("Int32", "2147483647", int.MaxValue)]
    [InlineData("UInt32", "4294967295", uint.MaxValue)]
    [InlineData("Int64", "-9223372036854775808", long.MinValue)]
    [InlineData("UInt64", "18446744073709551615", ulong.MaxValue)]
    [InlineData("Single", "1.25", 1.25f)]
    [InlineData("Double", "1.25", 1.25d)]
    [InlineData("Char", "x", 'x')]
    [InlineData("String", "false", "false")]
    [InlineData("String", "123", "123")]
    [InlineData("String", null, null)]
    [InlineData("Type", "typeof(Sample.Factory)", "typeof(Sample.Factory)")]
    [InlineData(null, "False", "False")]
    [InlineData("Boolean", "invalid", "invalid")]
    [InlineData("Byte", "256", "256")]
    public void TryAdaptPreservesAttributeArgumentValueTypes(string? typeName, string? value, object? expected)
    {
        var argument = new AttributeArgumentMetadata(Name: "Value", Value: value)
        {
            Type = typeName is null ? null : CreateReference(name: typeName, fullName: "System." + typeName),
        };
        var declaration = CreateType(kind: TypeMetadataKind.Class, arity: 0) with
        {
            Attributes = [new AttributeMetadata(Name: "Example", FullName: "Sample.ExampleAttribute", Arguments: [argument])],
        };
        var metadata = new ProjectMetadata(ProjectPath: "Sample.csproj", SourceFiles: [], Types: [declaration], Diagnostics: []);
        var factory = new TemplateCodeModelAdapterFactory(metadata: metadata, templatePath: "models.tst");

        factory.TryAdapt(context: argument, targetType: typeof(Typewriter.CodeModel.AttributeArgument), adapted: out var adaptedArgument).Should().BeTrue();
        var direct = adaptedArgument.Should().BeAssignableTo<Typewriter.CodeModel.AttributeArgument>().Which;
        var declared = factory.CreateFile(project: metadata).Classes.Single().Attributes.Single().Arguments.Single();
        var reference = CreateReference(name: declaration.Name, fullName: declaration.FullName);
        factory.TryAdapt(context: reference, targetType: typeof(Typewriter.CodeModel.Type), adapted: out var adaptedType).Should().BeTrue();
        var referenced = adaptedType.Should().BeAssignableTo<Typewriter.CodeModel.Type>().Which.Attributes.Single().Arguments.Single();

        foreach (var actual in new[] { direct.Value, declared.Value, referenced.Value })
        {
            actual.Should().Be(expected);
            if (expected is not null)
            {
                actual.Should().BeOfType(expected.GetType());
            }
        }
    }

    [Theory]
    [InlineData(TypeMetadataKind.Class, 0)]
    [InlineData(TypeMetadataKind.Class, 1)]
    [InlineData(TypeMetadataKind.Record, 0)]
    [InlineData(TypeMetadataKind.Record, 1)]
    [InlineData(TypeMetadataKind.Struct, 0)]
    [InlineData(TypeMetadataKind.Struct, 1)]
    [InlineData(TypeMetadataKind.Interface, 0)]
    [InlineData(TypeMetadataKind.Interface, 1)]
    [InlineData(TypeMetadataKind.Enum, 0)]
    public void CreateFilePreservesOriginalNameForDeclarations(TypeMetadataKind kind, int arity)
    {
        var declaration = CreateType(kind: kind, arity: arity);
        var metadata = new ProjectMetadata(ProjectPath: "Sample.csproj", SourceFiles: [], Types: [declaration], Diagnostics: []);
        var factory = new TemplateCodeModelAdapterFactory(metadata: metadata, templatePath: "models.tst");

        var file = factory.CreateFile(project: metadata);

        var type = file.Types.Should().ContainSingle().Which;
        type.OriginalName.Should().Be("Factory");
        type.ToString().Should().Be("Factory");
        file.Types.ItemFilterSelector(type).Should().Contain("Factory");

        Typewriter.CodeModel.Type? declarationType = kind switch
        {
            TypeMetadataKind.Class => file.Classes.Single(),
            TypeMetadataKind.Record => file.Records.Single(),
            TypeMetadataKind.Struct => file.Structs.Single(),
            TypeMetadataKind.Interface => file.Interfaces.Single(),
            TypeMetadataKind.Enum => file.Enums.Single(),
            _ => throw new ArgumentOutOfRangeException(paramName: nameof(kind), actualValue: kind, message: null),
        };
        declarationType.Should().NotBeNull();
        declarationType.OriginalName.Should().Be("Factory");

        factory.TryAdapt(context: declaration, targetType: typeof(Typewriter.CodeModel.Type), adapted: out var adapted).Should().BeTrue();
        adapted.Should().BeAssignableTo<Typewriter.CodeModel.Type>().Which.OriginalName.Should().Be("Factory");
    }

    [Fact]
    public void CreateFilePreservesOriginalNameForContainingTypes()
    {
        var nested = CreateType(kind: TypeMetadataKind.Class, arity: 0) with
        {
            Name = "Nested",
            FullName = "Sample.Factory.Nested",
            ContainingTypeFullName = "Sample.Factory",
        };
        var declaration = CreateType(kind: TypeMetadataKind.Class, arity: 1) with
        {
            NestedClasses = [nested],
        };
        var metadata = new ProjectMetadata(ProjectPath: "Sample.csproj", SourceFiles: [], Types: [declaration, nested], Diagnostics: []);
        var factory = new TemplateCodeModelAdapterFactory(metadata: metadata, templatePath: "models.tst");

        var file = factory.CreateFile(project: metadata);

        var nestedClass = file.Classes.Should().ContainSingle().Which.NestedClasses.Should().ContainSingle().Which;
        nestedClass.ContainingClass.Should().NotBeNull();
        nestedClass.ContainingClass.Type.OriginalName.Should().Be("Factory");
        nestedClass.Type.ContainingClass.Should().NotBeNull();
        nestedClass.Type.ContainingClass.Type.OriginalName.Should().Be("Factory");
    }

    [Theory]
    [InlineData("String", "System.String", "string")]
    [InlineData("Int32", "System.Int32", "number")]
    public void TryAdaptPreservesOriginalNameBeforeTypeScriptMapping(string name, string fullName, string mappedName)
    {
        var metadata = new ProjectMetadata(ProjectPath: "Sample.csproj", SourceFiles: [], Types: [], Diagnostics: []);
        var factory = new TemplateCodeModelAdapterFactory(metadata: metadata, templatePath: "models.tst");
        var reference = CreateReference(name: name, fullName: fullName) with { IsPrimitive = true };

        factory.TryAdapt(context: reference, targetType: typeof(Typewriter.CodeModel.Type), adapted: out var adapted).Should().BeTrue();

        var type = adapted.Should().BeAssignableTo<Typewriter.CodeModel.Type>().Which;
        type.OriginalName.Should().Be(name);
        type.Name.Should().Be(mappedName);
        type.ToString().Should().Be(mappedName);
    }

    [Fact]
    public void RenderCompilesHelpersUsingDeclarationOriginalName()
    {
        var declaration = CreateType(kind: TypeMetadataKind.Class, arity: 0);
        var metadata = new ProjectMetadata(ProjectPath: "Sample.csproj", SourceFiles: [], Types: [declaration], Diagnostics: []);
        const string Template = """
            ${
                string DescribeType(Type type) => $"{type.OriginalName}:{type}";
            }
            $Types[$DescribeType]
            $Classes[$DescribeType]
            """;
        var diagnostics = new List<GenerationDiagnostic>();
        var document = TemplateDocument.Parse(template: new TemplateFile(Path: "models.tst", Content: Template), diagnostics: diagnostics);
        var renderer = new TemplateRenderer(typeMapper: new TypeScriptTypeMapper());

        var output = renderer.Render(template: document, metadata: metadata, diagnostics: diagnostics);

        diagnostics.Should().BeEmpty();
        output.ReplaceLineEndings(replacementText: "\n").Trim().Should().Be("Factory:Factory\nFactory:Factory");
    }

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

    [Fact]
    public void CreateFilePopulatesFileLocationsAndIsDefinedForReferencedTypesIssue132()
    {
        var requestPath = Path.Combine(path1: "Project", path2: "Requests", path3: "CreateOrderRequest.cs");
        var envelopePath = Path.Combine(path1: "Project", path2: "Common", path3: "Envelope.cs");
        var nestedItemPath = Path.Combine(path1: "Project", path2: "Models", path3: "OrderContainer.cs");
        var callbackPath = Path.Combine(path1: "Project", path2: "Callbacks", path3: "OrderCallback.cs");
        var modelPath = Path.Combine(path1: "Project", path2: "Models", path3: "OrderModel.cs");

        var requestType = new TypeMetadata(
            Name: "CreateOrderRequest",
            FullName: "Sample.Requests.CreateOrderRequest",
            Namespace: "Sample.Requests",
            Kind: TypeMetadataKind.Class,
            Accessibility: MetadataAccessibility.Public,
            Properties: [],
            Attributes: [],
            BaseTypes: [],
            EnumValues: [],
            IsNullableAware: true)
        {
            FileLocations = [requestPath],
        };

        var envelopeType = new TypeMetadata(
            Name: "Envelope",
            FullName: "Sample.Common.Envelope",
            Namespace: "Sample.Common",
            Kind: TypeMetadataKind.Class,
            Accessibility: MetadataAccessibility.Public,
            Properties: [],
            Attributes: [],
            BaseTypes: [],
            EnumValues: [],
            IsNullableAware: true)
        {
            FileLocations = [envelopePath],
            TypeParameters = [new TypeParameterMetadata(Name: "T")],
        };

        var nestedItemType = new TypeMetadata(
            Name: "NestedItem",
            FullName: "Sample.Models.OrderContainer.NestedItem",
            Namespace: "Sample.Models",
            Kind: TypeMetadataKind.Class,
            Accessibility: MetadataAccessibility.Public,
            Properties: [],
            Attributes: [],
            BaseTypes: [],
            EnumValues: [],
            IsNullableAware: true)
        {
            FileLocations = [nestedItemPath],
        };

        var containerType = new TypeMetadata(
            Name: "OrderContainer",
            FullName: "Sample.Models.OrderContainer",
            Namespace: "Sample.Models",
            Kind: TypeMetadataKind.Class,
            Accessibility: MetadataAccessibility.Public,
            Properties: [],
            Attributes: [],
            BaseTypes: [],
            EnumValues: [],
            IsNullableAware: true)
        {
            FileLocations = [nestedItemPath],
            NestedClasses = [nestedItemType],
        };

        var requestRef = CreateReference(name: "CreateOrderRequest", fullName: "Sample.Requests.CreateOrderRequest");
        var nestedRef = CreateReference(name: "NestedItem", fullName: "Sample.Models.OrderContainer.NestedItem");
        var callbackRef = CreateReference(name: "OrderCallback", fullName: "Sample.Callbacks.OrderCallback") with
        {
            FileLocations = [callbackPath],
        };
        var externalRef = CreateReference(name: "String", fullName: "System.String") with
        {
            IsPrimitive = true,
        };

        var modelType = new TypeMetadata(
            Name: "OrderModel",
            FullName: "Sample.Models.OrderModel",
            Namespace: "Sample.Models",
            Kind: TypeMetadataKind.Class,
            Accessibility: MetadataAccessibility.Public,
            Properties:
            [
                new PropertyMetadata(
                    Name: "Request",
                    FullName: "Sample.Models.OrderModel.Request",
                    Type: requestRef,
                    Accessibility: MetadataAccessibility.Public,
                    HasGetter: true,
                    HasSetter: true,
                    IsRequired: false,
                    Attributes: []),
                new PropertyMetadata(
                    Name: "WrappedRequest",
                    FullName: "Sample.Models.OrderModel.WrappedRequest",
                    Type: CreateReference(name: "Envelope", fullName: "Sample.Common.Envelope") with
                    {
                        TypeArguments = [requestRef],
                    },
                    Accessibility: MetadataAccessibility.Public,
                    HasGetter: true,
                    HasSetter: true,
                    IsRequired: false,
                    Attributes: []),
                new PropertyMetadata(
                    Name: "Items",
                    FullName: "Sample.Models.OrderModel.Items",
                    Type: CreateReference(name: "IReadOnlyList", fullName: "System.Collections.Generic.IReadOnlyList") with
                    {
                        IsCollection = true,
                        ElementType = nestedRef,
                        TypeArguments = [nestedRef],
                    },
                    Accessibility: MetadataAccessibility.Public,
                    HasGetter: true,
                    HasSetter: true,
                    IsRequired: false,
                    Attributes: []),
                new PropertyMetadata(
                    Name: "Callback",
                    FullName: "Sample.Models.OrderModel.Callback",
                    Type: callbackRef,
                    Accessibility: MetadataAccessibility.Public,
                    HasGetter: true,
                    HasSetter: true,
                    IsRequired: false,
                    Attributes: []),
                new PropertyMetadata(
                    Name: "Title",
                    FullName: "Sample.Models.OrderModel.Title",
                    Type: externalRef,
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
            FileLocations = [modelPath],
        };

        var metadata = new ProjectMetadata(
            ProjectPath: "Sample.csproj",
            SourceFiles: [],
            Types: [requestType, envelopeType, containerType, modelType],
            Diagnostics: []);
        var factory = new TemplateCodeModelAdapterFactory(metadata: metadata, templatePath: "models.tst");

        var file = factory.CreateFile(project: metadata);

        var orderModel = file.Classes.Single(predicate: c => c.Name == "OrderModel");
        orderModel.Type.FileLocations.Should().Equal(modelPath);
        orderModel.Type.IsDefined.Should().BeTrue();

        var requestProperty = orderModel.Properties.Single(predicate: p => p.Name == "Request");
        requestProperty.Type.FileLocations.Should().Equal(requestPath);
        requestProperty.Type.IsDefined.Should().BeTrue();

        var wrappedProperty = orderModel.Properties.Single(predicate: p => p.Name == "WrappedRequest");
        wrappedProperty.Type.FileLocations.Should().Equal(envelopePath);
        wrappedProperty.Type.IsDefined.Should().BeTrue();
        var wrappedTypeArgument = wrappedProperty.Type.TypeArguments.Should().ContainSingle().Which;
        wrappedTypeArgument.FileLocations.Should().Equal(requestPath);
        wrappedTypeArgument.IsDefined.Should().BeTrue();

        var itemsProperty = orderModel.Properties.Single(predicate: p => p.Name == "Items");
        itemsProperty.Type.FileLocations.Should().BeEmpty();
        itemsProperty.Type.IsDefined.Should().BeFalse();
        itemsProperty.Type.ElementType.Should().NotBeNull();
        itemsProperty.Type.ElementType.FileLocations.Should().Equal(nestedItemPath);
        itemsProperty.Type.ElementType.IsDefined.Should().BeTrue();

        var callbackProperty = orderModel.Properties.Single(predicate: p => p.Name == "Callback");
        callbackProperty.Type.FileLocations.Should().Equal(callbackPath);
        callbackProperty.Type.IsDefined.Should().BeTrue();

        var titleProperty = orderModel.Properties.Single(predicate: p => p.Name == "Title");
        titleProperty.Type.FileLocations.Should().BeEmpty();
        titleProperty.Type.IsDefined.Should().BeFalse();
    }

    [Fact]
    public void CreateFileDoesNotRecurseWhenGenericTypeInheritsExternalNonGenericBaseWithSameFullName()
    {
        var genericDerived = CreateType(kind: TypeMetadataKind.Class, arity: 1) with
        {
            BaseTypes =
            [
                CreateReference(name: "Factory", fullName: "Sample.Factory") with
                {
                    AssemblyName = "ExternalLibrary",
                },
            ],
        };
        var metadata = new ProjectMetadata(
            ProjectPath: "Sample.csproj",
            SourceFiles: [],
            Types: [genericDerived],
            Diagnostics: []);
        var factory = new TemplateCodeModelAdapterFactory(metadata: metadata, templatePath: "models.tst");

        var file = factory.CreateFile(project: metadata);

        var derivedClass = file.Classes.Should().ContainSingle().Which;
        derivedClass.IsGeneric.Should().BeTrue();
        derivedClass.BaseClass.Should().NotBeNull();
        derivedClass.BaseClass.FullName.Should().Be("Sample.Factory");
        derivedClass.BaseClass.IsGeneric.Should().BeFalse();
        derivedClass.BaseClass.BaseClass.Should().BeNull();
    }

    [Fact]
    public void CreateFilePrefersTypeReferenceFileLocationsOverIndexedMetadataCollision()
    {
        var firstAssemblyPath = Path.Combine(path1: "ProjectA", path2: "Models", path3: "SharedDto.cs");
        var secondAssemblyPath = Path.Combine(path1: "ProjectB", path2: "Models", path3: "SharedDto.cs");

        var indexedDto = new TypeMetadata(
            Name: "SharedDto",
            FullName: "Sample.Models.SharedDto",
            Namespace: "Sample.Models",
            Kind: TypeMetadataKind.Class,
            Accessibility: MetadataAccessibility.Public,
            Properties: [],
            Attributes: [],
            BaseTypes: [],
            EnumValues: [],
            IsNullableAware: true)
        {
            AssemblyName = "ProjectA",
            FileLocations = [firstAssemblyPath],
        };

        var holder = new TypeMetadata(
            Name: "Holder",
            FullName: "Sample.Models.Holder",
            Namespace: "Sample.Models",
            Kind: TypeMetadataKind.Class,
            Accessibility: MetadataAccessibility.Public,
            Properties:
            [
                new PropertyMetadata(
                    Name: "Dto",
                    FullName: "Sample.Models.Holder.Dto",
                    Type: CreateReference(name: "SharedDto", fullName: "Sample.Models.SharedDto") with
                    {
                        AssemblyName = "ProjectB",
                        FileLocations = [secondAssemblyPath],
                    },
                    Accessibility: MetadataAccessibility.Public,
                    HasGetter: true,
                    HasSetter: true,
                    IsRequired: false,
                    Attributes: []),
            ],
            Attributes: [],
            BaseTypes: [],
            EnumValues: [],
            IsNullableAware: true);

        var metadata = new ProjectMetadata(
            ProjectPath: "Sample.csproj",
            SourceFiles: [],
            Types: [indexedDto, holder],
            Diagnostics: []);
        var factory = new TemplateCodeModelAdapterFactory(metadata: metadata, templatePath: "models.tst");

        var file = factory.CreateFile(project: metadata);

        var holderClass = file.Classes.Single(predicate: c => c.Name == "Holder");
        var dtoProperty = holderClass.Properties.Should().ContainSingle().Which;
        dtoProperty.Type.FileLocations.Should().Equal(secondAssemblyPath);
        dtoProperty.Type.IsDefined.Should().BeTrue();
    }

    [Fact]
    public void CreateFileDisambiguatesNestedTypesByContainingTypeArity()
    {
        var nonGenericInnerPath = Path.Combine(path1: "Models", path2: "NonGenericOuter.cs");
        var genericInnerPath = Path.Combine(path1: "Models", path2: "GenericOuter.cs");

        var nonGenericInner = new TypeMetadata(
            Name: "Inner",
            FullName: "Sample.Outer.Inner",
            Namespace: "Sample",
            Kind: TypeMetadataKind.Class,
            Accessibility: MetadataAccessibility.Public,
            Properties: [],
            Attributes: [],
            BaseTypes: [],
            EnumValues: [],
            IsNullableAware: true)
        {
            ContainingTypeFullName = "Sample.Outer",
            FileLocations = [nonGenericInnerPath],
        };

        var genericInner = new TypeMetadata(
            Name: "Inner",
            FullName: "Sample.Outer.Inner",
            Namespace: "Sample",
            Kind: TypeMetadataKind.Class,
            Accessibility: MetadataAccessibility.Public,
            Properties: [],
            Attributes: [],
            BaseTypes: [],
            EnumValues: [],
            IsNullableAware: true)
        {
            ContainingTypeFullName = "Sample.Outer",
            FileLocations = [genericInnerPath],
        };

        var nonGenericOuter = CreateType(kind: TypeMetadataKind.Class, arity: 0) with
        {
            Name = "Outer",
            FullName = "Sample.Outer",
            NestedClasses = [nonGenericInner],
        };

        var genericOuter = CreateType(kind: TypeMetadataKind.Class, arity: 1) with
        {
            Name = "Outer",
            FullName = "Sample.Outer",
            NestedClasses = [genericInner],
        };

        var holder = new TypeMetadata(
            Name: "Holder",
            FullName: "Sample.Holder",
            Namespace: "Sample",
            Kind: TypeMetadataKind.Class,
            Accessibility: MetadataAccessibility.Public,
            Properties:
            [
                new PropertyMetadata(
                    Name: "FromNonGeneric",
                    FullName: "Sample.Holder.FromNonGeneric",
                    Type: CreateReference(name: "Inner", fullName: "Sample.Outer.Inner") with
                    {
                        ContainingType = CreateReference(name: "Outer", fullName: "Sample.Outer"),
                    },
                    Accessibility: MetadataAccessibility.Public,
                    HasGetter: true,
                    HasSetter: true,
                    IsRequired: false,
                    Attributes: []),
                new PropertyMetadata(
                    Name: "FromGeneric",
                    FullName: "Sample.Holder.FromGeneric",
                    Type: CreateReference(name: "Inner", fullName: "Sample.Outer.Inner") with
                    {
                        ContainingType = CreateReference(name: "Outer", fullName: "Sample.Outer") with
                        {
                            Arity = 1,
                        },
                    },
                    Accessibility: MetadataAccessibility.Public,
                    HasGetter: true,
                    HasSetter: true,
                    IsRequired: false,
                    Attributes: []),
            ],
            Attributes: [],
            BaseTypes: [],
            EnumValues: [],
            IsNullableAware: true);

        var metadata = new ProjectMetadata(
            ProjectPath: "Sample.csproj",
            SourceFiles: [],
            Types: [nonGenericOuter, genericOuter, holder],
            Diagnostics: []);
        var factory = new TemplateCodeModelAdapterFactory(metadata: metadata, templatePath: "models.tst");

        var file = factory.CreateFile(project: metadata);

        var holderClass = file.Classes.Single(predicate: c => c.Name == "Holder");
        var fromNonGeneric = holderClass.Properties.Single(predicate: p => p.Name == "FromNonGeneric");
        fromNonGeneric.Type.FileLocations.Should().Equal(nonGenericInnerPath);

        var fromGeneric = holderClass.Properties.Single(predicate: p => p.Name == "FromGeneric");
        fromGeneric.Type.FileLocations.Should().Equal(genericInnerPath);
    }

    [Fact]
    public void CreateFileRejectsIndexedMetadataWhenReferencedAssemblyIsNotIndexed()
    {
        var sourcePath = Path.Combine(path1: "ProjectA", path2: "Models", path3: "SharedDto.cs");
        var nestedSourcePath = Path.Combine(path1: "ProjectA", path2: "Models", path3: "Outer.cs");

        var indexedDto = new TypeMetadata(
            Name: "SharedDto",
            FullName: "Sample.Models.SharedDto",
            Namespace: "Sample.Models",
            Kind: TypeMetadataKind.Class,
            Accessibility: MetadataAccessibility.Public,
            Properties: [],
            Attributes: [],
            BaseTypes: [],
            EnumValues: [],
            IsNullableAware: true)
        {
            AssemblyName = "ProjectA",
            FileLocations = [sourcePath],
        };

        var indexedNested = new TypeMetadata(
            Name: "Inner",
            FullName: "Sample.Models.Outer.Inner",
            Namespace: "Sample.Models",
            Kind: TypeMetadataKind.Class,
            Accessibility: MetadataAccessibility.Public,
            Properties: [],
            Attributes: [],
            BaseTypes: [],
            EnumValues: [],
            IsNullableAware: true)
        {
            AssemblyName = "ProjectA",
            ContainingTypeFullName = "Sample.Models.Outer",
            FileLocations = [nestedSourcePath],
        };

        var indexedOuter = new TypeMetadata(
            Name: "Outer",
            FullName: "Sample.Models.Outer",
            Namespace: "Sample.Models",
            Kind: TypeMetadataKind.Class,
            Accessibility: MetadataAccessibility.Public,
            Properties: [],
            Attributes: [],
            BaseTypes: [],
            EnumValues: [],
            IsNullableAware: true)
        {
            AssemblyName = "ProjectA",
            FileLocations = [nestedSourcePath],
            NestedClasses = [indexedNested],
        };

        var holder = new TypeMetadata(
            Name: "Holder",
            FullName: "Sample.Models.Holder",
            Namespace: "Sample.Models",
            Kind: TypeMetadataKind.Class,
            Accessibility: MetadataAccessibility.Public,
            Properties:
            [
                new PropertyMetadata(
                    Name: "ExternalDto",
                    FullName: "Sample.Models.Holder.ExternalDto",
                    Type: CreateReference(name: "SharedDto", fullName: "Sample.Models.SharedDto") with
                    {
                        AssemblyName = "ExternalLibrary",
                    },
                    Accessibility: MetadataAccessibility.Public,
                    HasGetter: true,
                    HasSetter: true,
                    IsRequired: false,
                    Attributes: []),
                new PropertyMetadata(
                    Name: "ExternalNestedWithoutContainingType",
                    FullName: "Sample.Models.Holder.ExternalNestedWithoutContainingType",
                    Type: CreateReference(name: "Inner", fullName: "Sample.Models.Outer.Inner") with
                    {
                        AssemblyName = "ExternalLibrary",
                    },
                    Accessibility: MetadataAccessibility.Public,
                    HasGetter: true,
                    HasSetter: true,
                    IsRequired: false,
                    Attributes: []),
            ],
            Attributes: [],
            BaseTypes: [],
            EnumValues: [],
            IsNullableAware: true);

        var metadata = new ProjectMetadata(
            ProjectPath: "Sample.csproj",
            SourceFiles: [],
            Types: [indexedDto, indexedOuter, holder],
            Diagnostics: []);
        var factory = new TemplateCodeModelAdapterFactory(metadata: metadata, templatePath: "models.tst");

        var file = factory.CreateFile(project: metadata);

        var holderClass = file.Classes.Single(predicate: c => c.Name == "Holder");
        var externalDto = holderClass.Properties.Single(predicate: p => p.Name == "ExternalDto");
        externalDto.Type.FileLocations.Should().BeEmpty();
        externalDto.Type.IsDefined.Should().BeFalse();

        var externalNested = holderClass.Properties.Single(predicate: p => p.Name == "ExternalNestedWithoutContainingType");
        externalNested.Type.FileLocations.Should().BeEmpty();
        externalNested.Type.IsDefined.Should().BeFalse();
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
