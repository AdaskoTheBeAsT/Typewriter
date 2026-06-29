using Typewriter.Abstractions;
using Typewriter.Engine;
using Xunit;

namespace Typewriter.Engine.Tests;

public sealed class TemplateRendererTests
{
    [Fact]
    public void RenderExpandsClassesPropertiesAndEnums()
    {
        var metadata = new ProjectMetadata(
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
                            Name: "DisplayName",
                            FullName: "Sample.User.DisplayName",
                            Type: TypeReference(name: "String", fullName: "System.String", isNullable: false),
                            Accessibility: MetadataAccessibility.Public,
                            HasGetter: true,
                            HasSetter: true,
                            IsRequired: true,
                            Attributes: []),
                        new PropertyMetadata(
                            Name: "Email",
                            FullName: "Sample.User.Email",
                            Type: TypeReference(name: "String", fullName: "System.String", isNullable: true),
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
                new TypeMetadata(
                    Name: "OrderStatus",
                    FullName: "Sample.OrderStatus",
                    Namespace: "Sample",
                    Kind: TypeMetadataKind.Enum,
                    Accessibility: MetadataAccessibility.Public,
                    Properties: [],
                    Attributes: [],
                    BaseTypes: [],
                    EnumValues: [new EnumValueMetadata(Name: "Draft", Value: 0), new EnumValueMetadata(Name: "Paid", Value: 1)],
                    IsNullableAware: true),
            ],
            Diagnostics: []);
        const string template = """
            $Classes[
            export interface $Name {
            $Properties[
              $name: $Type;]
            }
            ]
            $Enums[
            export enum $Name {
            $Values[
              $Name = $Value,]
            }
            ]
            """;
        var document = new TemplateDocument(Path: "models.tst", Content: template, OutputPath: "models.ts");
        var diagnostics = new List<GenerationDiagnostic>();
        var renderer = new TemplateRenderer(typeMapper: new TypeScriptTypeMapper());

        var output = renderer.Render(template: document, metadata: metadata, diagnostics: diagnostics);

        diagnostics.Should().BeEmpty();
        output.Should().Contain("export interface User");
        output.Should().Contain("displayName: string;");
        output.Should().Contain("email: string | null;");
        output.Should().Contain("export enum OrderStatus");
        output.Should().Contain("Paid = 1");
    }

    [Fact]
    public void RenderExpandsStructsAndStructFilter()
    {
        var metadata = new ProjectMetadata(
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
                    Properties: [],
                    Attributes: [],
                    BaseTypes: [],
                    EnumValues: [],
                    IsNullableAware: true),
                new TypeMetadata(
                    Name: "Point",
                    FullName: "Sample.Point",
                    Namespace: "Sample",
                    Kind: TypeMetadataKind.Struct,
                    Accessibility: MetadataAccessibility.Public,
                    Properties:
                    [
                        new PropertyMetadata(
                            Name: "X",
                            FullName: "Sample.Point.X",
                            Type: TypeReference(name: "Int32", fullName: "System.Int32", isNullable: false),
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
        const string template = "$Structs[struct $Name {$Properties[$name:$Type;]}] filtered:$Types(Struct)[$Name][,]";
        var document = new TemplateDocument(Path: "models.tst", Content: template, OutputPath: "models.ts");
        var diagnostics = new List<GenerationDiagnostic>();
        var renderer = new TemplateRenderer(typeMapper: new TypeScriptTypeMapper());

        var output = renderer.Render(template: document, metadata: metadata, diagnostics: diagnostics);

        diagnostics.Should().BeEmpty();
        output.Should().Contain("struct Point");
        output.Should().Contain("x:number;");
        output.Should().Contain("filtered:Point");
        output.Should().NotContain("filtered:User");
    }

    [Fact]
    public void RenderContainingTypeAccessorsAreKindFiltered()
    {
        var metadata = new ProjectMetadata(
            ProjectPath: "Sample.csproj",
            SourceFiles: [],
            Types:
            [
                CreateType(name: "ClassContainer", kind: TypeMetadataKind.Class),
                CreateType(name: "RecordContainer", kind: TypeMetadataKind.Record),
                CreateType(name: "StructContainer", kind: TypeMetadataKind.Struct),
                CreateType(name: "NestedInClass", kind: TypeMetadataKind.Struct, containingTypeFullName: "Sample.ClassContainer"),
                CreateType(name: "NestedInRecord", kind: TypeMetadataKind.Struct, containingTypeFullName: "Sample.RecordContainer"),
                CreateType(name: "NestedInStruct", kind: TypeMetadataKind.Struct, containingTypeFullName: "Sample.StructContainer"),
            ],
            Diagnostics: []);
        const string template = "$Types(Struct)[$Name:C=$ContainingClass[$Name];R=$ContainingRecord[$Name];S=$ContainingStruct[$Name];]";
        var document = new TemplateDocument(Path: "models.tst", Content: template, OutputPath: "models.ts");
        var diagnostics = new List<GenerationDiagnostic>();
        var renderer = new TemplateRenderer(typeMapper: new TypeScriptTypeMapper());

        var output = renderer.Render(template: document, metadata: metadata, diagnostics: diagnostics);

        diagnostics.Should().BeEmpty();
        output.Should().Contain("NestedInClass:C=ClassContainer;R=;S=;");
        output.Should().Contain("NestedInRecord:C=;R=RecordContainer;S=;");
        output.Should().Contain("NestedInStruct:C=;R=;S=StructContainer;");

        static TypeMetadata CreateType(
            string name,
            TypeMetadataKind kind,
            string containingTypeFullName = "")
        {
            return new TypeMetadata(
                Name: name,
                FullName: "Sample." + name,
                Namespace: "Sample",
                Kind: kind,
                Accessibility: MetadataAccessibility.Public,
                Properties: [],
                Attributes: [],
                BaseTypes: [],
                EnumValues: [],
                IsNullableAware: true)
            {
                ContainingTypeFullName = containingTypeFullName,
            };
        }
    }

    [Fact]
    public void RenderEscapedDollarSignsAsLiterals()
    {
        var metadata = new ProjectMetadata(
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
                    Properties: [],
                    Attributes: [],
                    BaseTypes: [],
                    EnumValues: [],
                    IsNullableAware: true),
            ],
            Diagnostics: []);
        const string template = "$Classes[$$type $$Name $${environment.apiBaseUrl} $$$Name]";
        var document = new TemplateDocument(Path: "models.tst", Content: template, OutputPath: "models.ts");
        var diagnostics = new List<GenerationDiagnostic>();
        var renderer = new TemplateRenderer(typeMapper: new TypeScriptTypeMapper());

        var output = renderer.Render(template: document, metadata: metadata, diagnostics: diagnostics);

        diagnostics.Should().BeEmpty();
        output.Should().Be("$type $Name ${environment.apiBaseUrl} $User");
    }

    [Fact]
    public void RenderExposesIndexerPropertiesAndParameters()
    {
        var metadata = new ProjectMetadata(
            ProjectPath: "Sample.csproj",
            SourceFiles: [],
            Types:
            [
                new TypeMetadata(
                    Name: "Lookup",
                    FullName: "Sample.Lookup",
                    Namespace: "Sample",
                    Kind: TypeMetadataKind.Class,
                    Accessibility: MetadataAccessibility.Public,
                    Properties:
                    [
                        new PropertyMetadata(
                            Name: "Item",
                            FullName: "Sample.Lookup.Item",
                            Type: TypeReference(name: "String", fullName: "System.String", isNullable: false),
                            Accessibility: MetadataAccessibility.Public,
                            HasGetter: true,
                            HasSetter: true,
                            IsRequired: false,
                            Attributes: [])
                        {
                            IsIndexer = true,
                            Parameters =
                            [
                                new ParameterMetadata(
                                    Name: "index",
                                    FullName: "Sample.Lookup.Item.index",
                                    Type: TypeReference(name: "Int32", fullName: "System.Int32", isNullable: false),
                                    HasDefaultValue: false,
                                    DefaultValue: null,
                                    Attributes: [],
                                    ParentMethodFullName: string.Empty)
                                {
                                    ParentPropertyFullName = "Sample.Lookup.Item",
                                },
                                new ParameterMetadata(
                                    Name: "key",
                                    FullName: "Sample.Lookup.Item.key",
                                    Type: TypeReference(name: "String", fullName: "System.String", isNullable: true),
                                    HasDefaultValue: false,
                                    DefaultValue: null,
                                    Attributes: [],
                                    ParentMethodFullName: string.Empty)
                                {
                                    ParentPropertyFullName = "Sample.Lookup.Item",
                                },
                            ],
                        },
                    ],
                    Attributes: [],
                    BaseTypes: [],
                    EnumValues: [],
                    IsNullableAware: true),
            ],
            Diagnostics: []);
        const string template = "$Classes[$Properties[$IsIndexer[$Parameters[$name:$Type][, ]->$Type;]]]";
        var document = new TemplateDocument(Path: "models.tst", Content: template, OutputPath: "models.ts");
        var diagnostics = new List<GenerationDiagnostic>();
        var renderer = new TemplateRenderer(typeMapper: new TypeScriptTypeMapper());

        var output = renderer.Render(template: document, metadata: metadata, diagnostics: diagnostics);

        diagnostics.Should().BeEmpty();
        output.Should().Contain("index:number, key:string | null->string;");
    }

    [Fact]
    public void RenderResolvesPropertyAndFieldInitializerValue()
    {
        var stringType = TypeReference(name: "String", fullName: "System.String", isNullable: false);
        var metadata = new ProjectMetadata(
            ProjectPath: "Sample.csproj",
            SourceFiles: [],
            Types:
            [
                new TypeMetadata(
                    Name: "Holder",
                    FullName: "Sample.Holder",
                    Namespace: "Sample",
                    Kind: TypeMetadataKind.Class,
                    Accessibility: MetadataAccessibility.Public,
                    Properties:
                    [
                        new PropertyMetadata(
                            Name: "Type",
                            FullName: "Sample.Holder.Type",
                            Type: stringType,
                            Accessibility: MetadataAccessibility.Public,
                            HasGetter: true,
                            HasSetter: false,
                            IsRequired: false,
                            Attributes: [])
                        {
                            Value = "myTestType",
                        },
                    ],
                    Attributes: [],
                    BaseTypes: [],
                    EnumValues: [],
                    IsNullableAware: true)
                {
                    Fields =
                    [
                        new FieldMetadata(
                            Name: "Label",
                            FullName: "Sample.Holder.Label",
                            Accessibility: MetadataAccessibility.Public,
                            Type: stringType,
                            Attributes: [],
                            ParentTypeFullName: "Sample.Holder")
                        {
                            Value = "instance",
                        },
                    ],
                },
            ],
            Diagnostics: []);
        const string template = "$Classes[$Properties[$Name=$Value;]$Fields[$Name=$Value;]]";
        var document = new TemplateDocument(Path: "models.tst", Content: template, OutputPath: "models.ts");
        var diagnostics = new List<GenerationDiagnostic>();
        var renderer = new TemplateRenderer(typeMapper: new TypeScriptTypeMapper());

        var output = renderer.Render(template: document, metadata: metadata, diagnostics: diagnostics);

        diagnostics.Should().BeEmpty();
        output.Should().Contain("Type=myTestType;");
        output.Should().Contain("Label=instance;");
    }

    [Fact]
    public void RenderPreservesNullableDictionaryWhenValueTypeIsNullable()
    {
        var keyType = TypeReference(name: "String", fullName: "System.String", isNullable: false);
        var valueType = TypeReference(name: "String", fullName: "System.String", isNullable: true);
        var dictionaryType = TypeReference(
            name: "Dictionary",
            fullName: "System.Collections.Generic.Dictionary",
            isNullable: true,
            isPrimitive: false,
            isCollection: true,
            isDictionary: true,
            elementType: TypeReference(name: "KeyValuePair", fullName: "System.Collections.Generic.KeyValuePair", isNullable: false, isPrimitive: false),
            typeArguments: [keyType, valueType]);
        var metadata = new ProjectMetadata(
            ProjectPath: "Sample.csproj",
            SourceFiles: [],
            Types:
            [
                new TypeMetadata(
                    Name: "MultipleDictionaries",
                    FullName: "Sample.MultipleDictionaries",
                    Namespace: "Sample",
                    Kind: TypeMetadataKind.Class,
                    Accessibility: MetadataAccessibility.Public,
                    Properties:
                    [
                        new PropertyMetadata(
                            Name: "Dictionary4",
                            FullName: "Sample.MultipleDictionaries.Dictionary4",
                            Type: dictionaryType,
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
        const string template = "$Classes[$Properties[$name: $Type;]]";
        var document = new TemplateDocument(Path: "models.tst", Content: template, OutputPath: "models.ts");
        var diagnostics = new List<GenerationDiagnostic>();
        var renderer = new TemplateRenderer(typeMapper: new TypeScriptTypeMapper());

        var output = renderer.Render(template: document, metadata: metadata, diagnostics: diagnostics);

        diagnostics.Should().BeEmpty();
        output.Should().Contain("dictionary4: Record<string, string | null> | null;");
    }

    [Fact]
    public void RenderUsesEnumMemberForTypeDefault()
    {
        var statusType = TypeReference(
            name: "Status",
            fullName: "Sample.Status",
            isNullable: false,
            isPrimitive: false,
            isEnum: true,
            enumValues: [new EnumValueMetadata(Name: "Draft", Value: 0), new EnumValueMetadata(Name: "Paid", Value: 1)]);
        var metadata = new ProjectMetadata(
            ProjectPath: "Sample.csproj",
            SourceFiles: [],
            Types:
            [
                new TypeMetadata(
                    Name: "Order",
                    FullName: "Sample.Order",
                    Namespace: "Sample",
                    Kind: TypeMetadataKind.Class,
                    Accessibility: MetadataAccessibility.Public,
                    Properties:
                    [
                        new PropertyMetadata(
                            Name: "Status",
                            FullName: "Sample.Order.Status",
                            Type: statusType,
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
        const string template = "$Classes[$Properties[$name=$Type[$Default];]]";
        var document = new TemplateDocument(Path: "models.tst", Content: template, OutputPath: "models.ts");
        var diagnostics = new List<GenerationDiagnostic>();
        var renderer = new TemplateRenderer(typeMapper: new TypeScriptTypeMapper());

        var output = renderer.Render(template: document, metadata: metadata, diagnostics: diagnostics);

        diagnostics.Should().BeEmpty();
        output.Should().Contain("status=Status.Draft;");
    }

    [Fact]
    public void RenderPreservesParenthesizedCollectionElementTypeInTypeNameBlock()
    {
        var nullableStringType = TypeReference(name: "String", fullName: "System.String", isNullable: true);
        var listType = TypeReference(
            name: "List",
            fullName: "System.Collections.Generic.List",
            isNullable: false,
            isPrimitive: false,
            isCollection: true,
            elementType: nullableStringType,
            typeArguments: [nullableStringType]);
        var nullableListType = listType with
        {
            IsNullable = true,
        };
        var metadata = new ProjectMetadata(
            ProjectPath: "Sample.csproj",
            SourceFiles: [],
            Types:
            [
                new TypeMetadata(
                    Name: "MultipleLists",
                    FullName: "Sample.MultipleLists",
                    Namespace: "Sample",
                    Kind: TypeMetadataKind.Class,
                    Accessibility: MetadataAccessibility.Public,
                    Properties:
                    [
                        new PropertyMetadata(
                            Name: "List3",
                            FullName: "Sample.MultipleLists.List3",
                            Type: listType,
                            Accessibility: MetadataAccessibility.Public,
                            HasGetter: true,
                            HasSetter: true,
                            IsRequired: false,
                            Attributes: []),
                        new PropertyMetadata(
                            Name: "List4",
                            FullName: "Sample.MultipleLists.List4",
                            Type: nullableListType,
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
        const string template = "$Classes[$Properties[$name: $Type[$Name];]]";
        var document = new TemplateDocument(Path: "models.tst", Content: template, OutputPath: "models.ts");
        var diagnostics = new List<GenerationDiagnostic>();
        var renderer = new TemplateRenderer(typeMapper: new TypeScriptTypeMapper());

        var output = renderer.Render(template: document, metadata: metadata, diagnostics: diagnostics);

        diagnostics.Should().BeEmpty();
        output.Should().Contain("list3: (string | null)[];");
        output.Should().Contain("list4: (string | null)[] | null;");
    }

    [Fact]
    public void RenderInvokesTwoParameterCompiledPredicateFiltersWithParentContext()
    {
        var metadata = new ProjectMetadata(
            ProjectPath: "Sample.csproj",
            SourceFiles: [],
            Types:
            [
                new TypeMetadata(
                    Name: "Test",
                    FullName: "Sample.Test",
                    Namespace: "Sample",
                    Kind: TypeMetadataKind.Record,
                    Accessibility: MetadataAccessibility.Public,
                    Properties:
                    [
                        new PropertyMetadata(
                            Name: "PseudoEnum",
                            FullName: "Sample.Test.PseudoEnum",
                            Type: TypeReference(name: "String", fullName: "System.String", isNullable: true),
                            Accessibility: MetadataAccessibility.Public,
                            HasGetter: true,
                            HasSetter: true,
                            IsRequired: false,
                            Attributes:
                            [
                                new AttributeMetadata(
                                    Name: "AllowedValues",
                                    FullName: "System.ComponentModel.DataAnnotations.AllowedValuesAttribute",
                                    Arguments: [new AttributeArgumentMetadata(Name: null, Value: ", value1, value2")]),
                            ]),
                        new PropertyMetadata(
                            Name: "Name",
                            FullName: "Sample.Test.Name",
                            Type: TypeReference(name: "String", fullName: "System.String", isNullable: false),
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
        const string template = """
            ${
                bool IsAllowedValuesProperty(Record r, Property p)
                {
                    return r.Name == "Test"
                        && p.Attributes.Select(a => a.Name).Any(a => a == "AllowedValues");
                }
            }
            $Records[$Properties($IsAllowedValuesProperty)[$name;]]
            """;
        var diagnostics = new List<GenerationDiagnostic>();
        var renderer = new TemplateRenderer(typeMapper: new TypeScriptTypeMapper());
        var document = TemplateDocument.Parse(template: new TemplateFile(Path: "models.tst", Content: template), diagnostics: diagnostics);

        var output = renderer.Render(template: document, metadata: metadata, diagnostics: diagnostics);

        diagnostics.Should().BeEmpty();
        output.Should().Contain("pseudoEnum;");
        output.Should().NotContain("name;");
    }

    [Fact]
    public void RenderSupportsNameCaseAliases()
    {
        var metadata = new ProjectMetadata(
            ProjectPath: "Sample.csproj",
            SourceFiles: [],
            Types:
            [
                new TypeMetadata(
                    Name: "URLValue2Model",
                    FullName: "Sample.URLValue2Model",
                    Namespace: "Sample",
                    Kind: TypeMetadataKind.Class,
                    Accessibility: MetadataAccessibility.Public,
                    Properties:
                    [
                        new PropertyMetadata(
                            Name: "IPAddressValue",
                            FullName: "Sample.URLValue2Model.IPAddressValue",
                            Type: TypeReference(name: "String", fullName: "System.String", isNullable: false),
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
        const string template = "$Classes[$UpperSnakeName|$LowerKebabName|$Name[$CamelCase]|$Properties[$UpperSnakeCaseName:$Name[$LowerKebabCase];]]";
        var document = new TemplateDocument(Path: "models.tst", Content: template, OutputPath: "models.ts");
        var diagnostics = new List<GenerationDiagnostic>();
        var renderer = new TemplateRenderer(typeMapper: new TypeScriptTypeMapper());

        var output = renderer.Render(template: document, metadata: metadata, diagnostics: diagnostics);

        diagnostics.Should().BeEmpty();
        output.Should().Be("URL_VALUE_2_MODEL|url-value-2-model|urlValue2Model|IP_ADDRESS_VALUE:ip-address-value;");
    }

    [Fact]
    public void RenderCompiledHelpersCanUseNameCaseApi()
    {
        var metadata = new ProjectMetadata(
            ProjectPath: "Sample.csproj",
            SourceFiles: [],
            Types:
            [
                new TypeMetadata(
                    Name: "URLValue2Model",
                    FullName: "Sample.URLValue2Model",
                    Namespace: "Sample",
                    Kind: TypeMetadataKind.Class,
                    Accessibility: MetadataAccessibility.Public,
                    Properties:
                    [
                        new PropertyMetadata(
                            Name: "IPAddressValue",
                            FullName: "Sample.URLValue2Model.IPAddressValue",
                            Type: TypeReference(name: "String", fullName: "System.String", isNullable: false),
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
        const string template = """
            ${
                string ClientName(Class c) => c.GetName(NameCase.LowerKebabCase);
                string PropertyName(Property p) => p.GetName(NameCase.UpperSnakeCase);
                string LiteralName(Class c) => c.Name.ToNameCase(NameCase.LowerDotCase);
            }
            $Classes[$ClientName|$LiteralName|$Properties[$PropertyName;]]
            """;
        var diagnostics = new List<GenerationDiagnostic>();
        var document = TemplateDocument.Parse(template: new TemplateFile(Path: "models.tst", Content: template), diagnostics: diagnostics);
        var renderer = new TemplateRenderer(typeMapper: new TypeScriptTypeMapper());

        var output = renderer.Render(template: document, metadata: metadata, diagnostics: diagnostics);

        diagnostics.Should().BeEmpty();
        output.Trim().Should().Be("url-value-2-model|url.value.2.model|IP_ADDRESS_VALUE;");
    }

    [Fact]
    public void RenderSupportsOldStyleAttributeAndInheritanceFilters()
    {
        var metadata = new ProjectMetadata(
            ProjectPath: "Sample.csproj",
            SourceFiles: [],
            Types:
            [
                new TypeMetadata(
                    Name: "Customer",
                    FullName: "Sample.Customer",
                    Namespace: "Sample",
                    Kind: TypeMetadataKind.Class,
                    Accessibility: MetadataAccessibility.Public,
                    Properties: [],
                    Attributes: [new AttributeMetadata(Name: "Generate", FullName: "Sample.GenerateAttribute", Arguments: [])],
                    BaseTypes: [TypeReference(name: "Entity", fullName: "Sample.Entity", isNullable: false)],
                    EnumValues: [],
                    IsNullableAware: true),
                new TypeMetadata(
                    Name: "Ignored",
                    FullName: "Sample.Ignored",
                    Namespace: "Sample",
                    Kind: TypeMetadataKind.Class,
                    Accessibility: MetadataAccessibility.Public,
                    Properties: [],
                    Attributes: [],
                    BaseTypes: [],
                    EnumValues: [],
                    IsNullableAware: true),
            ],
            Diagnostics: []);
        const string template = "$Classes([Generate])[$Name,]$Classes(:Entity)[$Name,]";
        var diagnostics = new List<GenerationDiagnostic>();
        var renderer = new TemplateRenderer(typeMapper: new TypeScriptTypeMapper());

        var output = renderer.Render(template: new TemplateDocument(Path: "models.tst", Content: template, OutputPath: null), metadata: metadata, diagnostics: diagnostics);

        diagnostics.Should().BeEmpty();
        output.Should().Be("Customer,Customer,");
    }

    [Fact]
    public void RenderSupportsLegacyScalarTypeParameterCollectionsWithoutSuppressingCollectionErrors()
    {
        var metadata = new ProjectMetadata(
            ProjectPath: "Sample.csproj",
            SourceFiles: [],
            Types:
            [
                new TypeMetadata(
                    Name: "Box",
                    FullName: "Sample.Box",
                    Namespace: "Sample",
                    Kind: TypeMetadataKind.Class,
                    Accessibility: MetadataAccessibility.Public,
                    Properties: [],
                    Attributes: [],
                    BaseTypes: [],
                    EnumValues: [],
                    IsNullableAware: true)
                {
                    TypeParameters = [new TypeParameterMetadata(Name: "T"), new TypeParameterMetadata(Name: "TResult")],
                },
            ],
            Diagnostics: []);
        var diagnostics = new List<GenerationDiagnostic>();
        var renderer = new TemplateRenderer(typeMapper: new TypeScriptTypeMapper());

        var output = renderer.Render(template: new TemplateDocument(Path: "models.tst", Content: "$Classes[$Name$TypeParameters]", OutputPath: null), metadata: metadata, diagnostics: diagnostics);

        diagnostics.Should().BeEmpty();
        output.Should().Be("Box<T, TResult>");

        _ = renderer.Render(template: new TemplateDocument(Path: "invalid.tst", Content: "$Classes", OutputPath: null), metadata: metadata, diagnostics: diagnostics);

        diagnostics.Should().Contain(diagnostic => diagnostic.Code == "TW0002");
    }

    [Fact]
    public void RenderExpandsMethodsParametersAndConstants()
    {
        var metadata = new ProjectMetadata(
            ProjectPath: "Sample.csproj",
            SourceFiles: [],
            Types:
            [
                ControllerMetadata(),
            ],
            Diagnostics: []);
        const string template = "$Classes[$Name:$IsStatic;$Constants[$Name=$Value;]$Methods[$Name:$Parameters[$name:$Type:$HasDefaultValue:$DefaultValue;]]]";
        var diagnostics = new List<GenerationDiagnostic>();
        var renderer = new TemplateRenderer(typeMapper: new TypeScriptTypeMapper());

        var output = renderer.Render(template: new TemplateDocument(Path: "services.tst", Content: template, OutputPath: null), metadata: metadata, diagnostics: diagnostics);

        diagnostics.Should().BeEmpty();
        output.Should().Contain("UsersController:false;");
        output.Should().Contain("ApiVersion=v1;");
        output.Should().Contain("GetAsync:id:number:false:;filter:string | null:true:null;");
    }

    [Fact]
    public void RenderPassesParentAwareCodeModelObjectsToCompiledHelpers()
    {
        var metadata = new ProjectMetadata(
            ProjectPath: "Sample.csproj",
            SourceFiles: [],
            Types:
            [
                ControllerMetadata(),
            ],
            Diagnostics: []);
        const string template = """
            ${
                string ParentName(Method method) => method.Parent?.Name ?? string.Empty;

                string ParameterParent(Parameter parameter) => parameter.Parent?.Name ?? string.Empty;

                string ConstantParent(Constant constant) => constant.Parent?.Name ?? string.Empty;
            }
            $Classes[$Constants[$ConstantParent;]$Methods[$ParentName:$Parameters[$ParameterParent;]]]
            """;
        var diagnostics = new List<GenerationDiagnostic>();
        var renderer = new TemplateRenderer(typeMapper: new TypeScriptTypeMapper());
        var document = TemplateDocument.Parse(template: new TemplateFile(Path: "services.tst", Content: template), diagnostics: diagnostics);

        var output = renderer.Render(template: document, metadata: metadata, diagnostics: diagnostics);

        diagnostics.Should().BeEmpty();
        output.Should().Contain("UsersController;");
        output.Should().Contain("UsersController:GetAsync;GetAsync;");
    }

    [Fact]
    public void RenderSupportsWebApiRecipeHelpers()
    {
        var metadata = new ProjectMetadata(
            ProjectPath: "Sample.csproj",
            SourceFiles: [],
            Types:
            [
                WebApiControllerMetadata(),
            ],
            Diagnostics: []);
        const string template = """
            ${
                using Typewriter.Extensions.WebApi;
                using System.Text.RegularExpressions;

                string ReturnType(Method m)
                {
                    var attr = m.Attributes.FirstOrDefault(a => a.Name == "ProducesResponseType");
                    if (attr == null)
                    {
                        return m.Type.Name;
                    }

                    var regex = new Regex(".*typeof[(]([^.<]*[.])*([^)<]*)(([<])([^.<]*[.])*([^)>]*)([>]))?[)].*");
                    return "recipe-" + regex.Replace(attr.Value, "$2$4$6$7");
                }

                string MethodInfo(Method m) => $"{m.HttpMethod()}|{m.Url("api/[controller]")}|{m.Type.Name}|{m.Type.OriginalName}";

                string ParameterInfo(Parameter p) => $"{p.name}:{(p.Type == "string")}:{p.Type.Name}:{p.Type.OriginalName}";
            }
            $Classes[$Methods[$ReturnType|$MethodInfo|$Parameters[$ParameterInfo][, ]]]
            """;
        var diagnostics = new List<GenerationDiagnostic>();
        var renderer = new TemplateRenderer(typeMapper: new TypeScriptTypeMapper());
        var document = TemplateDocument.Parse(template: new TemplateFile(Path: "services.tst", Content: template), diagnostics: diagnostics);

        var output = renderer.Render(template: document, metadata: metadata, diagnostics: diagnostics);

        diagnostics.Should().BeEmpty();
        output.Should().Contain("recipe-User|get|api/Users/items/${id}?search=${encodeURIComponent(search ?? '')}|User|Task");
        output.Should().Contain("id:False:number:Int32, search:True:string:String");
    }

    [Fact]
    public void RenderUrlIgnoresNamedOnlyHttpMethodAttributeArguments()
    {
        const string ControllerFullName = "Sample.UsersController";
        const string MethodFullName = "Sample.UsersController.GetListAsync()";
        var metadata = new ProjectMetadata(
            ProjectPath: "Sample.csproj",
            SourceFiles: [],
            Types:
            [
                new TypeMetadata(
                    Name: "UsersController",
                    FullName: ControllerFullName,
                    Namespace: "Sample.Controllers",
                    Kind: TypeMetadataKind.Class,
                    Accessibility: MetadataAccessibility.Public,
                    Properties: [],
                    Attributes:
                    [
                        new AttributeMetadata(Name: "Route", FullName: "Microsoft.AspNetCore.Mvc.RouteAttribute", Arguments: [new AttributeArgumentMetadata(Name: null, Value: "api/[controller]")]),
                    ],
                    BaseTypes: [],
                    EnumValues: [],
                    IsNullableAware: true)
                {
                    Methods =
                    [
                        new MethodMetadata(
                            Name: "GetListAsync",
                            FullName: MethodFullName,
                            ReturnType: TypeReference(name: "Task", fullName: "System.Threading.Tasks.Task", isNullable: false, isPrimitive: false),
                            Accessibility: MetadataAccessibility.Public,
                            IsStatic: false,
                            IsAbstract: false,
                            IsGeneric: false,
                            Parameters: [],
                            Attributes:
                            [
                                new AttributeMetadata(
                                    Name: "HttpGet",
                                    FullName: "Microsoft.AspNetCore.Mvc.HttpGetAttribute",
                                    Arguments:
                                    [
                                        new AttributeArgumentMetadata(Name: "Name", Value: "ListFoo"),
                                        new AttributeArgumentMetadata(Name: "Order", Value: "5"),
                                    ]),
                            ],
                            ParentTypeFullName: ControllerFullName),
                    ],
                },
            ],
            Diagnostics: []);
        const string template = """
            ${
                using Typewriter.Extensions.WebApi;

                string MethodUrl(Method m) => m.Url();
            }
            $Classes[$Methods[$MethodUrl]]
            """;
        var diagnostics = new List<GenerationDiagnostic>();
        var renderer = new TemplateRenderer(typeMapper: new TypeScriptTypeMapper());
        var document = TemplateDocument.Parse(template: new TemplateFile(Path: "services.tst", Content: template), diagnostics: diagnostics);

        var output = renderer.Render(template: document, metadata: metadata, diagnostics: diagnostics);

        diagnostics.Should().BeEmpty();
        output.Should().Contain("api/Users");
        output.Should().NotContain("ListFoo");
        output.Should().NotContain("Order");
    }

    [Fact]
    public void RenderInvokesParentContextAwareScalarHelpers()
    {
        var metadata = SampleUserMetadata();
        const string template = """
            ${
                string Qualified(Class c, Property p) => c.Name + "." + p.Name;
            }
            $Classes[$Properties[$Qualified;]]
            """;
        var diagnostics = new List<GenerationDiagnostic>();
        var renderer = new TemplateRenderer(typeMapper: new TypeScriptTypeMapper());
        var document = TemplateDocument.Parse(template: new TemplateFile(Path: "models.tst", Content: template), diagnostics: diagnostics);

        var output = renderer.Render(template: document, metadata: metadata, diagnostics: diagnostics);

        diagnostics.Should().BeEmpty();
        output.Should().Contain("User.DisplayName;User.Email;");
    }

    [Fact]
    public void RenderSupportsTemplateConstructorWithFileForPreFiltering()
    {
        var metadata = new ProjectMetadata(
            ProjectPath: "Sample.csproj",
            SourceFiles: [],
            Types:
            [
                new TypeMetadata(
                    Name: "CustomerDto",
                    FullName: "Sample.CustomerDto",
                    Namespace: "Sample",
                    Kind: TypeMetadataKind.Class,
                    Accessibility: MetadataAccessibility.Public,
                    Properties: [],
                    Attributes: [],
                    BaseTypes: [],
                    EnumValues: [],
                    IsNullableAware: true),
                new TypeMetadata(
                    Name: "Ignored",
                    FullName: "Sample.Ignored",
                    Namespace: "Sample",
                    Kind: TypeMetadataKind.Class,
                    Accessibility: MetadataAccessibility.Public,
                    Properties: [],
                    Attributes: [],
                    BaseTypes: [],
                    EnumValues: [],
                    IsNullableAware: true),
            ],
            Diagnostics: []);
        const string template = """
            ${
                private Class[] dtoClasses = Array.Empty<Class>();
                private int totalClasses;

                Template(Settings settings, File file)
                {
                    totalClasses = file.Classes.Count;
                    dtoClasses = file.Classes
                        .Where(c => c.Name.EndsWith("Dto", StringComparison.Ordinal))
                        .ToArray();
                }

                string Count(File file) => totalClasses.ToString();

                IEnumerable<Class> DtoClasses(File file) => dtoClasses;
            }
            Total: $Count
            $DtoClasses[$Name;]
            """;
        var diagnostics = new List<GenerationDiagnostic>();
        var renderer = new TemplateRenderer(typeMapper: new TypeScriptTypeMapper());
        var document = TemplateDocument.Parse(template: new TemplateFile(Path: "models.tst", Content: template), diagnostics: diagnostics);

        var output = renderer.Render(template: document, metadata: metadata, diagnostics: diagnostics);

        diagnostics.Should().BeEmpty();
        output.Should().Contain("Total: 2");
        output.Should().Contain("CustomerDto;");
        output.Should().NotContain("Ignored;");
    }

    [Fact]
    public void RenderReportsTemplateFrameForThrowingHelpers()
    {
        var metadata = SampleUserMetadata();
        const string template = """
            ${
                string Boom(Class c)
                {
                    throw new InvalidOperationException("boom");
                }
            }
            $Classes[$Boom]
            """;
        var diagnostics = new List<GenerationDiagnostic>();
        var renderer = new TemplateRenderer(typeMapper: new TypeScriptTypeMapper());
        var document = TemplateDocument.Parse(template: new TemplateFile(Path: "models.tst", Content: template), diagnostics: diagnostics);

        _ = renderer.Render(template: document, metadata: metadata, diagnostics: diagnostics);

        var diagnostic = diagnostics.Should().ContainSingle().Which;
        diagnostic.Message.Should().Contain("InvalidOperationException: boom");
        diagnostic.Message.Should().Contain("Boom(Class)");
        diagnostic.Message.Should().Contain("models.tst:line 4");
        diagnostic.Line.Should().Be(4);
    }

    [Fact]
    public async Task RenderSupportsLoadDirectiveForSharedHelpers()
    {
        var metadata = SampleUserMetadata();
        var directory = Directory.CreateTempSubdirectory(prefix: "typewriter-load-test");
        try
        {
            await File.WriteAllTextAsync(
                path: Path.Combine(path1: directory.FullName, path2: "shared.cs"),
                contents: """
                          string Shared(Class c) => "shared-" + c.Name;
                          """);
            const string template = """
                ${
                    #load "shared.cs"
                }
                $Classes[$Shared]
                """;
            var diagnostics = new List<GenerationDiagnostic>();
            var renderer = new TemplateRenderer(typeMapper: new TypeScriptTypeMapper());
            var document = TemplateDocument.Parse(
                template: new TemplateFile(Path: Path.Combine(path1: directory.FullName, path2: "models.tst"), Content: template),
                diagnostics: diagnostics);

            var output = renderer.Render(template: document, metadata: metadata, diagnostics: diagnostics);

            diagnostics.Should().BeEmpty();
            output.Should().Contain("shared-User");
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task RenderSupportsLoadDirectiveForRemoteSharedHelpers()
    {
        var metadata = SampleUserMetadata();
        await using var server = new TestHttpServer(
            responses: new Dictionary<string, string>(comparer: StringComparer.Ordinal)
            {
                ["/shared.cs"] = """
                                 string Shared(Class c) => "remote-" + c.Name;
                                 """,
            });
        var helperUrl = server.UrlFor(path: "/shared.cs");
        var template = $$"""
            ${
                #load "{{helperUrl}}"
            }
            $Classes[$Shared]
            """;
        var diagnostics = new List<GenerationDiagnostic>();
        var renderer = new TemplateRenderer(typeMapper: new TypeScriptTypeMapper());
        var document = TemplateDocument.Parse(template: new TemplateFile(Path: "models.tst", Content: template), diagnostics: diagnostics);

        var output = renderer.Render(template: document, metadata: metadata, diagnostics: diagnostics);

        diagnostics.Should().BeEmpty();
        output.Should().Contain("remote-User");
    }

    [Fact]
    public async Task RenderReloadsRemoteLoadWithoutCacheDuration()
    {
        var metadata = SampleUserMetadata();
        await using var server = new TestHttpServer(
            responses: new Dictionary<string, string>(comparer: StringComparer.Ordinal)
            {
                ["/shared.cs"] = """
                                 string Shared(Class c) => "uncached-" + c.Name;
                                 """,
            });
        var helperUrl = server.UrlFor(path: "/shared.cs");
        var template = $$"""
            ${
                #load "{{helperUrl}}"
            }
            $Classes[$Shared]
            """;
        var diagnostics = new List<GenerationDiagnostic>();
        var renderer = new TemplateRenderer(typeMapper: new TypeScriptTypeMapper());
        var document = TemplateDocument.Parse(template: new TemplateFile(Path: "models.tst", Content: template), diagnostics: diagnostics);

        var firstOutput = renderer.Render(template: document, metadata: metadata, diagnostics: diagnostics);
        diagnostics.Clear();
        var secondOutput = renderer.Render(template: document, metadata: metadata, diagnostics: diagnostics);

        diagnostics.Should().BeEmpty();
        firstOutput.Should().Contain("uncached-User");
        secondOutput.Should().Contain("uncached-User");
        server.RequestCount(path: "/shared.cs").Should().Be(2);
    }

    [Fact]
    public async Task RenderCachesRemoteLoadWhenCacheDurationIsProvided()
    {
        var metadata = SampleUserMetadata();
        await using var server = new TestHttpServer(
            responses: new Dictionary<string, string>(comparer: StringComparer.Ordinal)
            {
                ["/shared.cs"] = """
                                 string Shared(Class c) => "cached-" + c.Name;
                                 """,
            });
        var helperUrl = server.UrlFor(path: "/shared.cs");
        var template = $$"""
            ${
                #load "{{helperUrl}}", "00:10:00"
            }
            $Classes[$Shared]
            """;
        var diagnostics = new List<GenerationDiagnostic>();
        var renderer = new TemplateRenderer(typeMapper: new TypeScriptTypeMapper());
        var document = TemplateDocument.Parse(template: new TemplateFile(Path: "models.tst", Content: template), diagnostics: diagnostics);

        var firstOutput = renderer.Render(template: document, metadata: metadata, diagnostics: diagnostics);
        diagnostics.Clear();
        var secondOutput = renderer.Render(template: document, metadata: metadata, diagnostics: diagnostics);

        diagnostics.Should().BeEmpty();
        firstOutput.Should().Contain("cached-User");
        secondOutput.Should().Contain("cached-User");
        server.RequestCount(path: "/shared.cs").Should().Be(1);
    }

    [Fact]
    public async Task RenderReportsInvalidRemoteLoadCacheDuration()
    {
        var metadata = SampleUserMetadata();
        await using var server = new TestHttpServer(
            responses: new Dictionary<string, string>(comparer: StringComparer.Ordinal)
            {
                ["/shared.cs"] = """
                                 string Shared(Class c) => "invalid-" + c.Name;
                                 """,
            });
        var helperUrl = server.UrlFor(path: "/shared.cs");
        var template = $$"""
            ${
                #load "{{helperUrl}}", "not-a-timespan"
            }
            $Classes[$Name]
            """;
        var diagnostics = new List<GenerationDiagnostic>();
        var renderer = new TemplateRenderer(typeMapper: new TypeScriptTypeMapper());
        var document = TemplateDocument.Parse(template: new TemplateFile(Path: "models.tst", Content: template), diagnostics: diagnostics);

        _ = renderer.Render(template: document, metadata: metadata, diagnostics: diagnostics);

        diagnostics.Should().Contain(diagnostic => diagnostic.Message.Contains(value: "#load cache duration is invalid", comparisonType: StringComparison.Ordinal));
        server.RequestCount(path: "/shared.cs").Should().Be(0);
    }

    [Fact]
    public async Task RenderSupportsNestedRelativeLoadDirectiveFromRemoteHelpers()
    {
        var metadata = SampleUserMetadata();
        await using var server = new TestHttpServer(
            responses: new Dictionary<string, string>(comparer: StringComparer.Ordinal)
            {
                ["/helpers/main.cs"] = """
                                      #load "names.cs"
                                      string Shared(Class c) => Prefix() + c.Name;
                                      """,
                ["/helpers/names.cs"] = """
                                       string Prefix() => "nested-remote-";
                                       """,
            });
        var helperUrl = server.UrlFor(path: "/helpers/main.cs");
        var template = $$"""
            ${
                #load "{{helperUrl}}"
            }
            $Classes[$Shared]
            """;
        var diagnostics = new List<GenerationDiagnostic>();
        var renderer = new TemplateRenderer(typeMapper: new TypeScriptTypeMapper());
        var document = TemplateDocument.Parse(template: new TemplateFile(Path: "models.tst", Content: template), diagnostics: diagnostics);

        var output = renderer.Render(template: document, metadata: metadata, diagnostics: diagnostics);

        diagnostics.Should().BeEmpty();
        output.Should().Contain("nested-remote-User");
    }

    [Fact]
    public async Task RenderReportsMissingRemoteLoadDirectiveFile()
    {
        var metadata = SampleUserMetadata();
        await using var server = new TestHttpServer(responses: new Dictionary<string, string>(comparer: StringComparer.Ordinal));
        var helperUrl = server.UrlFor(path: "/missing.cs");
        var template = $$"""
            ${
                #load "{{helperUrl}}"
            }
            $Classes[$Name]
            """;
        var diagnostics = new List<GenerationDiagnostic>();
        var renderer = new TemplateRenderer(typeMapper: new TypeScriptTypeMapper());
        var document = TemplateDocument.Parse(template: new TemplateFile(Path: "models.tst", Content: template), diagnostics: diagnostics);

        _ = renderer.Render(template: document, metadata: metadata, diagnostics: diagnostics);

        diagnostics.Should().Contain(
            diagnostic => diagnostic.Message.Contains(value: "#load URL returned HTTP 404", comparisonType: StringComparison.Ordinal)
                          && diagnostic.Message.Contains(value: helperUrl, comparisonType: StringComparison.Ordinal));
    }

    [Fact]
    public void RenderReportsMissingLoadDirectiveFile()
    {
        var metadata = SampleUserMetadata();
        const string template = """
            ${
                #load "missing-helpers.cs"
            }
            $Classes[$Name]
            """;
        var diagnostics = new List<GenerationDiagnostic>();
        var renderer = new TemplateRenderer(typeMapper: new TypeScriptTypeMapper());
        var document = TemplateDocument.Parse(template: new TemplateFile(Path: "models.tst", Content: template), diagnostics: diagnostics);

        _ = renderer.Render(template: document, metadata: metadata, diagnostics: diagnostics);

        diagnostics.Should().Contain(
            diagnostic => diagnostic.Message.Contains(value: "#load file was not found", comparisonType: StringComparison.Ordinal)
                          && diagnostic.Message.Contains(value: "missing-helpers.cs", comparisonType: StringComparison.Ordinal));
    }

    [Fact]
    public async Task RenderInvokesOnRenderCompleteHook()
    {
        var metadata = SampleUserMetadata();
        var directory = Directory.CreateTempSubdirectory(prefix: "typewriter-render-complete");
        var markerPath = Path.Combine(path1: directory.FullName, path2: "marker.txt");
        var template = $$"""
            ${
                void OnRenderComplete(File file)
                {
                    System.IO.File.WriteAllText(@"{{markerPath}}", "classes:" + file.Classes.Count);
                }
            }
            $Classes[$Name]
            """;
        var diagnostics = new List<GenerationDiagnostic>();
        var renderer = new TemplateRenderer(typeMapper: new TypeScriptTypeMapper());
        var document = TemplateDocument.Parse(template: new TemplateFile(Path: "models.tst", Content: template), diagnostics: diagnostics);

        try
        {
            var output = renderer.Render(template: document, metadata: metadata, diagnostics: diagnostics);

            diagnostics.Should().BeEmpty();
            output.Should().Contain("User");
            File.Exists(path: markerPath).Should().BeTrue();
            (await File.ReadAllTextAsync(path: markerPath)).Should().Be("classes:1");
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void RenderReportsOnRenderCompleteFailures()
    {
        var metadata = SampleUserMetadata();
        const string template = """
            ${
                void OnRenderComplete()
                {
                    throw new InvalidOperationException("hook failed");
                }
            }
            $Classes[$Name]
            """;
        var diagnostics = new List<GenerationDiagnostic>();
        var renderer = new TemplateRenderer(typeMapper: new TypeScriptTypeMapper());
        var document = TemplateDocument.Parse(template: new TemplateFile(Path: "models.tst", Content: template), diagnostics: diagnostics);

        _ = renderer.Render(template: document, metadata: metadata, diagnostics: diagnostics);

        var diagnostic = diagnostics.Should().ContainSingle().Which;
        diagnostic.Message.Should().Contain("OnRenderComplete");
        diagnostic.Message.Should().Contain("hook failed");
    }

    [Fact]
    public void RenderSupportsSignalRHubRecipeHelpers()
    {
        var metadata = new ProjectMetadata(
            ProjectPath: "Sample.csproj",
            SourceFiles: [],
            Types:
            [
                SignalRHubMetadata(),
            ],
            Diagnostics: []);
        const string template = """
            ${
                using Typewriter.Extensions.Types;
                using System.Collections.Generic;
                using System.Linq;

                string HubName(string className) => className.EndsWith("Hub") ? className.Substring(0, className.Length - 3) : className;

                string CleanAttributeValue(string value) => value.Trim().Trim('"').Trim('\'').Trim().TrimStart('/');

                bool IncludeClass(Class c)
                {
                    var attr = c.Attributes.FirstOrDefault(p => p.Name == "GenerateSignalRFrontendType" || p.Name == "GenerateFrontendType");
                    var parent = c.BaseClass;
                    return attr != null
                        && parent != null
                        && (parent.Name == "Hub" || parent.FullName == "Microsoft.AspNetCore.SignalR.Hub");
                }

                bool IncludeHubMethod(Method m) => !m.Attributes.Any(a => a.Name == "NonHubMethod")
                    && m.Name != "OnConnectedAsync"
                    && m.Name != "OnDisconnectedAsync";

                bool IsStreamLike(Type t)
                {
                    var effectiveType = t.IsTask && t.TypeArguments.Any() ? t.TypeArguments.FirstOrDefault() : t;
                    return effectiveType.OriginalName == "IAsyncEnumerable"
                        || effectiveType.OriginalName == "ChannelReader"
                        || effectiveType.OriginalName == "IObservable";
                }

                bool IsStreamingMethod(Method m) => IncludeHubMethod(m) && IsStreamLike(m.Type);

                bool IsInvokeMethod(Method m) => IncludeHubMethod(m) && !IsStreamLike(m.Type);

                bool IsParameterSkipped(Parameter parameter)
                {
                    return parameter.Type.OriginalName == "CancellationToken"
                        || parameter.Type.OriginalName == "ClaimsPrincipal";
                }

                List<Parameter> SkipParameters(Method m)
                {
                    return m.Parameters.Where(parameter => !IsParameterSkipped(parameter)).ToList();
                }

                string InvocationArguments(Method m)
                {
                    var names = SkipParameters(m).Select(p => p.name).ToList();
                    return names.Count == 0 ? string.Empty : ", " + string.Join(", ", names);
                }

                string SignalRMethodName(Method m)
                {
                    var attr = m.Attributes.FirstOrDefault(a => a.Name == "HubMethodName");
                    return attr == null ? m.Name : CleanAttributeValue(attr.Value);
                }

                string GetHubRouteValue(Class c)
                {
                    var route = c.Attributes.FirstOrDefault(a => a.Name == "HubRoute" || a.Name == "SignalRRoute" || a.Name == "HubPath" || a.Name == "Route");
                    return (route == null ? $"hubs/{HubName(c.Name)}" : CleanAttributeValue(route.Value)).Replace("[Hub]", HubName(c.Name));
                }
            }
            $Classes($IncludeClass)[$GetHubRouteValue|$Methods($IncludeHubMethod)[$SignalRMethodName:$IsStreamingMethod:$IsInvokeMethod:$InvocationArguments;]]
            """;
        var diagnostics = new List<GenerationDiagnostic>();
        var renderer = new TemplateRenderer(typeMapper: new TypeScriptTypeMapper());
        var document = TemplateDocument.Parse(template: new TemplateFile(Path: "signalr.tst", Content: template), diagnostics: diagnostics);

        var output = renderer.Render(template: document, metadata: metadata, diagnostics: diagnostics);

        diagnostics.Should().BeEmpty();
        output.TrimEnd().Should().Be("hubs/Chat|SendMessage:false:true:, message;StreamMessages:true:false:, room;");
    }

    [Fact]
    public void RenderSupportsRecipeStyleCompatibilityPredicates()
    {
        var metadata = new ProjectMetadata(
            ProjectPath: "Sample.csproj",
            SourceFiles: [],
            Types:
            [
                new TypeMetadata(
                    Name: "Customer",
                    FullName: "Sample.Customer",
                    Namespace: "Sample.Models",
                    Kind: TypeMetadataKind.Class,
                    Accessibility: MetadataAccessibility.Public,
                    Properties:
                    [
                        new PropertyMetadata(
                            Name: "Name",
                            FullName: "Sample.Customer.Name",
                            Type: TypeReference(name: "String", fullName: "System.String", isNullable: false),
                            Accessibility: MetadataAccessibility.Public,
                            HasGetter: true,
                            HasSetter: true,
                            IsRequired: true,
                            Attributes: []),
                        new PropertyMetadata(
                            Name: "Nickname",
                            FullName: "Sample.Customer.Nickname",
                            Type: TypeReference(name: "String", fullName: "System.String", isNullable: true),
                            Accessibility: MetadataAccessibility.Public,
                            HasGetter: true,
                            HasSetter: true,
                            IsRequired: false,
                            Attributes: []),
                        new PropertyMetadata(
                            Name: "Secret",
                            FullName: "Sample.Customer.Secret",
                            Type: TypeReference(name: "String", fullName: "System.String", isNullable: false),
                            Accessibility: MetadataAccessibility.Public,
                            HasGetter: true,
                            HasSetter: true,
                            IsRequired: false,
                            Attributes: [new AttributeMetadata(Name: "JsonIgnore", FullName: "System.Text.Json.Serialization.JsonIgnoreAttribute", Arguments: [])]),
                    ],
                    Attributes: [new AttributeMetadata(Name: "GenerateFrontendType", FullName: "Sample.GenerateFrontendTypeAttribute", Arguments: [])],
                    BaseTypes: [],
                    EnumValues: [],
                    IsNullableAware: true),
                new TypeMetadata(
                    Name: "CustomerController",
                    FullName: "Sample.CustomerController",
                    Namespace: "Sample.Controllers",
                    Kind: TypeMetadataKind.Class,
                    Accessibility: MetadataAccessibility.Public,
                    Properties: [],
                    Attributes: [new AttributeMetadata(Name: "GenerateFrontendType", FullName: "Sample.GenerateFrontendTypeAttribute", Arguments: [])],
                    BaseTypes: [TypeReference(name: "ControllerBase", fullName: "Microsoft.AspNetCore.Mvc.ControllerBase", isNullable: false)],
                    EnumValues: [],
                    IsNullableAware: true),
            ],
            Diagnostics: []);
        const string template = """
            ${
                bool IncludeClass(Class c){
                    if(!c.Namespace.StartsWith("Sample"))
                    {
                        return false;
                    }

                    var attr = c.Attributes.FirstOrDefault(p => p.Name == "GenerateFrontendType");
                    if(attr == null){
                        return false;
                    }

                    var parent = c.BaseClass;
                    if(parent != null){
                        if(parent.Name.EndsWith("Controller")
                      || parent.Name.EndsWith("ControllerBase"))
                      {
                        return false;
                      }
                    }

                    return true;
                }

                bool IncludeProperty(Property property) {
                    var attr = property.Attributes.FirstOrDefault(p => p.Name == "JsonIgnore");
                    if(attr != null){
                        return false;
                    }
                    return true;
                }

                string NullableMark(Property property) {
                    return property.Type.IsNullable ? "?" : string.Empty;
                }
            }
            $Classes($IncludeClass)[
            export interface I$Name {
            $Properties($IncludeProperty)[
              $name$NullableMark: $Type = $Type[$Default];]
            }]
            """;
        var diagnostics = new List<GenerationDiagnostic>();
        var renderer = new TemplateRenderer(typeMapper: new TypeScriptTypeMapper());
        var document = TemplateDocument.Parse(template: new TemplateFile(Path: "models.tst", Content: template), diagnostics: diagnostics);

        var output = renderer.Render(template: document, metadata: metadata, diagnostics: diagnostics);

        diagnostics.Should().BeEmpty();
        output.Should().Contain("export interface ICustomer");
        output.Should().NotContain("CustomerController");
        output.Should().Contain("name: string = \"\";");
        output.Should().Contain("nickname?: string | null = null;");
        output.Should().NotContain("secret");
    }

    [Fact]
    public void RenderInvokesCompiledTypeHelpersInsideTypeBlocks()
    {
        var metadata = new ProjectMetadata(
            ProjectPath: "Sample.csproj",
            SourceFiles: [],
            Types:
            [
                new TypeMetadata(
                    Name: "Customer",
                    FullName: "Sample.Customer",
                    Namespace: "Sample",
                    Kind: TypeMetadataKind.Class,
                    Accessibility: MetadataAccessibility.Public,
                    Properties:
                    [
                        new PropertyMetadata(
                            Name: "Id",
                            FullName: "Sample.Customer.Id",
                            Type: TypeReference(name: "Int32", fullName: "System.Int32", isNullable: false),
                            Accessibility: MetadataAccessibility.Public,
                            HasGetter: true,
                            HasSetter: true,
                            IsRequired: false,
                            Attributes: []),
                        new PropertyMetadata(
                            Name: "Name",
                            FullName: "Sample.Customer.Name",
                            Type: TypeReference(name: "String", fullName: "System.String", isNullable: false),
                            Accessibility: MetadataAccessibility.Public,
                            HasGetter: true,
                            HasSetter: true,
                            IsRequired: false,
                            Attributes: []),
                        new PropertyMetadata(
                            Name: "MiddleName",
                            FullName: "Sample.Customer.MiddleName",
                            Type: TypeReference(name: "String", fullName: "System.String", isNullable: true),
                            Accessibility: MetadataAccessibility.Public,
                            HasGetter: true,
                            HasSetter: true,
                            IsRequired: false,
                            Attributes: []),
                        new PropertyMetadata(
                            Name: "Aliases",
                            FullName: "Sample.Customer.Aliases",
                            Type: TypeReference(
                                name: "List",
                                fullName: "System.Collections.Generic.List",
                                isNullable: false,
                                isPrimitive: false,
                                isCollection: true,
                                elementType: TypeReference(name: "String", fullName: "System.String", isNullable: false),
                                typeArguments: [TypeReference(name: "String", fullName: "System.String", isNullable: false)]),
                            Accessibility: MetadataAccessibility.Public,
                            HasGetter: true,
                            HasSetter: true,
                            IsRequired: false,
                            Attributes: []),
                        new PropertyMetadata(
                            Name: "Status",
                            FullName: "Sample.Customer.Status",
                            Type: TypeReference(name: "Status", fullName: "Sample.Status", isNullable: false, isPrimitive: false, isEnum: true),
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
                new TypeMetadata(
                    Name: "Status",
                    FullName: "Sample.Status",
                    Namespace: "Sample",
                    Kind: TypeMetadataKind.Enum,
                    Accessibility: MetadataAccessibility.Public,
                    Properties: [],
                    Attributes: [],
                    BaseTypes: [],
                    EnumValues: [new EnumValueMetadata(Name: "Draft", Value: 0), new EnumValueMetadata(Name: "Paid", Value: 1)],
                    IsNullableAware: true),
            ],
            Diagnostics: []);
        const string template = """
            ${
                string SanitizeDefault(Type t){
                    var d = t.Default();
                    if(d.StartsWith("new ")){
                        return "null";
                    }

                    return d.Replace("\"", "'");
                }
            }
            $Classes[
            $Properties[
            $name=$Type[$SanitizeDefault];]]
            """;
        var diagnostics = new List<GenerationDiagnostic>();
        var renderer = new TemplateRenderer(typeMapper: new TypeScriptTypeMapper());
        var document = TemplateDocument.Parse(template: new TemplateFile(Path: "models.tst", Content: template), diagnostics: diagnostics);

        var output = renderer.Render(template: document, metadata: metadata, diagnostics: diagnostics);

        diagnostics.Should().BeEmpty();
        output.Should().Contain("id=0;");
        output.Should().Contain("name='';");
        output.Should().Contain("middleName=null;");
        output.Should().Contain("aliases=[];");
        output.Should().Contain("status=Status.Draft;");
    }

    [Fact]
    public void RenderSupportsInlineLambdaFilters()
    {
        var metadata = new ProjectMetadata(
            ProjectPath: "Sample.csproj",
            SourceFiles: [],
            Types:
            [
                new TypeMetadata(
                    Name: "CustomerDto",
                    FullName: "Sample.CustomerDto",
                    Namespace: "Sample",
                    Kind: TypeMetadataKind.Class,
                    Accessibility: MetadataAccessibility.Public,
                    Properties:
                    [
                        new PropertyMetadata(
                            Name: "Name",
                            FullName: "Sample.CustomerDto.Name",
                            Type: TypeReference(name: "String", fullName: "System.String", isNullable: false),
                            Accessibility: MetadataAccessibility.Public,
                            HasGetter: true,
                            HasSetter: true,
                            IsRequired: true,
                            Attributes: []),
                    ],
                    Attributes: [new AttributeMetadata(Name: "GenerateFrontendType", FullName: "Sample.GenerateFrontendTypeAttribute", Arguments: [])],
                    BaseTypes: [],
                    EnumValues: [],
                    IsNullableAware: true),
                new TypeMetadata(
                    Name: "IgnoredDto",
                    FullName: "Sample.IgnoredDto",
                    Namespace: "Sample",
                    Kind: TypeMetadataKind.Class,
                    Accessibility: MetadataAccessibility.Public,
                    Properties: [],
                    Attributes: [],
                    BaseTypes: [],
                    EnumValues: [],
                    IsNullableAware: true),
            ],
            Diagnostics: []);
        const string template = """
            $Classes(c => c.Name.EndsWith("Dto") && c.Attributes.Any(a => a.Name == "GenerateFrontendType"))[
            $Name:$Properties(p => !p.Type.IsNullable && p.Type.OriginalName == "String")[$Name;]]
            """;
        var diagnostics = new List<GenerationDiagnostic>();
        var renderer = new TemplateRenderer(typeMapper: new TypeScriptTypeMapper());

        var output = renderer.Render(template: new TemplateDocument(Path: "models.tst", Content: template, OutputPath: null), metadata: metadata, diagnostics: diagnostics);

        diagnostics.Should().BeEmpty();
        output.Should().Contain("CustomerDto:Name;");
        output.Should().NotContain("IgnoredDto");
    }

    [Fact]
    public void RenderCompilesTemplateCodeBlockHelpers()
    {
        var metadata = new ProjectMetadata(
            ProjectPath: "Sample.csproj",
            SourceFiles: [],
            Types:
            [
                new TypeMetadata(
                    Name: "Customer",
                    FullName: "Sample.Customer",
                    Namespace: "Sample.Models",
                    Kind: TypeMetadataKind.Class,
                    Accessibility: MetadataAccessibility.Public,
                    Properties:
                    [
                        new PropertyMetadata(
                            Name: "Name",
                            FullName: "Sample.Customer.Name",
                            Type: TypeReference(name: "String", fullName: "System.String", isNullable: false),
                            Accessibility: MetadataAccessibility.Public,
                            HasGetter: true,
                            HasSetter: true,
                            IsRequired: true,
                            Attributes: []),
                    ],
                    Attributes: [new AttributeMetadata(Name: "GenerateFrontendType", FullName: "Sample.GenerateFrontendTypeAttribute", Arguments: [])],
                    BaseTypes: [],
                    EnumValues: [],
                    IsNullableAware: true),
                new TypeMetadata(
                    Name: "CustomerController",
                    FullName: "Sample.CustomerController",
                    Namespace: "Sample.Controllers",
                    Kind: TypeMetadataKind.Class,
                    Accessibility: MetadataAccessibility.Public,
                    Properties: [],
                    Attributes: [new AttributeMetadata(Name: "GenerateFrontendType", FullName: "Sample.GenerateFrontendTypeAttribute", Arguments: [])],
                    BaseTypes: [TypeReference(name: "ControllerBase", fullName: "Microsoft.AspNetCore.Mvc.ControllerBase", isNullable: false)],
                    EnumValues: [],
                    IsNullableAware: true),
            ],
            Diagnostics: []);
        const string template = """
            ${
                using System.Text;

                Template(Settings settings)
                {
                    settings.UseStringLiteralCharacter('\'');
                }

                bool IncludeClass(Class c)
                {
                    return c.Attributes.Any(a => a.Name == "GenerateFrontendType")
                        && (c.BaseClass == null || !c.BaseClass.Name.EndsWith("ControllerBase"));
                }

                string LoudName(Property property)
                {
                    var builder = new StringBuilder(property.Name);
                    return builder.ToString().ToUpperInvariant();
                }
            }
            $Classes($IncludeClass)[$Properties[$LoudName;]]
            """;
        var diagnostics = new List<GenerationDiagnostic>();
        var renderer = new TemplateRenderer(typeMapper: new TypeScriptTypeMapper());
        var document = TemplateDocument.Parse(template: new TemplateFile(Path: "models.tst", Content: template), diagnostics: diagnostics);

        var output = renderer.Render(template: document, metadata: metadata, diagnostics: diagnostics);

        diagnostics.Should().BeEmpty();
        output.TrimEnd().Should().Be("NAME;");
    }

    [Fact]
    public void RenderResolvesNuGetReferenceFromLocalCache()
    {
        var packageRoot = Path.Combine(path1: Path.GetTempPath(), path2: "Typewriter.Engine.Tests", path3: Guid.NewGuid().ToString(format: "N"));
        var packageDirectory = Path.Combine(packageRoot, "templatehelpers", "1.0.0", "lib", "net10.0");
        Directory.CreateDirectory(path: packageDirectory);
        File.Copy(
            sourceFileName: typeof(FactAttribute).Assembly.Location,
            destFileName: Path.Combine(path1: packageDirectory, path2: Path.GetFileName(path: typeof(FactAttribute).Assembly.Location)));
        var previousPackageRoot = Environment.GetEnvironmentVariable(variable: "NUGET_PACKAGES");
        Environment.SetEnvironmentVariable(variable: "NUGET_PACKAGES", value: packageRoot);

        try
        {
            var metadata = new ProjectMetadata(
                ProjectPath: "Sample.csproj",
                SourceFiles: [],
                Types:
                [
                    new TypeMetadata(
                        Name: "Customer",
                        FullName: "Sample.Customer",
                        Namespace: "Sample.Models",
                        Kind: TypeMetadataKind.Class,
                        Accessibility: MetadataAccessibility.Public,
                        Properties:
                        [
                            new PropertyMetadata(
                                Name: "Name",
                                FullName: "Sample.Customer.Name",
                                Type: TypeReference(name: "String", fullName: "System.String", isNullable: false),
                                Accessibility: MetadataAccessibility.Public,
                                HasGetter: true,
                                HasSetter: true,
                                IsRequired: true,
                                Attributes: []),
                        ],
                        Attributes: [],
                        BaseTypes: [],
                        EnumValues: [],
                        IsNullableAware: true),
                ],
                Diagnostics: []);
            const string template = """
                ${
                    #r "nuget: templatehelpers, 1.0.0"
                    using Xunit;

                    string FactName(Property property) => typeof(FactAttribute).Name + property.Name;
                }
                $Classes[$Properties[$FactName;]]
                """;
            var diagnostics = new List<GenerationDiagnostic>();
            var renderer = new TemplateRenderer(typeMapper: new TypeScriptTypeMapper());
            var document = TemplateDocument.Parse(template: new TemplateFile(Path: "models.tst", Content: template), diagnostics: diagnostics);

            var output = renderer.Render(template: document, metadata: metadata, diagnostics: diagnostics);

            diagnostics.Should().BeEmpty();
            output.TrimEnd().Should().Be("FactAttributeName;");
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable: "NUGET_PACKAGES", value: previousPackageRoot);
            Directory.Delete(path: packageRoot, recursive: true);
        }
    }

    [Fact]
    public void RenderRestoresNuGetReferenceFromConfiguredPackageSource()
    {
        const string PackageId = "xunit.v3.extensibility.core";
        const string PackageVersion = "3.2.2";
        var sourcePackageRoot = GetCurrentNuGetPackageRoot();
        Directory.Exists(path: Path.Combine(path1: sourcePackageRoot, path2: PackageId, path3: PackageVersion))
            .Should()
            .BeTrue(because: $"package source '{sourcePackageRoot}' should contain {PackageId} {PackageVersion}.");

        var testRoot = Path.Combine(path1: Path.GetTempPath(), path2: "Typewriter.Engine.Tests", path3: Guid.NewGuid().ToString(format: "N"));
        var templateDirectory = Path.Combine(path1: testRoot, path2: "template");
        var restoredPackageRoot = Path.Combine(path1: testRoot, path2: "restored");
        Directory.CreateDirectory(path: templateDirectory);
#pragma warning disable SEC0116
        File.WriteAllText(
            path: Path.Combine(path1: templateDirectory, path2: "NuGet.config"),
            contents: $"""
                       <?xml version="1.0" encoding="utf-8"?>
                       <configuration>
                         <packageSources>
                           <clear />
                           <add key="local" value="{sourcePackageRoot}" />
                         </packageSources>
                       </configuration>
                       """);
#pragma warning restore SEC0116

        var previousPackageRoot = Environment.GetEnvironmentVariable(variable: "NUGET_PACKAGES");
        Environment.SetEnvironmentVariable(variable: "NUGET_PACKAGES", value: restoredPackageRoot);

        try
        {
            var metadata = new ProjectMetadata(
                ProjectPath: "Sample.csproj",
                SourceFiles: [],
                Types:
                [
                    new TypeMetadata(
                        Name: "Customer",
                        FullName: "Sample.Customer",
                        Namespace: "Sample.Models",
                        Kind: TypeMetadataKind.Class,
                        Accessibility: MetadataAccessibility.Public,
                        Properties:
                        [
                            new PropertyMetadata(
                                Name: "Name",
                                FullName: "Sample.Customer.Name",
                                Type: TypeReference(name: "String", fullName: "System.String", isNullable: false),
                                Accessibility: MetadataAccessibility.Public,
                                HasGetter: true,
                                HasSetter: true,
                                IsRequired: true,
                                Attributes: []),
                        ],
                        Attributes: [],
                        BaseTypes: [],
                        EnumValues: [],
                        IsNullableAware: true),
                ],
                Diagnostics: []);
            const string template = """
                ${
                    #r "nuget: xunit.v3.extensibility.core, 3.2.2"
                    using Xunit;

                    string FactName(Property property) => typeof(FactAttribute).Name + property.Name;
                }
                $Classes[$Properties[$FactName;]]
                """;
            var diagnostics = new List<GenerationDiagnostic>();
            var renderer = new TemplateRenderer(typeMapper: new TypeScriptTypeMapper());
            var document = TemplateDocument.Parse(
                template: new TemplateFile(Path: Path.Combine(path1: templateDirectory, path2: "models.tst"), Content: template),
                diagnostics: diagnostics);

            var output = renderer.Render(template: document, metadata: metadata, diagnostics: diagnostics);

            diagnostics.Should().BeEmpty();
            Directory.Exists(path: Path.Combine(path1: restoredPackageRoot, path2: PackageId, path3: PackageVersion)).Should().BeTrue();
            output.TrimEnd().Should().Be("FactAttributeName;");
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable: "NUGET_PACKAGES", value: previousPackageRoot);
            ForceGarbageCollection();
            DeleteDirectoryWithRetry(directory: testRoot);
        }
    }

    [Fact]
    public void CompileUsesCollectibleAssemblyLoadContext()
    {
        var contextReference = CompileAndDisposeTemplate();

        ForceCollectibleContextCollection(contextReference: contextReference);

        contextReference.IsAlive.Should().BeFalse();
    }

    [Fact]
    public void RenderInvokesDocCommentToJsDocExtension()
    {
        var metadata = new ProjectMetadata(
            ProjectPath: "Sample.csproj",
            SourceFiles: [],
            Types:
            [
                new TypeMetadata(
                    Name: "Widget",
                    FullName: "Sample.Widget",
                    Namespace: "Sample",
                    Kind: TypeMetadataKind.Class,
                    Accessibility: MetadataAccessibility.Public,
                    Properties: [],
                    Attributes: [],
                    BaseTypes: [],
                    EnumValues: [],
                    IsNullableAware: true)
                {
                    DocComment = new DocCommentMetadata(
                        Summary: "Defines a <see cref=\"T:Sample.Widget\" />.",
                        Returns: string.Empty,
                        Parameters: []),
                },
            ],
            Diagnostics: []);
        const string template = """
            ${
                string JsDoc(Class c) => c.DocComment.ToJsDocSummary();
            }
            $Classes[$JsDoc]
            """;
        var diagnostics = new List<GenerationDiagnostic>();
        var renderer = new TemplateRenderer(typeMapper: new TypeScriptTypeMapper());
        var document = TemplateDocument.Parse(template: new TemplateFile(Path: "jsdoc.tst", Content: template), diagnostics: diagnostics);

        var output = renderer.Render(template: document, metadata: metadata, diagnostics: diagnostics);

        diagnostics.Should().BeEmpty();
        output.Should().Contain("Defines a {@link Widget}.");
    }

    [Fact]
    public void RenderExposesCodeModelParityMembers()
    {
        const string ModelFullName = "Sample.Widget";
        const string DelegateFullName = "Sample.Widget.Factory";
        var stringType = TypeReference(name: "String", fullName: "System.String", isNullable: false);
        var metadata = new ProjectMetadata(
            ProjectPath: "Sample.csproj",
            SourceFiles: [],
            Types:
            [
                new TypeMetadata(
                    Name: "Widget",
                    FullName: ModelFullName,
                    Namespace: "Sample",
                    Kind: TypeMetadataKind.Class,
                    Accessibility: MetadataAccessibility.Public,
                    Properties: [],
                    Attributes: [],
                    BaseTypes: [],
                    EnumValues: [],
                    IsNullableAware: true)
                {
                    DocComment = new DocCommentMetadata(Summary: "Widget summary.", Returns: string.Empty, Parameters: []),
                    TypeParameters = [new TypeParameterMetadata(Name: "T")],
                    Fields =
                    [
                        new FieldMetadata(
                            Name: "InstanceField",
                            FullName: ModelFullName + ".InstanceField",
                            Accessibility: MetadataAccessibility.Public,
                            Type: stringType,
                            Attributes: [],
                            ParentTypeFullName: ModelFullName)
                        {
                            DocComment = new DocCommentMetadata(Summary: "Field summary.", Returns: string.Empty, Parameters: []),
                        },
                    ],
                    StaticReadOnlyFields =
                    [
                        new StaticReadOnlyFieldMetadata(
                            Name: "StaticLabel",
                            FullName: ModelFullName + ".StaticLabel",
                            Accessibility: MetadataAccessibility.Public,
                            Type: stringType,
                            Value: "ready",
                            Attributes: [],
                            ParentTypeFullName: ModelFullName),
                    ],
                    Events =
                    [
                        new EventMetadata(
                            Name: "Changed",
                            FullName: ModelFullName + ".Changed",
                            Accessibility: MetadataAccessibility.Public,
                            Type: TypeReference(name: "EventHandler", fullName: "System.EventHandler", isNullable: true, isPrimitive: false),
                            Attributes: [],
                            ParentTypeFullName: ModelFullName),
                    ],
                    Delegates =
                    [
                        new DelegateMetadata(
                            Name: "Factory",
                            FullName: DelegateFullName,
                            Accessibility: MetadataAccessibility.Public,
                            ReturnType: stringType,
                            IsGeneric: true,
                            Parameters:
                            [
                                new ParameterMetadata(
                                    Name: "value",
                                    FullName: DelegateFullName + ".value",
                                    Type: stringType,
                                    HasDefaultValue: false,
                                    DefaultValue: null,
                                    Attributes: [],
                                    ParentMethodFullName: DelegateFullName),
                            ],
                            Attributes: [],
                            ParentTypeFullName: ModelFullName)
                        {
                            TypeParameters = [new TypeParameterMetadata(Name: "TResult")],
                        },
                    ],
                    NestedClasses =
                    [
                        new TypeMetadata(
                            Name: "Nested",
                            FullName: ModelFullName + ".Nested",
                            Namespace: "Sample",
                            Kind: TypeMetadataKind.Class,
                            Accessibility: MetadataAccessibility.Public,
                            Properties: [],
                            Attributes: [],
                            BaseTypes: [],
                            EnumValues: [],
                            IsNullableAware: true)
                        {
                            ContainingTypeFullName = ModelFullName,
                        },
                    ],
                },
            ],
            Diagnostics: []);
        const string template = """
            ${
                string Helper(Class c) => $"{c.DocComment.Summary}|{c.TypeParameters}|{c.Fields.First().DocComment.Summary}|{c.StaticReadOnlyFields.First().Value}|{c.Events.First().Name}|{c.Delegates.First().TypeParameters}";
            }
            $Classes[$Helper|$DocComment[$Summary]|$Fields[$Name:$DocComment[$Summary];]$StaticReadOnlyFields[$Name=$Value;]$Events[$Name;]$Delegates[$Name:$Parameters[$name:$Type;];]$NestedClasses[$Name;]]
            """;
        var diagnostics = new List<GenerationDiagnostic>();
        var renderer = new TemplateRenderer(typeMapper: new TypeScriptTypeMapper());
        var document = TemplateDocument.Parse(template: new TemplateFile(Path: "parity.tst", Content: template), diagnostics: diagnostics);

        var output = renderer.Render(template: document, metadata: metadata, diagnostics: diagnostics);

        diagnostics.Should().BeEmpty();
        output.Should().Contain("Widget summary.|<T>|Field summary.|ready|Changed|<TResult>");
        output.Should().Contain("InstanceField:Field summary.;");
        output.Should().Contain("StaticLabel=ready;");
        output.Should().Contain("Changed;");
        output.Should().Contain("Factory:value:string;");
        output.Should().Contain("Nested;");
    }

    [Fact]
    public void RenderPreservesDollarSignBeforeBraceInTypeScriptTemplateLiteral()
    {
        var metadata = new ProjectMetadata(
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
                    Properties: [],
                    Attributes: [],
                    BaseTypes: [],
                    EnumValues: [],
                    IsNullableAware: true),
            ],
            Diagnostics: []);
        const string template = """$Classes[export const url = `${environment.apiBaseUrl}$Name`;]""";
        var diagnostics = new List<GenerationDiagnostic>();
        var renderer = new TemplateRenderer(typeMapper: new TypeScriptTypeMapper());
        var document = TemplateDocument.Parse(template: new TemplateFile(Path: "test.tst", Content: template), diagnostics: diagnostics);

        var output = renderer.Render(template: document, metadata: metadata, diagnostics: diagnostics);

        diagnostics.Should().BeEmpty();
        output.Should().Contain("${environment.apiBaseUrl}User");
    }

    [Fact]
    public void RenderExposesNestedRecordsInClassAndRecord()
    {
        var metadata = new ProjectMetadata(
            ProjectPath: "Sample.csproj",
            SourceFiles: [],
            Types:
            [
                new TypeMetadata(
                    Name: "Outer",
                    FullName: "Sample.Outer",
                    Namespace: "Sample",
                    Kind: TypeMetadataKind.Class,
                    Accessibility: MetadataAccessibility.Public,
                    Properties: [],
                    Attributes: [],
                    BaseTypes: [],
                    EnumValues: [],
                    IsNullableAware: true)
                {
                    NestedRecords =
                    [
                        new TypeMetadata(
                            Name: "InnerRecord",
                            FullName: "Sample.Outer.InnerRecord",
                            Namespace: "Sample",
                            Kind: TypeMetadataKind.Record,
                            Accessibility: MetadataAccessibility.Public,
                            Properties: [],
                            Attributes: [],
                            BaseTypes: [],
                            EnumValues: [],
                            IsNullableAware: true)
                        {
                            ContainingTypeFullName = "Sample.Outer",
                        },
                    ],
                },
                new TypeMetadata(
                    Name: "OuterRecord",
                    FullName: "Sample.OuterRecord",
                    Namespace: "Sample",
                    Kind: TypeMetadataKind.Record,
                    Accessibility: MetadataAccessibility.Public,
                    Properties: [],
                    Attributes: [],
                    BaseTypes: [],
                    EnumValues: [],
                    IsNullableAware: true)
                {
                    NestedRecords =
                    [
                        new TypeMetadata(
                            Name: "DeepRecord",
                            FullName: "Sample.OuterRecord.DeepRecord",
                            Namespace: "Sample",
                            Kind: TypeMetadataKind.Record,
                            Accessibility: MetadataAccessibility.Public,
                            Properties: [],
                            Attributes: [],
                            BaseTypes: [],
                            EnumValues: [],
                            IsNullableAware: true)
                        {
                            ContainingTypeFullName = "Sample.OuterRecord",
                        },
                    ],
                },
            ],
            Diagnostics: []);
        const string template = """$Classes[$NestedRecords[$Name;]]$Records[$NestedRecords[$Name;]]""";
        var diagnostics = new List<GenerationDiagnostic>();
        var renderer = new TemplateRenderer(typeMapper: new TypeScriptTypeMapper());
        var document = TemplateDocument.Parse(template: new TemplateFile(Path: "test.tst", Content: template), diagnostics: diagnostics);

        var output = renderer.Render(template: document, metadata: metadata, diagnostics: diagnostics);

        diagnostics.Should().BeEmpty();
        output.Should().Contain("InnerRecord;");
        output.Should().Contain("DeepRecord;");
    }

    [Fact]
    public void RenderIncludesStaticEvents()
    {
        var metadata = new ProjectMetadata(
            ProjectPath: "Sample.csproj",
            SourceFiles: [],
            Types:
            [
                new TypeMetadata(
                    Name: "Widget",
                    FullName: "Sample.Widget",
                    Namespace: "Sample",
                    Kind: TypeMetadataKind.Class,
                    Accessibility: MetadataAccessibility.Public,
                    Properties: [],
                    Attributes: [],
                    BaseTypes: [],
                    EnumValues: [],
                    IsNullableAware: true)
                {
                    Events =
                    [
                        new EventMetadata(
                            Name: "InstanceChanged",
                            FullName: "Sample.Widget.InstanceChanged",
                            Accessibility: MetadataAccessibility.Public,
                            Type: TypeReference(name: "EventHandler", fullName: "System.EventHandler", isNullable: true, isPrimitive: false),
                            Attributes: [],
                            ParentTypeFullName: "Sample.Widget"),
                        new EventMetadata(
                            Name: "StaticChanged",
                            FullName: "Sample.Widget.StaticChanged",
                            Accessibility: MetadataAccessibility.Public,
                            Type: TypeReference(name: "EventHandler", fullName: "System.EventHandler", isNullable: true, isPrimitive: false),
                            Attributes: [],
                            ParentTypeFullName: "Sample.Widget"),
                    ],
                },
            ],
            Diagnostics: []);
        const string template = """$Classes[$Events[$Name;]]""";
        var diagnostics = new List<GenerationDiagnostic>();
        var renderer = new TemplateRenderer(typeMapper: new TypeScriptTypeMapper());
        var document = TemplateDocument.Parse(template: new TemplateFile(Path: "test.tst", Content: template), diagnostics: diagnostics);

        var output = renderer.Render(template: document, metadata: metadata, diagnostics: diagnostics);

        diagnostics.Should().BeEmpty();
        output.Should().Contain("InstanceChanged;");
        output.Should().Contain("StaticChanged;");
    }

    [Fact]
    public void TypeScriptMapperPrefersDictionaryShapeOverEnumerableShape()
    {
        var mapper = new TypeScriptTypeMapper();
        var stringType = TypeReference(name: "String", fullName: "System.String", isNullable: false);
        var dictionaryType = TypeReference(
            name: "Dictionary",
            fullName: "System.Collections.Generic.Dictionary",
            isNullable: false,
            isPrimitive: false,
            isCollection: true,
            isDictionary: true,
            elementType: TypeReference(name: "KeyValuePair", fullName: "System.Collections.Generic.KeyValuePair", isNullable: false, isPrimitive: false),
            typeArguments: [stringType, stringType]);

        var result = mapper.Map(type: dictionaryType);

        result.Should().Be("Record<string, string>");
    }

    [Fact]
    public void TypeScriptMapperPreservesNullableDictionaryWhenValueTypeIsNullable()
    {
        var mapper = new TypeScriptTypeMapper();
        var keyType = TypeReference(name: "String", fullName: "System.String", isNullable: false);
        var valueType = TypeReference(name: "String", fullName: "System.String", isNullable: true);
        var dictionaryType = TypeReference(
            name: "Dictionary",
            fullName: "System.Collections.Generic.Dictionary",
            isNullable: true,
            isPrimitive: false,
            isCollection: true,
            isDictionary: true,
            elementType: TypeReference(name: "KeyValuePair", fullName: "System.Collections.Generic.KeyValuePair", isNullable: false, isPrimitive: false),
            typeArguments: [keyType, valueType]);

        var result = mapper.Map(type: dictionaryType);

        result.Should().Be("Record<string, string | null> | null");
    }

    [Fact]
    public void TypeScriptMapperPreservesClosedGenericTypeArguments()
    {
        var mapper = new TypeScriptTypeMapper();
        var intType = TypeReference(name: "Int32", fullName: "System.Int32", isNullable: false);
        var stringType = TypeReference(name: "String", fullName: "System.String", isNullable: false);
        var boxOfIntType = TypeReference(
            name: "Box",
            fullName: "App.Box",
            isNullable: false,
            isPrimitive: false,
            typeArguments: [intType]);
        var listOfBoxType = TypeReference(
            name: "List",
            fullName: "System.Collections.Generic.List",
            isNullable: false,
            isPrimitive: false,
            isCollection: true,
            elementType: boxOfIntType,
            typeArguments: [boxOfIntType]);
        var dictionaryOfBoxType = TypeReference(
            name: "Dictionary",
            fullName: "System.Collections.Generic.Dictionary",
            isNullable: false,
            isPrimitive: false,
            isCollection: true,
            isDictionary: true,
            elementType: TypeReference(name: "KeyValuePair", fullName: "System.Collections.Generic.KeyValuePair", isNullable: false, isPrimitive: false),
            typeArguments: [stringType, boxOfIntType]);

        mapper.Map(type: boxOfIntType).Should().Be("Box<number>");
        mapper.Map(type: listOfBoxType).Should().Be("Box<number>[]");
        mapper.Map(type: dictionaryOfBoxType).Should().Be("Record<string, Box<number>>");
    }

    [Fact]
    public void TypeScriptMapperParenthesizesNullableCollectionElementType()
    {
        var mapper = new TypeScriptTypeMapper();
        var nullableStringType = TypeReference(name: "String", fullName: "System.String", isNullable: true);
        var listType = TypeReference(
            name: "List",
            fullName: "System.Collections.Generic.List",
            isNullable: false,
            isPrimitive: false,
            isCollection: true,
            elementType: nullableStringType,
            typeArguments: [nullableStringType]);
        var nullableListType = listType with
        {
            IsNullable = true,
        };

        var listResult = mapper.Map(type: listType);
        var nullableListResult = mapper.Map(type: nullableListType);

        listResult.Should().Be("(string | null)[]");
        nullableListResult.Should().Be("(string | null)[] | null");
    }

    private static TypeMetadataReference TypeReference(
        string name,
        string fullName,
        bool isNullable,
        bool? isPrimitive = null,
        bool isCollection = false,
        bool isDictionary = false,
        bool isEnum = false,
        bool isDateLike = false,
        TypeMetadataReference? elementType = null,
        IReadOnlyList<TypeMetadataReference>? typeArguments = null,
        IReadOnlyList<EnumValueMetadata>? enumValues = null)
    {
        return new TypeMetadataReference(
            Name: name,
            FullName: fullName,
            Namespace: GetNamespace(fullName: fullName),
            IsNullable: isNullable,
            IsCollection: isCollection,
            IsDictionary: isDictionary,
            IsEnum: isEnum,
            IsPrimitive: isPrimitive ?? IsPrimitive(fullName: fullName),
            IsDateLike: isDateLike,
            ElementType: elementType,
            TypeArguments: typeArguments ?? [])
        {
            EnumValues = enumValues ?? [],
        };
    }

    private static string GetNamespace(string fullName)
    {
        var index = fullName.LastIndexOf(value: '.');
        return index < 0 ? string.Empty : fullName[..index];
    }

    private static bool IsPrimitive(string fullName)
    {
        return fullName is "System.Boolean"
            or "System.Byte"
            or "System.SByte"
            or "System.Int16"
            or "System.UInt16"
            or "System.Int32"
            or "System.UInt32"
            or "System.Int64"
            or "System.UInt64"
            or "System.Single"
            or "System.Double"
            or "System.Decimal"
            or "System.String"
            or "System.Char";
    }

    private static string GetCurrentNuGetPackageRoot()
    {
        var configuredRoot = Environment.GetEnvironmentVariable(variable: "NUGET_PACKAGES");
        if (!string.IsNullOrWhiteSpace(value: configuredRoot))
        {
            return configuredRoot;
        }

        return Path.Combine(
            path1: Environment.GetFolderPath(folder: Environment.SpecialFolder.UserProfile),
            path2: ".nuget",
            path3: "packages");
    }

    private static WeakReference CompileAndDisposeTemplate()
    {
        var metadata = new ProjectMetadata(
            ProjectPath: "Sample.csproj",
            SourceFiles: [],
            Types:
            [
                new TypeMetadata(
                    Name: "Customer",
                    FullName: "Sample.Customer",
                    Namespace: "Sample.Models",
                    Kind: TypeMetadataKind.Class,
                    Accessibility: MetadataAccessibility.Public,
                    Properties: [],
                    Attributes: [],
                    BaseTypes: [],
                    EnumValues: [],
                    IsNullableAware: true),
            ],
            Diagnostics: []);
        const string template = """
            ${
                string LoudName(Class item) => item.Name.ToUpperInvariant();
            }
            """;
        var diagnostics = new List<GenerationDiagnostic>();
        var document = TemplateDocument.Parse(template: new TemplateFile(Path: "models.tst", Content: template), diagnostics: diagnostics);
        using var helper = TemplateRuntimeCompiler.Compile(template: document, metadata: metadata, diagnostics: diagnostics);
        diagnostics.Should().BeEmpty();
        helper.Should().NotBeNull();

        return helper!.AssemblyLoadContextReference;
    }

    private static void ForceCollectibleContextCollection(WeakReference contextReference)
    {
        for (var attempt = 0; attempt < 10 && contextReference.IsAlive; attempt++)
        {
            ForceGarbageCollection();
        }
    }

#pragma warning disable S1215
    private static void ForceGarbageCollection()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }
#pragma warning restore S1215

    private static void DeleteDirectoryWithRetry(string directory)
    {
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            try
            {
                if (Directory.Exists(path: directory))
                {
                    Directory.Delete(path: directory, recursive: true);
                }

                return;
            }
            catch (IOException) when (attempt < 5)
            {
                ForceGarbageCollection();
            }
            catch (UnauthorizedAccessException) when (attempt < 5)
            {
                ForceGarbageCollection();
            }
        }
    }

    private static TypeMetadata ControllerMetadata()
    {
        const string ControllerFullName = "Sample.UsersController";
        const string MethodFullName = "Sample.UsersController.GetAsync(int, string?)";
        return new TypeMetadata(
            Name: "UsersController",
            FullName: ControllerFullName,
            Namespace: "Sample",
            Kind: TypeMetadataKind.Class,
            Accessibility: MetadataAccessibility.Public,
            Properties: [],
            Attributes: [new AttributeMetadata(Name: "GenerateFrontendType", FullName: "Sample.GenerateFrontendTypeAttribute", Arguments: [])],
            BaseTypes: [],
            EnumValues: [],
            IsNullableAware: true)
        {
            Constants =
            [
                new ConstantMetadata(
                    Name: "ApiVersion",
                    FullName: "Sample.UsersController.ApiVersion",
                    Accessibility: MetadataAccessibility.Public,
                    Type: TypeReference(name: "String", fullName: "System.String", isNullable: false),
                    Value: "v1",
                    Attributes: [],
                    ParentTypeFullName: ControllerFullName),
            ],
            Methods =
            [
                new MethodMetadata(
                    Name: "GetAsync",
                    FullName: MethodFullName,
                    ReturnType: TypeReference(name: "User", fullName: "Sample.User", isNullable: true),
                    Accessibility: MetadataAccessibility.Public,
                    IsStatic: false,
                    IsAbstract: false,
                    IsGeneric: false,
                    Parameters:
                    [
                        new ParameterMetadata(
                            Name: "id",
                            FullName: MethodFullName + ".id",
                            Type: TypeReference(name: "Int32", fullName: "System.Int32", isNullable: false),
                            HasDefaultValue: false,
                            DefaultValue: null,
                            Attributes: [],
                            ParentMethodFullName: MethodFullName),
                        new ParameterMetadata(
                            Name: "filter",
                            FullName: MethodFullName + ".filter",
                            Type: TypeReference(name: "String", fullName: "System.String", isNullable: true),
                            HasDefaultValue: true,
                            DefaultValue: "null",
                            Attributes: [],
                            ParentMethodFullName: MethodFullName),
                    ],
                    Attributes: [new AttributeMetadata(Name: "HttpGet", FullName: "Sample.HttpGetAttribute", Arguments: [])],
                    ParentTypeFullName: ControllerFullName),
            ],
        };
    }

    private static ProjectMetadata SampleUserMetadata()
    {
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
                            Name: "DisplayName",
                            FullName: "Sample.User.DisplayName",
                            Type: TypeReference(name: "String", fullName: "System.String", isNullable: false),
                            Accessibility: MetadataAccessibility.Public,
                            HasGetter: true,
                            HasSetter: true,
                            IsRequired: true,
                            Attributes: []),
                        new PropertyMetadata(
                            Name: "Email",
                            FullName: "Sample.User.Email",
                            Type: TypeReference(name: "String", fullName: "System.String", isNullable: true),
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

    private static TypeMetadata WebApiControllerMetadata()
    {
        const string ControllerFullName = "Sample.UsersController";
        const string MethodFullName = "Sample.UsersController.GetItemAsync(int, string?)";
        var userType = TypeReference(name: "User", fullName: "Sample.User", isNullable: false);
        var taskOfUserType = TypeReference(
            name: "Task",
            fullName: "System.Threading.Tasks.Task",
            isNullable: false,
            isPrimitive: false,
            typeArguments: [userType]);
        return new TypeMetadata(
            Name: "UsersController",
            FullName: ControllerFullName,
            Namespace: "Sample.Controllers",
            Kind: TypeMetadataKind.Class,
            Accessibility: MetadataAccessibility.Public,
            Properties: [],
            Attributes:
            [
                new AttributeMetadata(Name: "GenerateFrontendType", FullName: "Sample.GenerateFrontendTypeAttribute", Arguments: []),
                new AttributeMetadata(Name: "Route", FullName: "Microsoft.AspNetCore.Mvc.RouteAttribute", Arguments: [new AttributeArgumentMetadata(Name: null, Value: "api/[controller]")]),
            ],
            BaseTypes: [TypeReference(name: "ControllerBase", fullName: "Microsoft.AspNetCore.Mvc.ControllerBase", isNullable: false, isPrimitive: false)],
            EnumValues: [],
            IsNullableAware: true)
        {
            Methods =
            [
                new MethodMetadata(
                    Name: "GetItemAsync",
                    FullName: MethodFullName,
                    ReturnType: taskOfUserType,
                    Accessibility: MetadataAccessibility.Public,
                    IsStatic: false,
                    IsAbstract: false,
                    IsGeneric: false,
                    Parameters:
                    [
                        new ParameterMetadata(
                            Name: "id",
                            FullName: MethodFullName + ".id",
                            Type: TypeReference(name: "Int32", fullName: "System.Int32", isNullable: false),
                            HasDefaultValue: false,
                            DefaultValue: null,
                            Attributes: [new AttributeMetadata(Name: "FromRoute", FullName: "Microsoft.AspNetCore.Mvc.FromRouteAttribute", Arguments: [])],
                            ParentMethodFullName: MethodFullName),
                        new ParameterMetadata(
                            Name: "search",
                            FullName: MethodFullName + ".search",
                            Type: TypeReference(name: "String", fullName: "System.String", isNullable: true),
                            HasDefaultValue: true,
                            DefaultValue: "null",
                            Attributes: [],
                            ParentMethodFullName: MethodFullName),
                    ],
                    Attributes:
                    [
                        new AttributeMetadata(Name: "HttpGet", FullName: "Microsoft.AspNetCore.Mvc.HttpGetAttribute", Arguments: [new AttributeArgumentMetadata(Name: null, Value: "items/{id}")]),
                        new AttributeMetadata(Name: "ProducesResponseType", FullName: "Microsoft.AspNetCore.Mvc.ProducesResponseTypeAttribute", Arguments: [new AttributeArgumentMetadata(Name: null, Value: "typeof(Sample.User)")]),
                    ],
                    ParentTypeFullName: ControllerFullName),
            ],
        };
    }

    private static TypeMetadata SignalRHubMetadata()
    {
        const string HubFullName = "Sample.ChatHub";
        const string SendMethodFullName = "Sample.ChatHub.SendAsync(string, CancellationToken)";
        const string StreamMethodFullName = "Sample.ChatHub.StreamMessages(string)";
        var chatMessageType = TypeReference(name: "ChatMessage", fullName: "Sample.ChatMessage", isNullable: false);
        var taskType = TypeReference(
            name: "Task",
            fullName: "System.Threading.Tasks.Task",
            isNullable: false,
            isPrimitive: false);
        var streamType = TypeReference(
            name: "IAsyncEnumerable",
            fullName: "System.Collections.Generic.IAsyncEnumerable",
            isNullable: false,
            isPrimitive: false,
            typeArguments: [chatMessageType]);
        return new TypeMetadata(
            Name: "ChatHub",
            FullName: HubFullName,
            Namespace: "Sample.Hubs",
            Kind: TypeMetadataKind.Class,
            Accessibility: MetadataAccessibility.Public,
            Properties: [],
            Attributes:
            [
                new AttributeMetadata(Name: "GenerateSignalRFrontendType", FullName: "Sample.GenerateSignalRFrontendTypeAttribute", Arguments: []),
                new AttributeMetadata(Name: "HubRoute", FullName: "Sample.HubRouteAttribute", Arguments: [new AttributeArgumentMetadata(Name: null, Value: "hubs/[Hub]")]),
            ],
            BaseTypes: [TypeReference(name: "Hub", fullName: "Microsoft.AspNetCore.SignalR.Hub", isNullable: false, isPrimitive: false)],
            EnumValues: [],
            IsNullableAware: true)
        {
            Methods =
            [
                new MethodMetadata(
                    Name: "SendAsync",
                    FullName: SendMethodFullName,
                    ReturnType: taskType,
                    Accessibility: MetadataAccessibility.Public,
                    IsStatic: false,
                    IsAbstract: false,
                    IsGeneric: false,
                    Parameters:
                    [
                        new ParameterMetadata(
                            Name: "message",
                            FullName: SendMethodFullName + ".message",
                            Type: TypeReference(name: "String", fullName: "System.String", isNullable: false),
                            HasDefaultValue: false,
                            DefaultValue: null,
                            Attributes: [],
                            ParentMethodFullName: SendMethodFullName),
                        new ParameterMetadata(
                            Name: "cancellationToken",
                            FullName: SendMethodFullName + ".cancellationToken",
                            Type: TypeReference(name: "CancellationToken", fullName: "System.Threading.CancellationToken", isNullable: false, isPrimitive: false),
                            HasDefaultValue: false,
                            DefaultValue: null,
                            Attributes: [],
                            ParentMethodFullName: SendMethodFullName),
                    ],
                    Attributes: [new AttributeMetadata(Name: "HubMethodName", FullName: "Microsoft.AspNetCore.SignalR.HubMethodNameAttribute", Arguments: [new AttributeArgumentMetadata(Name: null, Value: "SendMessage")])],
                    ParentTypeFullName: HubFullName),
                new MethodMetadata(
                    Name: "StreamMessages",
                    FullName: StreamMethodFullName,
                    ReturnType: streamType,
                    Accessibility: MetadataAccessibility.Public,
                    IsStatic: false,
                    IsAbstract: false,
                    IsGeneric: false,
                    Parameters:
                    [
                        new ParameterMetadata(
                            Name: "room",
                            FullName: StreamMethodFullName + ".room",
                            Type: TypeReference(name: "String", fullName: "System.String", isNullable: false),
                            HasDefaultValue: false,
                            DefaultValue: null,
                            Attributes: [],
                            ParentMethodFullName: StreamMethodFullName),
                    ],
                    Attributes: [],
                    ParentTypeFullName: HubFullName),
            ],
        };
    }

    private sealed class TestHttpServer : IAsyncDisposable
    {
        private readonly CancellationTokenSource _cancellation = new();
        private readonly System.Net.Sockets.TcpListener _listener;
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, int> _requestCounts = new(comparer: StringComparer.Ordinal);
        private readonly IReadOnlyDictionary<string, string> _responses;
        private readonly Task _listenTask;

        public TestHttpServer(IReadOnlyDictionary<string, string> responses)
        {
            _responses = responses;
            _listener = new System.Net.Sockets.TcpListener(localaddr: System.Net.IPAddress.Loopback, port: 0);
            _listener.Start();
            var endPoint = (System.Net.IPEndPoint)_listener.LocalEndpoint;
            BaseUrl = $"http://127.0.0.1:{endPoint.Port.ToString(provider: System.Globalization.CultureInfo.InvariantCulture)}/";
            _listenTask = Task.Run(function: ListenAsync);
        }

        private string BaseUrl { get; }

        public string UrlFor(string path) =>
            new Uri(baseUri: new Uri(uriString: BaseUrl), relativeUri: path.TrimStart(trimChar: '/')).AbsoluteUri;

        public int RequestCount(string path) =>
            _requestCounts.TryGetValue(key: path, value: out var count) ? count : 0;

        public async ValueTask DisposeAsync()
        {
            await _cancellation.CancelAsync().ConfigureAwait(continueOnCapturedContext: false);
            _listener.Stop();
            _listener.Dispose();
            try
            {
#pragma warning disable VSTHRD003 // The listener task is intentionally joined during async disposal.
                await _listenTask.ConfigureAwait(continueOnCapturedContext: false);
#pragma warning restore VSTHRD003
            }
            catch (OperationCanceledException ex)
            {
                _ = ex;
            }
            catch (ObjectDisposedException ex)
            {
                _ = ex;
            }

            _cancellation.Dispose();
        }

        private static string ReadRequestPath(string? requestLine)
        {
            if (string.IsNullOrWhiteSpace(value: requestLine))
            {
                return "/";
            }

            var parts = requestLine.Split(separator: ' ', options: StringSplitOptions.RemoveEmptyEntries);
            return parts.Length >= 2 ? parts[1] : "/";
        }

        private static async Task WriteResponseAsync(
            Stream stream,
            int statusCode,
            string reason,
            string content)
        {
            var body = System.Text.Encoding.UTF8.GetBytes(s: content);
            var header = string.Concat(
                "HTTP/1.1 ",
                statusCode.ToString(provider: System.Globalization.CultureInfo.InvariantCulture),
                " ",
                reason,
                "\r\nContent-Type: text/plain; charset=utf-8\r\nContent-Length: ",
                body.Length.ToString(provider: System.Globalization.CultureInfo.InvariantCulture),
                "\r\nConnection: close\r\n\r\n");
            await stream.WriteAsync(buffer: System.Text.Encoding.ASCII.GetBytes(s: header)).ConfigureAwait(continueOnCapturedContext: false);
            await stream.WriteAsync(buffer: body).ConfigureAwait(continueOnCapturedContext: false);
        }

        private async Task ListenAsync()
        {
            while (!_cancellation.IsCancellationRequested)
            {
                try
                {
                    using var client = await _listener.AcceptTcpClientAsync(cancellationToken: _cancellation.Token).ConfigureAwait(continueOnCapturedContext: false);
                    await HandleClientAsync(client: client).ConfigureAwait(continueOnCapturedContext: false);
                }
                catch (OperationCanceledException) when (_cancellation.IsCancellationRequested)
                {
                    return;
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
            }
        }

        private async Task HandleClientAsync(System.Net.Sockets.TcpClient client)
        {
            await using var stream = client.GetStream();
            using var reader = new StreamReader(stream: stream, encoding: System.Text.Encoding.ASCII, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
            var requestLine = await reader.ReadLineAsync().ConfigureAwait(continueOnCapturedContext: false);
            var path = ReadRequestPath(requestLine: requestLine);
            _requestCounts.AddOrUpdate(key: path, addValue: 1, updateValueFactory: static (_, count) => count + 1);
            while (true)
            {
                var headerLine = await reader.ReadLineAsync().ConfigureAwait(continueOnCapturedContext: false);
                if (string.IsNullOrEmpty(value: headerLine))
                {
                    break;
                }

                _ = headerLine;
            }

            if (_responses.TryGetValue(key: path, value: out var content))
            {
                await WriteResponseAsync(stream: stream, statusCode: 200, reason: "OK", content: content).ConfigureAwait(continueOnCapturedContext: false);
                return;
            }

            await WriteResponseAsync(stream: stream, statusCode: 404, reason: "Not Found", content: "not found").ConfigureAwait(continueOnCapturedContext: false);
        }
    }
}
