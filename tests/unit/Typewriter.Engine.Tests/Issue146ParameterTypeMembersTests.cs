using System.Collections.Generic;
using AwesomeAssertions;
using Typewriter.Abstractions;
using Typewriter.CodeModel;
using Typewriter.Configuration;
using Xunit;
using CodeClass = Typewriter.CodeModel.Class;
using CodeMethod = Typewriter.CodeModel.Method;
using CodeType = Typewriter.CodeModel.Type;

namespace Typewriter.Engine.Tests;

public sealed class Issue146ParameterTypeMembersTests
{
    [Fact]
    public void RenderCollectsFieldsAndPropertiesRecursivelyAcrossParameterTypeBaseClassesIssue146()
    {
        var metadata = CreateControllerHierarchyMetadata();
        const string TemplateText = """
            ${
                private void CollectTypeProperties( Type t, List<string> props ){
                    Break();
                    props.AddRange( t.Fields.Select( p => "'" + p.Name + "'" ).ToList() );
                    props.AddRange( t.Properties.Select( p => "'" + p.Name + "'" ).ToList() );
                    if( t.BaseClass != null ){
                        CollectTypeProperties(t.BaseClass, props);
                    }
                }

                string ParameterMembers(Parameter parameter)
                {
                    var props = new List<string>();
                    CollectTypeProperties(parameter.Type, props);
                    return string.Join(", ", props);
                }
            }
            $Classes(Name=OrdersController)[$Methods[$Name($Parameters[$Name=[$ParameterMembers]][; ])]]
            """;

        var diagnostics = new List<GenerationDiagnostic>();
        var document = TemplateDocument.Parse(
            template: new TemplateFile(Path: "Services.tst", Content: TemplateText),
            diagnostics: diagnostics);
        var renderer = new TemplateRenderer(typeMapper: new TypeScriptTypeMapper());

        var output = renderer.Render(template: document, metadata: metadata, diagnostics: diagnostics);

        diagnostics.Should().BeEmpty();
        output.Trim().Should().Be(
            "Submit(request=['IncludeDrafts', 'OrderNumber', 'Amount', 'RawCorrelation', 'Id', 'RootField', 'TenantCode']; retryCount=[])");
    }

    [Fact]
    public void RenderAllowsTemplateToDefineOwnBreakMethodWithoutConflict()
    {
        var metadata = CreateControllerHierarchyMetadata();
        const string TemplateText = """
            ${
                private int _breakCalls;

                private void Break()
                {
                    _breakCalls++;
                }

                string CountWithCustomBreak(Class c)
                {
                    Break();
                    return $"{c.Name}:{_breakCalls}";
                }
            }
            $Classes(Name=OrdersController)[$CountWithCustomBreak]
            """;

        var diagnostics = new List<GenerationDiagnostic>();
        var document = TemplateDocument.Parse(
            template: new TemplateFile(Path: "CustomBreak.tst", Content: TemplateText),
            diagnostics: diagnostics);
        var renderer = new TemplateRenderer(typeMapper: new TypeScriptTypeMapper());

        var output = renderer.Render(template: document, metadata: metadata, diagnostics: diagnostics);

        diagnostics.Should().BeEmpty();
        output.Trim().Should().Be("OrdersController:1");
    }

    [Fact]
    public void TryAdaptSubstitutesGenericTypeArgumentsAcrossParameterTypeAndBaseClassMembers()
    {
        var metadata = CreateGenericParameterMetadata();
        var factory = new TemplateCodeModelAdapterFactory(metadata: metadata, templatePath: "Services.tst");
        var controllerMetadata = metadata.Types.Single(predicate: item => item.Name == "OrdersController");

        factory.TryAdapt(context: controllerMetadata, targetType: typeof(CodeClass), adapted: out var adapted)
            .Should().BeTrue();
        var controller = adapted.Should().BeOfType<CodeClass>().Subject;
        var method = controller.Methods.Should().ContainSingle(item => item.Name == "SubmitEnvelope").Which;
        var envelopeParam = method.Parameters.Should().ContainSingle().Which;
        var envelopeType = envelopeParam.Type;

        envelopeType.IsGeneric.Should().BeTrue();
        envelopeType.TypeParameters.Select(selector: p => p.Name).Should().Equal("TPayload");

        var payloadProp = envelopeType.Properties.Should().ContainSingle(item => item.Name == "Payload").Which;
        payloadProp.Parent.Should().BeSameAs(envelopeType);
        payloadProp.Type.Name.Should().Be("OrderLineDto");
        payloadProp.Type.Properties.Select(selector: p => p.Name).Should().Equal("Sku", "Quantity");

        var batchProp = envelopeType.Properties.Should().ContainSingle(item => item.Name == "Batch").Which;
        batchProp.Type.Name.Should().Be("OrderLineDto[]");
        batchProp.Type.IsEnumerable.Should().BeTrue();
        var batchElementType = batchProp.Type.ElementType;
        batchElementType.Should().NotBeNull();
        batchElementType.Properties.Select(selector: p => p.Name).Should().Equal("Sku", "Quantity");

        var rawPayloadField = envelopeType.Fields.Should().ContainSingle(item => item.Name == "RawPayload").Which;
        var rawPayloadType = rawPayloadField.Type;
        rawPayloadType.Should().NotBeNull();
        rawPayloadType.Name.Should().Be("OrderLineDto");

        var transformMethod = envelopeType.Methods.Should().ContainSingle(item => item.Name == "Transform").Which;
        transformMethod.Type.Name.Should().Be("OrderLineDto");
        transformMethod.Parameters.Should().ContainSingle().Which.Type.Name.Should().Be("OrderLineDto");

        var shadowedMethod = envelopeType.Methods.Should().ContainSingle(item => item.Name == "EchoShadowed").Which;
        shadowedMethod.Type.Name.Should().Be("TPayload");

        var baseClass = envelopeType.BaseClass;
        baseClass.Should().NotBeNull();
        baseClass.Properties.Should().ContainSingle(item => item.Name == "EnvelopeId").Which.Type.Name.Should().Be("string");
        CodeType? implicitBaseType = baseClass;
        implicitBaseType.Should().NotBeNull();
        implicitBaseType.Properties.Should().ContainSingle(item => item.Name == "EnvelopeId").Which.Type.Name.Should().Be("string");
    }

    [Fact]
    public void TryAdaptPopulatesRecordParameterBaseClassAndUnwrappedReturnTypeMembers()
    {
        var stringRef = CreateScalarReference(name: "string", fullName: "System.String", isPrimitive: true);
        var baseRecordMetadata = new TypeMetadata(
            Name: "AuditRecordBase",
            FullName: "Sample.AuditRecordBase",
            Namespace: "Sample",
            Kind: TypeMetadataKind.Record,
            Accessibility: MetadataAccessibility.Public,
            Properties:
            [
                CreateProperty(
                    name: "CreatedBy",
                    parentFullName: "Sample.AuditRecordBase",
                    type: stringRef,
                    hasSetter: false),
            ],
            Attributes: [],
            BaseTypes: [],
            EnumValues: [],
            IsNullableAware: true)
        {
            AssemblyName = "Sample",
        };
        var derivedRecordRef = CreateDefinedReference(name: "CreateInvoiceCommand", fullName: "Sample.CreateInvoiceCommand");
        var derivedRecordMetadata = new TypeMetadata(
            Name: "CreateInvoiceCommand",
            FullName: "Sample.CreateInvoiceCommand",
            Namespace: "Sample",
            Kind: TypeMetadataKind.Record,
            Accessibility: MetadataAccessibility.Public,
            Properties:
            [
                CreateProperty(
                    name: "InvoiceNumber",
                    parentFullName: "Sample.CreateInvoiceCommand",
                    type: stringRef,
                    hasSetter: false),
            ],
            Attributes: [],
            BaseTypes: [CreateDefinedReference(name: "AuditRecordBase", fullName: "Sample.AuditRecordBase")],
            EnumValues: [],
            IsNullableAware: true)
        {
            AssemblyName = "Sample",
        };
        var taskReturnRef = new TypeMetadataReference(
            Name: "Task",
            FullName: "System.Threading.Tasks.Task",
            Namespace: "System.Threading.Tasks",
            IsNullable: false,
            IsCollection: false,
            IsDictionary: false,
            IsEnum: false,
            IsPrimitive: false,
            IsDateLike: false,
            ElementType: null,
            TypeArguments: [derivedRecordRef])
        {
            Arity = 1,
            AssemblyName = "System.Runtime",
        };
        var methodMetadata = CreateMethod(
            name: "Create",
            parentFullName: "Sample.InvoicesController",
            returnType: taskReturnRef,
            parameters:
            [
                CreateParameter(
                    name: "command",
                    parentMethodFullName: "Sample.InvoicesController.Create",
                    type: derivedRecordRef with { IsNullable = true }),
            ]);
        var metadata = new ProjectMetadata(
            ProjectPath: "Sample.csproj",
            SourceFiles: [],
            Types: [baseRecordMetadata, derivedRecordMetadata],
            Diagnostics: []);
        var factory = new TemplateCodeModelAdapterFactory(metadata: metadata, templatePath: "Services.tst");

        factory.TryAdapt(context: methodMetadata, targetType: typeof(CodeMethod), adapted: out var adapted).Should().BeTrue();
        var method = adapted.Should().BeOfType<CodeMethod>().Subject;
        var paramType = method.Parameters.Single().Type;
        paramType.IsNullable.Should().BeTrue();
        paramType.Properties.Select(selector: p => p.Name).Should().Equal("InvoiceNumber");
        var paramBaseClass = paramType.BaseClass;
        paramBaseClass.Should().NotBeNull();
        paramBaseClass.Properties.Select(selector: p => p.Name).Should().Equal("CreatedBy");

        var unwrappedReturn = Typewriter.Extensions.Types.TypeExtensions.Unwrap(type: method.Type);
        unwrappedReturn.Properties.Select(selector: p => p.Name).Should().Equal("InvoiceNumber");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SelfReferentialParameterTypeDoesNotOverflowWithBuiltInOrCustomSettings(bool useCustomSettings)
    {
        var treeNodeRef = CreateDefinedReference(name: "TreeNode", fullName: "Sample.TreeNode") with
        {
            IsNullable = true,
        };
        var dateTimeRef = CreateScalarReference(name: "DateTime", fullName: "System.DateTime", isPrimitive: false) with
        {
            IsDateLike = true,
        };
        var treeNodeMetadata = new TypeMetadata(
            Name: "TreeNode",
            FullName: "Sample.TreeNode",
            Namespace: "Sample",
            Kind: TypeMetadataKind.Class,
            Accessibility: MetadataAccessibility.Public,
            Properties:
            [
                CreateProperty(name: "ParentNode", parentFullName: "Sample.TreeNode", type: treeNodeRef),
                CreateProperty(name: "CreatedAt", parentFullName: "Sample.TreeNode", type: dateTimeRef),
            ],
            Attributes: [],
            BaseTypes: [],
            EnumValues: [],
            IsNullableAware: true)
        {
            AssemblyName = "Sample",
        };
        var metadata = new ProjectMetadata(
            ProjectPath: "Sample.csproj",
            SourceFiles: [],
            Types: [treeNodeMetadata],
            Diagnostics: []);
        Settings settings = useCustomSettings ? new CustomTestSettings() : new Settings();
        settings.UseDateType(dateType: "Temporal.Instant");
        var factory = new TemplateCodeModelAdapterFactory(metadata: metadata, settings: settings);

        var file = factory.CreateFile(project: metadata);
        file.Classes.Should().ContainSingle(item => item.Name == "TreeNode");

        factory.TryAdapt(context: treeNodeRef, targetType: typeof(CodeType), adapted: out var adapted).Should().BeTrue();
        if (!useCustomSettings)
        {
            settings.UseDateType(dateType: "MutatedLater");
        }

        var adaptedType = adapted.Should().BeOfType<MappedCodeType>().Subject;
        adaptedType.Properties.Select(selector: p => p.Name).Should().Equal("ParentNode", "CreatedAt");
        adaptedType.Properties.Single(predicate: p => p.Name == "CreatedAt").Type.Name.Should().Be("Temporal.Instant");
        adaptedType.Properties.Single(predicate: p => p.Name == "ParentNode").Type.Properties.Select(selector: p => p.Name)
            .Should().Equal("ParentNode", "CreatedAt");
        if (settings is CustomTestSettings customSettings)
        {
            customSettings.DateTypeReads.Should().BePositive();
        }
    }

    [Fact]
    public void ParameterTypeRespectsIncludeInternalsSetting()
    {
        var metadata = CreateControllerHierarchyMetadata();
        const string DefaultTemplate = """
            $Classes(Name=OrdersController)[$Methods[$Parameters[$Name:[$Type[$Properties[$Name][,]]|$Type[$Fields[$Name][,]]]]]]
            """;
        const string InternalsTemplate = """
            ${
                Template(Settings settings)
                {
                    settings.IncludeInternals();
                }
            }
            $Classes(Name=OrdersController)[$Methods[$Parameters[$Name:[$Type[$Properties[$Name][,]]|$Type[$Fields[$Name][,]]]]]]
            """;

        var renderer = new TemplateRenderer(typeMapper: new TypeScriptTypeMapper());
        var defaultDiagnostics = new List<GenerationDiagnostic>();
        var defaultDoc = TemplateDocument.Parse(
            template: new TemplateFile(Path: "Default.tst", Content: DefaultTemplate),
            diagnostics: defaultDiagnostics);
        var defaultOutput = renderer.Render(template: defaultDoc, metadata: metadata, diagnostics: defaultDiagnostics);

        defaultDiagnostics.Should().BeEmpty();
        defaultOutput.Trim().Should().Be("request:[OrderNumber,Amount|IncludeDrafts]retryCount:[|]");

        var internalsDiagnostics = new List<GenerationDiagnostic>();
        var internalsDoc = TemplateDocument.Parse(
            template: new TemplateFile(Path: "Internals.tst", Content: InternalsTemplate),
            diagnostics: internalsDiagnostics);
        var internalsOutput = renderer.Render(template: internalsDoc, metadata: metadata, diagnostics: internalsDiagnostics);

        internalsDiagnostics.Should().BeEmpty();
        internalsOutput.Trim().Should().Be("request:[OrderNumber,Amount,InternalChecksum|IncludeDrafts,InternalTag]retryCount:[|]");
    }

    [Fact]
    public void DirectTemplateSyntaxResolvesMembersAndHierarchyOnTypeReferences()
    {
        var metadata = CreateControllerHierarchyMetadata();
        const string TemplateText = """
            $Classes(Name=OrdersController)[$Methods[$Parameters[$Name($Type[$IsPrimitive]):props=[$Type[$Properties[$Name:$Type][, ]]] base=[$Type[$BaseClass[$Name:$Properties[$Name:$Type][, ]]|$BaseClass[$BaseClass[$Properties[$Name:$Type][, ]]]]]]]]
            """;

        var diagnostics = new List<GenerationDiagnostic>();
        var document = TemplateDocument.Parse(
            template: new TemplateFile(Path: "Direct.tst", Content: TemplateText),
            diagnostics: diagnostics);
        var renderer = new TemplateRenderer(typeMapper: new TypeScriptTypeMapper());

        var output = renderer.Render(template: document, metadata: metadata, diagnostics: diagnostics);

        diagnostics.Should().BeEmpty();
        output.Trim().Should().Be(
            "request(false):props=[OrderNumber:string, Amount:number] base=[MiddleRequest<string>:Id:string|TenantCode:string]retryCount(true):props=[] base=[|]");
    }

    [Fact]
    public void ReferencedTypesAreResolvedThroughTypeReferencesWithoutEmittingAsRootClasses()
    {
        var stringRef = CreateScalarReference(name: "string", fullName: "System.String", isPrimitive: true);
        var voidRef = CreateScalarReference(name: "void", fullName: "System.Void", isPrimitive: false);
        var generatedDtoMetadata = new TypeMetadata(
            Name: "GeneratedFilterDto",
            FullName: "Sample.GeneratedFilterDto",
            Namespace: "Sample",
            Kind: TypeMetadataKind.Class,
            Accessibility: MetadataAccessibility.Public,
            Properties:
            [
                CreateProperty(name: "SearchText", parentFullName: "Sample.GeneratedFilterDto", type: stringRef),
            ],
            Attributes: [],
            BaseTypes: [],
            EnumValues: [],
            IsNullableAware: true)
        {
            AssemblyName = "Sample",
        };
        var controllerMetadata = new TypeMetadata(
            Name: "SearchController",
            FullName: "Sample.SearchController",
            Namespace: "Sample",
            Kind: TypeMetadataKind.Class,
            Accessibility: MetadataAccessibility.Public,
            Properties: [],
            Attributes: [],
            BaseTypes: [],
            EnumValues: [],
            IsNullableAware: true)
        {
            AssemblyName = "Sample",
            Methods =
            [
                CreateMethod(
                    name: "Find",
                    parentFullName: "Sample.SearchController",
                    returnType: voidRef,
                    parameters:
                    [
                        CreateParameter(
                            name: "filterParam",
                            parentMethodFullName: "Sample.SearchController.Find",
                            type: CreateDefinedReference(name: "GeneratedFilterDto", fullName: "Sample.GeneratedFilterDto")),
                    ]),
            ],
        };
        var primary = new ProjectMetadata(
            ProjectPath: "Api.csproj",
            SourceFiles: [],
            Types: [controllerMetadata],
            Diagnostics: []);
        var contracts = new ProjectMetadata(
            ProjectPath: "Contracts.csproj",
            SourceFiles: [],
            Types: [],
            Diagnostics: [])
        {
            ReferencedTypes = [generatedDtoMetadata],
        };
        var merged = ProjectMetadataMerger.Merge(project: primary, includedProjects: [contracts]);

        const string TemplateText = """
            ${
                string HelperProps(Parameter p) => string.Join(",", p.Type.Properties.Select(x => x.Name));
            }
            roots=[$Classes[$Name][,]] param=[$Classes[$Methods[$Parameters[$HelperProps|$Type[$Properties[$Name][,]]]]]]
            """;
        var diagnostics = new List<GenerationDiagnostic>();
        var document = TemplateDocument.Parse(
            template: new TemplateFile(Path: "Generated.tst", Content: TemplateText),
            diagnostics: diagnostics);
        var renderer = new TemplateRenderer(typeMapper: new TypeScriptTypeMapper());

        var output = renderer.Render(template: document, metadata: merged, diagnostics: diagnostics);

        diagnostics.Should().BeEmpty();
        output.Trim().Should().Be("roots=[SearchController] param=[SearchText|SearchText]");
    }

    private static TypeMetadataReference CreateScalarReference(
        string name,
        string fullName,
        bool isPrimitive) =>
        new(
            Name: name,
            FullName: fullName,
            Namespace: "System",
            IsNullable: false,
            IsCollection: false,
            IsDictionary: false,
            IsEnum: false,
            IsPrimitive: isPrimitive,
            IsDateLike: false,
            ElementType: null,
            TypeArguments: [])
        {
            AssemblyName = "System.Runtime",
        };

    private static TypeMetadataReference CreateDefinedReference(
        string name,
        string fullName,
        IReadOnlyList<TypeMetadataReference>? typeArguments = null) =>
        new(
            Name: name,
            FullName: fullName,
            Namespace: "Sample",
            IsNullable: false,
            IsCollection: false,
            IsDictionary: false,
            IsEnum: false,
            IsPrimitive: false,
            IsDateLike: false,
            ElementType: null,
            TypeArguments: typeArguments ?? [])
        {
            AssemblyName = "Sample",
        };

    private static TypeMetadataReference CreateTypeParamReference(
        string name,
        string fullName) =>
        new(
            Name: name,
            FullName: fullName,
            Namespace: "Sample",
            IsNullable: false,
            IsCollection: false,
            IsDictionary: false,
            IsEnum: false,
            IsPrimitive: false,
            IsDateLike: false,
            ElementType: null,
            TypeArguments: []);

    private static PropertyMetadata CreateProperty(
        string name,
        string parentFullName,
        TypeMetadataReference type,
        MetadataAccessibility accessibility = MetadataAccessibility.Public,
        bool hasSetter = true) =>
        new(
            Name: name,
            FullName: $"{parentFullName}.{name}",
            Type: type,
            Accessibility: accessibility,
            HasGetter: true,
            HasSetter: hasSetter,
            IsRequired: false,
            Attributes: [])
        {
            ParentTypeFullName = parentFullName,
        };

    private static FieldMetadata CreateField(
        string name,
        string parentFullName,
        TypeMetadataReference type,
        MetadataAccessibility accessibility = MetadataAccessibility.Public) =>
        new(
            Name: name,
            FullName: $"{parentFullName}.{name}",
            Accessibility: accessibility,
            Type: type,
            Attributes: [],
            ParentTypeFullName: parentFullName);

    private static ParameterMetadata CreateParameter(
        string name,
        string parentMethodFullName,
        TypeMetadataReference type) =>
        new(
            Name: name,
            FullName: $"{parentMethodFullName}.{name}",
            Type: type,
            HasDefaultValue: false,
            DefaultValue: null,
            Attributes: [],
            ParentMethodFullName: parentMethodFullName);

    private static MethodMetadata CreateMethod(
        string name,
        string parentFullName,
        TypeMetadataReference returnType,
        IReadOnlyList<ParameterMetadata> parameters,
        bool isGeneric = false,
        IReadOnlyList<TypeParameterMetadata>? typeParameters = null) =>
        new(
            Name: name,
            FullName: $"{parentFullName}.{name}",
            ReturnType: returnType,
            Accessibility: MetadataAccessibility.Public,
            IsStatic: false,
            IsAbstract: false,
            IsGeneric: isGeneric,
            Parameters: parameters,
            Attributes: [],
            ParentTypeFullName: parentFullName)
        {
            TypeParameters = typeParameters ?? [],
        };

#pragma warning disable MA0051
    private static ProjectMetadata CreateControllerHierarchyMetadata()
    {
        var stringRef = CreateScalarReference(name: "string", fullName: "System.String", isPrimitive: true);
        var intRef = CreateScalarReference(name: "int", fullName: "System.Int32", isPrimitive: true);
        var decimalRef = CreateScalarReference(name: "decimal", fullName: "System.Decimal", isPrimitive: false);
        var boolRef = CreateScalarReference(name: "bool", fullName: "System.Boolean", isPrimitive: true);
        var guidRef = CreateScalarReference(name: "Guid", fullName: "System.Guid", isPrimitive: false);
        var voidRef = CreateScalarReference(name: "void", fullName: "System.Void", isPrimitive: false);

        var rootBaseMetadata = new TypeMetadata(
            Name: "RootRequest",
            FullName: "Sample.RootRequest",
            Namespace: "Sample",
            Kind: TypeMetadataKind.Class,
            Accessibility: MetadataAccessibility.Public,
            Properties:
            [
                CreateProperty(name: "TenantCode", parentFullName: "Sample.RootRequest", type: stringRef),
            ],
            Attributes: [],
            BaseTypes: [],
            EnumValues: [],
            IsNullableAware: true)
        {
            AssemblyName = "Sample",
            Fields =
            [
                CreateField(name: "RootField", parentFullName: "Sample.RootRequest", type: stringRef),
            ],
        };

        var middleTypeParam = CreateTypeParamReference(name: "TId", fullName: "Sample.MiddleRequest.TId");

        var middleBaseMetadata = new TypeMetadata(
            Name: "MiddleRequest",
            FullName: "Sample.MiddleRequest",
            Namespace: "Sample",
            Kind: TypeMetadataKind.Class,
            Accessibility: MetadataAccessibility.Public,
            Properties:
            [
                CreateProperty(name: "Id", parentFullName: "Sample.MiddleRequest", type: middleTypeParam),
            ],
            Attributes: [],
            BaseTypes:
            [
                CreateDefinedReference(name: "RootRequest", fullName: "Sample.RootRequest"),
            ],
            EnumValues: [],
            IsNullableAware: true)
        {
            AssemblyName = "Sample",
            TypeParameters = [new TypeParameterMetadata(Name: "TId") { FullName = "Sample.MiddleRequest.TId" }],
            Fields =
            [
                CreateField(name: "RawCorrelation", parentFullName: "Sample.MiddleRequest", type: middleTypeParam),
            ],
        };

        var submitRequestMetadata = new TypeMetadata(
            Name: "SubmitOrderRequest",
            FullName: "Sample.SubmitOrderRequest",
            Namespace: "Sample",
            Kind: TypeMetadataKind.Class,
            Accessibility: MetadataAccessibility.Public,
            Properties:
            [
                CreateProperty(name: "OrderNumber", parentFullName: "Sample.SubmitOrderRequest", type: stringRef),
                CreateProperty(name: "Amount", parentFullName: "Sample.SubmitOrderRequest", type: decimalRef),
                CreateProperty(
                    name: "InternalChecksum",
                    parentFullName: "Sample.SubmitOrderRequest",
                    type: stringRef,
                    accessibility: MetadataAccessibility.Internal),
            ],
            Attributes: [],
            BaseTypes:
            [
                CreateDefinedReference(name: "MiddleRequest", fullName: "Sample.MiddleRequest", typeArguments: [guidRef]),
            ],
            EnumValues: [],
            IsNullableAware: true)
        {
            AssemblyName = "Sample",
            Fields =
            [
                CreateField(name: "IncludeDrafts", parentFullName: "Sample.SubmitOrderRequest", type: boolRef),
                CreateField(
                    name: "InternalTag",
                    parentFullName: "Sample.SubmitOrderRequest",
                    type: stringRef,
                    accessibility: MetadataAccessibility.Internal),
            ],
        };

        var submitRequestRef = CreateDefinedReference(name: "SubmitOrderRequest", fullName: "Sample.SubmitOrderRequest");

        var controllerMetadata = new TypeMetadata(
            Name: "OrdersController",
            FullName: "Sample.OrdersController",
            Namespace: "Sample",
            Kind: TypeMetadataKind.Class,
            Accessibility: MetadataAccessibility.Public,
            Properties: [],
            Attributes: [],
            BaseTypes: [],
            EnumValues: [],
            IsNullableAware: true)
        {
            AssemblyName = "Sample",
            Methods =
            [
                CreateMethod(
                    name: "Submit",
                    parentFullName: "Sample.OrdersController",
                    returnType: voidRef,
                    parameters:
                    [
                        CreateParameter(name: "request", parentMethodFullName: "Sample.OrdersController.Submit", type: submitRequestRef),
                        CreateParameter(name: "retryCount", parentMethodFullName: "Sample.OrdersController.Submit", type: intRef),
                    ]),
            ],
        };

        return new ProjectMetadata(
            ProjectPath: "Sample.csproj",
            SourceFiles: [],
            Types: [rootBaseMetadata, middleBaseMetadata, submitRequestMetadata, controllerMetadata],
            Diagnostics: []);
    }

    private static ProjectMetadata CreateGenericParameterMetadata()
    {
        var stringRef = CreateScalarReference(name: "string", fullName: "System.String", isPrimitive: true);
        var intRef = CreateScalarReference(name: "int", fullName: "System.Int32", isPrimitive: true);
        var guidRef = CreateScalarReference(name: "Guid", fullName: "System.Guid", isPrimitive: false);
        var voidRef = CreateScalarReference(name: "void", fullName: "System.Void", isPrimitive: false);

        var orderLineDtoMetadata = new TypeMetadata(
            Name: "OrderLineDto",
            FullName: "Sample.OrderLineDto",
            Namespace: "Sample",
            Kind: TypeMetadataKind.Class,
            Accessibility: MetadataAccessibility.Public,
            Properties:
            [
                CreateProperty(name: "Sku", parentFullName: "Sample.OrderLineDto", type: stringRef),
                CreateProperty(name: "Quantity", parentFullName: "Sample.OrderLineDto", type: intRef),
            ],
            Attributes: [],
            BaseTypes: [],
            EnumValues: [],
            IsNullableAware: true)
        {
            AssemblyName = "Sample",
        };

        var orderLineRef = CreateDefinedReference(name: "OrderLineDto", fullName: "Sample.OrderLineDto");
        var baseIdParam = CreateTypeParamReference(name: "TKey", fullName: "Sample.EnvelopeBase.TKey");

        var envelopeBaseMetadata = new TypeMetadata(
            Name: "EnvelopeBase",
            FullName: "Sample.EnvelopeBase",
            Namespace: "Sample",
            Kind: TypeMetadataKind.Class,
            Accessibility: MetadataAccessibility.Public,
            Properties:
            [
                CreateProperty(name: "EnvelopeId", parentFullName: "Sample.EnvelopeBase", type: baseIdParam),
            ],
            Attributes: [],
            BaseTypes: [],
            EnumValues: [],
            IsNullableAware: true)
        {
            AssemblyName = "Sample",
            TypeParameters = [new TypeParameterMetadata(Name: "TKey") { FullName = "Sample.EnvelopeBase.TKey" }],
        };

        var payloadTypeParam = CreateTypeParamReference(name: "TPayload", fullName: "Sample.Envelope.TPayload");
        var batchListRef = new TypeMetadataReference(
            Name: "List",
            FullName: "System.Collections.Generic.List",
            Namespace: "System.Collections.Generic",
            IsNullable: false,
            IsCollection: true,
            IsDictionary: false,
            IsEnum: false,
            IsPrimitive: false,
            IsDateLike: false,
            ElementType: payloadTypeParam,
            TypeArguments: [payloadTypeParam])
        {
            Arity = 1,
            AssemblyName = "System.Collections",
        };

        var envelopeMetadata = new TypeMetadata(
            Name: "Envelope",
            FullName: "Sample.Envelope",
            Namespace: "Sample",
            Kind: TypeMetadataKind.Class,
            Accessibility: MetadataAccessibility.Public,
            Properties:
            [
                CreateProperty(name: "Payload", parentFullName: "Sample.Envelope", type: payloadTypeParam),
                CreateProperty(name: "Batch", parentFullName: "Sample.Envelope", type: batchListRef),
            ],
            Attributes: [],
            BaseTypes:
            [
                CreateDefinedReference(name: "EnvelopeBase", fullName: "Sample.EnvelopeBase", typeArguments: [guidRef]),
            ],
            EnumValues: [],
            IsNullableAware: true)
        {
            AssemblyName = "Sample",
            TypeParameters = [new TypeParameterMetadata(Name: "TPayload") { FullName = "Sample.Envelope.TPayload" }],
            Fields =
            [
                CreateField(name: "RawPayload", parentFullName: "Sample.Envelope", type: payloadTypeParam),
            ],
            Methods =
            [
                CreateMethod(
                    name: "Transform",
                    parentFullName: "Sample.Envelope",
                    returnType: payloadTypeParam,
                    parameters:
                    [
                        CreateParameter(name: "input", parentMethodFullName: "Sample.Envelope.Transform", type: payloadTypeParam),
                    ]),
                CreateMethod(
                    name: "EchoShadowed",
                    parentFullName: "Sample.Envelope",
                    returnType: payloadTypeParam,
                    parameters: [],
                    isGeneric: true,
                    typeParameters: [new TypeParameterMetadata(Name: "TPayload") { FullName = "Sample.Envelope.EchoShadowed.TPayload" }]),
            ],
        };

        var controllerMetadata = new TypeMetadata(
            Name: "OrdersController",
            FullName: "Sample.OrdersController",
            Namespace: "Sample",
            Kind: TypeMetadataKind.Class,
            Accessibility: MetadataAccessibility.Public,
            Properties: [],
            Attributes: [],
            BaseTypes: [],
            EnumValues: [],
            IsNullableAware: true)
        {
            AssemblyName = "Sample",
            Methods =
            [
                CreateMethod(
                    name: "SubmitEnvelope",
                    parentFullName: "Sample.OrdersController",
                    returnType: voidRef,
                    parameters:
                    [
                        CreateParameter(
                            name: "envelopeArg",
                            parentMethodFullName: "Sample.OrdersController.SubmitEnvelope",
                            type: CreateDefinedReference(
                                name: "Envelope",
                                fullName: "Sample.Envelope",
                                typeArguments: [orderLineRef])),
                    ]),
            ],
        };

        return new ProjectMetadata(
            ProjectPath: "Sample.csproj",
            SourceFiles: [],
            Types: [orderLineDtoMetadata, envelopeBaseMetadata, envelopeMetadata, controllerMetadata],
            Diagnostics: []);
    }
#pragma warning restore MA0051

    private sealed class CustomTestSettings : Settings
    {
        public int DateTypeReads { get; private set; }

        public override string DateTypeGeneration
        {
            get
            {
                DateTypeReads++;
                return base.DateTypeGeneration;
            }
        }
    }
}
