using AwesomeAssertions;
using Typewriter.Abstractions;
using Xunit;
using CodeClass = Typewriter.CodeModel.Class;
using CodeDelegate = Typewriter.CodeModel.Delegate;
using CodeEnum = Typewriter.CodeModel.Enum;
using CodeFile = Typewriter.CodeModel.File;
using CodeInterface = Typewriter.CodeModel.Interface;
using CodeItem = Typewriter.CodeModel.Item;
using CodeRecord = Typewriter.CodeModel.Record;
using CodeStruct = Typewriter.CodeModel.Struct;
using CodeType = Typewriter.CodeModel.Type;

namespace Typewriter.Engine.Tests;

public sealed class CodeModelParentTests
{
    [Theory]
    [InlineData(TypeMetadataKind.Class, false)]
    [InlineData(TypeMetadataKind.Class, true)]
    [InlineData(TypeMetadataKind.Record, false)]
    [InlineData(TypeMetadataKind.Record, true)]
    [InlineData(TypeMetadataKind.Struct, false)]
    [InlineData(TypeMetadataKind.Struct, true)]
    [InlineData(TypeMetadataKind.Interface, false)]
    [InlineData(TypeMetadataKind.Interface, true)]
    [InlineData(TypeMetadataKind.Enum, false)]
    [InlineData(TypeMetadataKind.Enum, true)]
    public void DeclarationParentsAreDeclaringFiles(TypeMetadataKind kind, bool createFileFirst)
    {
        var declaration = CreateDeclaration(name: "Model", kind: kind);
        var sibling = CreateDeclaration(name: "Sibling");
        var unrelated = CreateDeclaration(name: "Unrelated", filePath: "Unrelated.cs");
        var metadata = CreateProject(types: [declaration, sibling, unrelated]);

        foreach (var targetType in new[] { GetModelType(kind: kind), typeof(CodeType) })
        {
            var factory = new TemplateCodeModelAdapterFactory(metadata: metadata, templatePath: "models.tst");
            if (createFileFirst)
            {
                _ = factory.CreateFile(project: metadata);
            }

            var item = Adapt(factory: factory, context: declaration, targetType: targetType);
            var file = item.Parent.Should().BeOfType<CodeFile>().Which;

            file.Path.Should().Be("Models.cs");
            file.Name.Should().Be("Models");
            file.FullName.Should().Be("Models.cs");
            file.Parent.Should().BeNull();
            file.Types.Select(selector: type => type.Name).Should().Equal("Model", "Sibling");
            file.Classes.Should().NotContain(model => model.Name == "Unrelated");
            item.Parent.Should().BeSameAs(file);
            GetTypeModel(item: item)!.Parent.Should().BeSameAs(file);
            AssertParents(items: GetDeclarations(file: file), parent: file);
        }
    }

    [Fact]
    public void TopLevelDelegateParentIsDeclaringFile()
    {
        var callback = CreateDelegate(name: "Callback");
        var sourceFile = new SourceFileMetadata(Path: "Models.cs", Types: []) { Delegates = [callback] };
        var metadata = new ProjectMetadata(ProjectPath: "Sample.csproj", SourceFiles: [sourceFile], Types: [], Diagnostics: [])
        {
            Delegates = [callback],
        };
        var factory = new TemplateCodeModelAdapterFactory(metadata: metadata, templatePath: "models.tst");

        var item = Adapt(factory: factory, context: callback, targetType: typeof(CodeDelegate));
        var file = item.Parent.Should().BeOfType<CodeFile>().Which;

        file.Should().BeSameAs(factory.CreateFile(project: metadata));
        file.Delegates.Should().ContainSingle().Which.Parent.Should().BeSameAs(file);
        file.Path.Should().Be("Models.cs");
    }

    [Theory]
    [InlineData(TypeMetadataKind.Class)]
    [InlineData(TypeMetadataKind.Record)]
    [InlineData(TypeMetadataKind.Struct)]
    [InlineData(TypeMetadataKind.Interface)]
    public void NestedDeclarationsHaveContainingTypeParents(TypeMetadataKind containingKind)
    {
        var outer = CreateContainingDeclaration(kind: containingKind);
        var metadata = CreateProject(types: [outer]);
        var factory = new TemplateCodeModelAdapterFactory(metadata: metadata, templatePath: "models.tst");
        var file = factory.CreateFile(project: metadata);
        var model = Adapt(factory: factory, context: outer, targetType: GetModelType(kind: containingKind));

        AssertParents(items: GetNestedDeclarations(item: model), parent: model);
        foreach (var nested in GetNestedMetadata(type: outer))
        {
            foreach (var targetType in new[] { GetModelType(kind: nested.Kind), typeof(CodeType) })
            {
                var item = Adapt(factory: factory, context: nested, targetType: targetType);
                item.Parent.Should().BeOfType(GetModelType(kind: containingKind));
                item.Parent.FullName.Should().Be("Sample.Outer");
                item.Parent.Parent.Should().BeSameAs(file);
            }
        }

        var nestedDelegate = Adapt(factory: factory, context: outer.Delegates.Single(), targetType: typeof(CodeDelegate));
        nestedDelegate.Parent.Should().BeOfType(GetModelType(kind: containingKind));
        nestedDelegate.Parent.Parent.Should().BeSameAs(file);
    }

    [Theory]
    [InlineData(TypeMetadataKind.Class, "Classes", "NestedClasses")]
    [InlineData(TypeMetadataKind.Record, "Records", "NestedRecords")]
    [InlineData(TypeMetadataKind.Struct, "Structs", "NestedInterfaces")]
    [InlineData(TypeMetadataKind.Interface, "Interfaces", "NestedStructs")]
    public void NestedParentsDistinguishSameNameGenericContainers(TypeMetadataKind kind, string collection, string nestedCollection)
    {
        var nongeneric = CreateContainingDeclaration(kind: kind);
        var generic = CreateContainingDeclaration(kind: kind) with { TypeParameters = [new TypeParameterMetadata(Name: "T")] };
        var metadata = CreateProject(types: [nongeneric, generic]);
        var factory = new TemplateCodeModelAdapterFactory(metadata: metadata, templatePath: "models.tst");

        foreach (var nested in GetNestedMetadata(type: generic))
        {
            var item = Adapt(factory: factory, context: nested, targetType: GetModelType(kind: nested.Kind));
            var parentType = GetTypeModel(item: item.Parent!);
            parentType.Should().NotBeNull();
            parentType.TypeParameters.Should().ContainSingle().Which.Name.Should().Be("T");
            item.Parent!.FullName.Should().Be("Sample.Outer<T>");
            if (kind == TypeMetadataKind.Class)
            {
                GetTypeModel(item: item)!.ContainingClass.Should().NotBeNull();
                GetTypeModel(item: item)!.ContainingClass!.TypeParameters.Should().ContainSingle().Which.Name.Should().Be("T");
            }
        }

        var nestedDelegate = Adapt(factory: factory, context: generic.Delegates.Single(), targetType: typeof(CodeDelegate));
        GetTypeModel(item: nestedDelegate.Parent!)!.TypeParameters.Should().ContainSingle().Which.Name.Should().Be("T");
        var template = "$" + collection + "(Outer)[$" + nestedCollection + "[$Parent[$TypeParameters[$Name;]]]]";
        Render(template: template, metadata: metadata).Should().Be("T;");
        var delegateTemplate = "$" + collection + "(Outer)[$Delegates[$Parent[$TypeParameters[$Name;]]]]";
        Render(template: delegateTemplate, metadata: metadata).Should().Be("T;");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReferencedTypesResolveFilesOutsideCurrentRenderScope(bool useFileLocations)
    {
        var local = CreateDeclaration(name: "Local");
        var referenced = CreateDeclaration(name: "Referenced", filePath: "Referenced.cs");
        if (useFileLocations)
        {
            referenced = referenced with { Location = null };
        }

        var project = CreateProject(types: [local, referenced]);
        var scoped = project with { SourceFiles = [project.SourceFiles[0]], Types = [local] };
        var factory = new TemplateCodeModelAdapterFactory(
            metadata: scoped,
            settings: new Typewriter.Configuration.Settings { TemplatePath = "models.tst" },
            metadataIndex: ProjectMetadataIndex.Create(metadata: project));

        var item = Adapt(factory: factory, context: CreateReference(type: referenced), targetType: typeof(CodeType));
        var file = item.Parent.Should().BeOfType<CodeFile>().Which;

        file.Path.Should().Be("Referenced.cs");
        file.Classes.Should().ContainSingle().Which.Name.Should().Be("Referenced");
        file.Classes.Single().Parent.Should().BeSameAs(file);
        factory.CreateFile(project: scoped).Classes.Should().ContainSingle().Which.Name.Should().Be("Local");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SyntheticMetadataUsesDeclarationLocations(bool useFileLocations)
    {
        var first = CreateDeclaration(name: "First");
        var second = CreateDeclaration(name: "Second");
        var unrelated = CreateDeclaration(name: "Unrelated", filePath: "Unrelated.cs");
        if (useFileLocations)
        {
            first = first with { Location = null };
            second = second with { Location = null };
            unrelated = unrelated with { Location = null };
        }

        var metadata = CreateProject(types: [first, second, unrelated]) with { SourceFiles = [] };
        var factory = new TemplateCodeModelAdapterFactory(metadata: metadata, templatePath: "models.tst");

        var item = Adapt(factory: factory, context: first, targetType: typeof(CodeClass));
        var file = item.Parent.Should().BeOfType<CodeFile>().Which;

        file.Path.Should().Be("Models.cs");
        file.Classes.Select(selector: model => model.Name).Should().Equal("First", "Second");
        AssertParents(items: GetDeclarations(file: file), parent: file);
    }

    [Fact]
    public void MetadataWithoutLocationsFallsBackToRenderedFile()
    {
        var declaration = CreateDeclaration(name: "Model") with { Location = null, FileLocations = [] };
        var metadata = new ProjectMetadata(ProjectPath: "Sample.csproj", SourceFiles: [], Types: [declaration], Diagnostics: []);
        var factory = new TemplateCodeModelAdapterFactory(metadata: metadata, templatePath: "models.tst");
        var item = Adapt(factory: factory, context: declaration, targetType: typeof(CodeClass));

        item.Parent.Should().BeSameAs(factory.CreateFile(project: metadata));
        item.Parent.FullName.Should().Be("Sample.csproj");
    }

    [Fact]
    public void PartialDeclarationUsesCurrentSourceFile()
    {
        var partial = CreateDeclaration(name: "Partial", filePath: "First.cs") with { FileLocations = ["First.cs", "Second.cs"] };
        var first = CreateDeclaration(name: "FirstOnly", filePath: "First.cs");
        var second = CreateDeclaration(name: "SecondOnly", filePath: "Second.cs");
        var firstFile = new SourceFileMetadata(Path: "First.cs", Types: [partial, first]);
        var secondFile = new SourceFileMetadata(Path: "Second.cs", Types: [partial, second]);
        var project = new ProjectMetadata(
            ProjectPath: "Sample.csproj", SourceFiles: [firstFile, secondFile], Types: [partial, first, second], Diagnostics: []);
        var scoped = project with { SourceFiles = [secondFile], Types = secondFile.Types };
        var factory = new TemplateCodeModelAdapterFactory(
            metadata: scoped,
            settings: new Typewriter.Configuration.Settings { TemplatePath = "models.tst" },
            metadataIndex: ProjectMetadataIndex.Create(metadata: project));

        var item = Adapt(factory: factory, context: partial, targetType: typeof(CodeClass));
        var file = item.Parent.Should().BeOfType<CodeFile>().Which;

        file.Path.Should().Be("Second.cs");
        file.Classes.Select(selector: model => model.Name).Should().Equal("Partial", "SecondOnly");
        file.Should().BeSameAs(factory.CreateFile(project: scoped));
    }

    [Fact]
    public void CompiledImportHelperExcludesColocatedClasses()
    {
        var metadata = CreateSampleProject();
        const string Template = """
            ${
                string Imports(Class c)
                {
                    var file = c.Parent as File;
                    var colocatedNames = new HashSet<string>(
                        file.Classes.Select(fc => fc.Name.ToLower())
                    );
                    return string.Join(",", c.Properties
                        .Where(p => !colocatedNames.Contains(p.Type.Name.ToLower()))
                        .Select(p => p.Type.Name));
                }
            }
            $Classes(FooViewModel)[$Imports]
            """;

        Render(template: Template, metadata: metadata).Should().Be("ExternalViewModel");
    }

    [Theory]
    [InlineData("Classes", "FooViewModel")]
    [InlineData("Records", "ModelRecord")]
    [InlineData("Structs", "ModelStruct")]
    [InlineData("Interfaces", "IModel")]
    [InlineData("Enums", "ModelStatus")]
    [InlineData("Types", "FooViewModel")]
    [InlineData("Delegates", "Callback")]
    public void TemplateParentExposesDeclaringFileCollections(string collection, string name)
    {
        var template = "$" + collection + "(" + name + ")[$Parent[$Name:$Classes(Bar*)[$Name]:$Records[$Name]:$Structs[$Name]:$Interfaces[$Name]:$Enums[$Name]:$Delegates[$Name]]]";

        Render(template: template, metadata: CreateSampleProject()).Should()
            .Be("FooViewModel:BarViewModel:ModelRecord:ModelStruct:IModel:ModelStatus:Callback");
    }

    [Theory]
    [InlineData("Class", "FooViewModel;BarViewModel;StaticModel;")]
    [InlineData("Record", "ModelRecord;")]
    [InlineData("Struct", "ModelStruct;")]
    [InlineData("Interface", "IModel;")]
    [InlineData("Enum", "ModelStatus;")]
    [InlineData("Foo*", "FooViewModel;")]
    [InlineData("Name=BarViewModel", "BarViewModel;")]
    [InlineData("Namespace=Sample", "FooViewModel;BarViewModel;StaticModel;ModelRecord;ModelStruct;IModel;ModelStatus;")]
    [InlineData("[Generate]", "FooViewModel;")]
    [InlineData(":IModel", "FooViewModel;")]
    [InlineData("HasProperties", "FooViewModel;")]
    [InlineData("HasMethods", "FooViewModel;")]
    [InlineData("HasConstants", "FooViewModel;")]
    [InlineData("Static", "StaticModel;")]
    [InlineData("NonStatic", "FooViewModel;BarViewModel;ModelRecord;ModelStruct;IModel;ModelStatus;")]
    [InlineData("Internal", "BarViewModel;")]
    [InlineData("Public", "FooViewModel;StaticModel;ModelRecord;ModelStruct;IModel;ModelStatus;")]
    public void ParentFileCollectionsSupportTemplateFilters(string filter, string expected)
    {
        var template = "$Classes(FooViewModel)[$Parent[$Types(" + filter + ")[$Name;]]]";

        Render(template: template, metadata: CreateSampleProject()).Should().Be(expected);
    }

    [Theory]
    [InlineData(TypeMetadataKind.Class, "Classes", "NestedClasses")]
    [InlineData(TypeMetadataKind.Record, "Records", "NestedRecords")]
    [InlineData(TypeMetadataKind.Struct, "Structs", "NestedInterfaces")]
    [InlineData(TypeMetadataKind.Interface, "Interfaces", "NestedStructs")]
    public void TemplatesTraverseNestedParentsThroughMetadataAndCodeModels(TypeMetadataKind kind, string collection, string nestedCollection)
    {
        var metadata = CreateProject(types: [CreateContainingDeclaration(kind: kind)]);
        var template = "$" + collection + "(Outer)[$" + nestedCollection + "[$Parent[$Parent[$Name]]]|$Parent[$"
            + collection + "(Outer)[$" + nestedCollection + "[$Parent[$Parent[$Name]]]]]]";

        Render(template: template, metadata: metadata).Should().Be("Models|Models");
    }

    [Fact]
    public void TypeReferencesExposeDeclaringFileInTemplates()
    {
        const string Template = "$Classes(FooViewModel)[$Properties[$Type[$Parent[$Name;]]]]";

        Render(template: Template, metadata: CreateSampleProject()).Should().Be("FooViewModel;ExternalViewModel;");
    }

    [Fact]
    public void ChildrenPointToTheirActualOwner()
    {
        var declaration = CreateSampleProject().Types[0];
        var attribute = declaration.Attributes.Single();
        var comment = new DocCommentMetadata(
            Summary: "Summary", Returns: "Result", Parameters: [new ParameterCommentMetadata(Name: "value", Description: "Value")]);
        var reference = CreateReference(type: declaration) with { Arity = 1 };
        var parameter = new ParameterMetadata(
            Name: "value",
            FullName: "Sample.FooViewModel.Run.value",
            Type: reference,
            HasDefaultValue: false,
            DefaultValue: null,
            Attributes: [attribute],
            ParentMethodFullName: "Sample.FooViewModel.Run");
        var callback = CreateDelegate(name: "Nested", parentTypeFullName: declaration.FullName) with
        {
            Parameters = [parameter], Attributes = [attribute], DocComment = comment, TypeParameters = [new TypeParameterMetadata(Name: "T")],
        };
        declaration = declaration with
        {
            DocComment = comment,
            TypeParameters = [new TypeParameterMetadata(Name: "T")],
            Properties = [declaration.Properties[0] with { Type = reference, Attributes = [attribute], DocComment = comment, Parameters = [parameter] }],
            Methods = [declaration.Methods.Single() with { Attributes = [attribute], DocComment = comment, Parameters = [parameter], TypeParameters = [new TypeParameterMetadata(Name: "T")] }],
            Constants = [declaration.Constants.Single() with { Attributes = [attribute], DocComment = comment }],
            Fields = [new FieldMetadata(Name: "Field", FullName: "Sample.FooViewModel.Field", Accessibility: MetadataAccessibility.Public, Type: reference, Attributes: [attribute], ParentTypeFullName: declaration.FullName) { DocComment = comment }],
            StaticReadOnlyFields = [new StaticReadOnlyFieldMetadata(Name: "Instance", FullName: "Sample.FooViewModel.Instance", Accessibility: MetadataAccessibility.Public, Type: reference, Value: null, Attributes: [attribute], ParentTypeFullName: declaration.FullName) { DocComment = comment }],
            Events = [new EventMetadata(Name: "Changed", FullName: "Sample.FooViewModel.Changed", Accessibility: MetadataAccessibility.Public, Type: reference, Attributes: [attribute], ParentTypeFullName: declaration.FullName) { DocComment = comment }],
            Delegates = [callback],
        };
        var metadata = CreateProject(types: [declaration]);
        var factory = new TemplateCodeModelAdapterFactory(metadata: metadata, templatePath: "models.tst");
        var file = factory.CreateFile(project: metadata);
        var model = file.Classes.Single();
        (CodeItem Item, Typewriter.CodeModel.IAttributeCollection Attributes, Typewriter.CodeModel.DocComment? Comment)[] members =
        [
            (model.Properties.Single(), model.Properties.Single().Attributes, model.Properties.Single().DocComment),
            (model.Methods.Single(), model.Methods.Single().Attributes, model.Methods.Single().DocComment),
            (model.Constants.Single(), model.Constants.Single().Attributes, model.Constants.Single().DocComment),
            (model.Fields.Single(), model.Fields.Single().Attributes, model.Fields.Single().DocComment),
            (model.StaticReadOnlyFields.Single(), model.StaticReadOnlyFields.Single().Attributes, model.StaticReadOnlyFields.Single().DocComment),
            (model.Events.Single(), model.Events.Single().Attributes, model.Events.Single().DocComment),
            (model.Delegates.Single(), model.Delegates.Single().Attributes, model.Delegates.Single().DocComment),
        ];

        AssertParents(items: members.Select(selector: member => member.Item), parent: model);
        model.Parent.Should().BeSameAs(file);
        model.Attributes.Single().Parent.Should().BeSameAs(model);
        model.Attributes.Single().Arguments.Single().Parent.Should().BeSameAs(model.Attributes.Single());
        model.DocComment!.Parent.Should().BeSameAs(model);
        model.DocComment.Parameters.Single().Parent.Should().BeSameAs(model.DocComment);
        model.TypeParameters.Single().Parent.Should().BeSameAs(model);
        model.Methods.Single().Parameters.Single().Parent.Should().BeSameAs(model.Methods.Single());
        model.Methods.Single().Parameters.Single().Attributes.Single().Parent.Should().BeSameAs(model.Methods.Single().Parameters.Single());
        model.Methods.Single().TypeParameters.Single().Parent.Should().BeSameAs(model.Methods.Single());
        model.Properties.Single().Parameters.Single().Parent.Should().BeSameAs(model.Properties.Single());
        model.Delegates.Single().Parameters.Single().Parent.Should().BeSameAs(model.Delegates.Single());
        model.Delegates.Single().TypeParameters.Single().Parent.Should().BeSameAs(model.Delegates.Single());
        model.Properties.Single().Type.Parent.Should().BeSameAs(file);

        foreach (var (member, attributes, docComment) in members)
        {
            attributes.Single().Parent.Should().BeSameAs(member);
            docComment.Should().NotBeNull();
            docComment.Parent.Should().BeSameAs(member);
        }
    }

    [Fact]
    public void EnumValuesAndTupleElementsHaveTheirOwningParents()
    {
        var declaration = CreateDeclaration(name: "Status", kind: TypeMetadataKind.Enum) with
        {
            EnumValues = [new EnumValueMetadata(Name: "Active", Value: 1) { ParentTypeFullName = "Sample.Status" }],
        };
        var metadata = CreateProject(types: [declaration]);
        var factory = new TemplateCodeModelAdapterFactory(metadata: metadata, templatePath: "models.tst");
        var file = factory.CreateFile(project: metadata);
        var model = file.Enums.Single();
        model.Values.Single().Parent.Should().BeSameAs(model);
        model.Values.Single().Parent!.Parent.Should().BeSameAs(file);

        var tuple = CreateReference(type: declaration) with
        {
            Name = "ValueTuple",
            FullName = "System.ValueTuple",
            Namespace = "System",
            IsEnum = false,
            IsValueTuple = true,
            TupleElements =
            [
                new FieldMetadata(
                    Name: "status",
                    FullName: "status",
                    Accessibility: MetadataAccessibility.Public,
                    Type: CreateReference(type: declaration),
                    Attributes: [],
                    ParentTypeFullName: string.Empty),
            ],
        };
        var tupleModel = (CodeType)Adapt(factory: factory, context: tuple, targetType: typeof(CodeType));

        tupleModel.TupleElements.Single().Parent.Should().BeSameAs(tupleModel);
        tupleModel.TupleElements.Single().Type!.Parent.Should().BeSameAs(file);
        tupleModel.Parent.Should().BeNull();
    }

    private static string Render(string template, ProjectMetadata metadata)
    {
        var diagnostics = new List<GenerationDiagnostic>();
        var document = TemplateDocument.Parse(template: new TemplateFile(Path: "models.tst", Content: template), diagnostics: diagnostics);
        var output = new TemplateRenderer(typeMapper: new TypeScriptTypeMapper()).Render(
            template: document, metadata: metadata, diagnostics: diagnostics);
        diagnostics.Should().BeEmpty();
        return output.Trim();
    }

    private static CodeItem Adapt(TemplateCodeModelAdapterFactory factory, object context, System.Type targetType)
    {
        factory.TryAdapt(context: context, targetType: targetType, adapted: out var adapted).Should().BeTrue();
        return adapted.Should().BeAssignableTo<CodeItem>().Which;
    }

    private static void AssertParents(IEnumerable<CodeItem> items, CodeItem parent)
    {
        foreach (var item in items)
        {
            item.Parent.Should().BeSameAs(parent);
            if (GetTypeModel(item: item) is { } type)
            {
                type.Parent.Should().BeSameAs(parent);
            }
        }
    }

    private static System.Type GetModelType(TypeMetadataKind kind) =>
        kind switch
        {
            TypeMetadataKind.Class => typeof(CodeClass),
            TypeMetadataKind.Record => typeof(CodeRecord),
            TypeMetadataKind.Struct => typeof(CodeStruct),
            TypeMetadataKind.Interface => typeof(CodeInterface),
            TypeMetadataKind.Enum => typeof(CodeEnum),
            _ => throw new ArgumentOutOfRangeException(paramName: nameof(kind)),
        };

    private static CodeType? GetTypeModel(CodeItem item) =>
        item switch
        {
            CodeClass model => model.Type,
            CodeRecord model => model.Type,
            CodeStruct model => model.Type,
            CodeInterface model => model.Type,
            CodeEnum model => model.Type,
            CodeType model => model,
            _ => null,
        };

    private static IEnumerable<CodeItem> GetDeclarations(CodeFile file) =>
        file.Classes.Cast<CodeItem>().Concat(second: file.Records).Concat(second: file.Structs)
            .Concat(second: file.Interfaces).Concat(second: file.Enums).Concat(second: file.Delegates).Concat(second: file.Types);

    private static IEnumerable<CodeItem> GetNestedDeclarations(CodeItem item) =>
        item switch
        {
            CodeClass model => model.NestedClasses.Cast<CodeItem>().Concat(second: model.NestedRecords)
                .Concat(second: model.NestedStructs).Concat(second: model.NestedInterfaces).Concat(second: model.NestedEnums).Concat(second: model.Delegates),
            CodeRecord model => model.NestedClasses.Cast<CodeItem>().Concat(second: model.NestedRecords)
                .Concat(second: model.NestedStructs).Concat(second: model.NestedInterfaces).Concat(second: model.NestedEnums).Concat(second: model.Delegates),
            CodeStruct model => model.NestedClasses.Cast<CodeItem>().Concat(second: model.NestedRecords)
                .Concat(second: model.NestedStructs).Concat(second: model.NestedInterfaces).Concat(second: model.NestedEnums).Concat(second: model.Delegates),
            CodeInterface model => model.NestedStructs,
            _ => [],
        };

    private static IEnumerable<TypeMetadata> GetNestedMetadata(TypeMetadata type) =>
        type.NestedClasses.Concat(second: type.NestedRecords).Concat(second: type.NestedStructs)
            .Concat(second: type.NestedInterfaces).Concat(second: type.NestedEnums);

    private static TypeMetadata CreateContainingDeclaration(TypeMetadataKind kind)
    {
        var outer = CreateDeclaration(name: "Outer", kind: kind);
        TypeMetadata Nested(string name, TypeMetadataKind nestedKind) =>
            CreateDeclaration(name: name, kind: nestedKind) with
            {
                FullName = "Sample.Outer." + name,
                ContainingTypeFullName = outer.FullName,
            };
        return outer with
        {
            NestedClasses = [Nested(name: "NestedClass", nestedKind: TypeMetadataKind.Class)],
            NestedRecords = [Nested(name: "NestedRecord", nestedKind: TypeMetadataKind.Record)],
            NestedStructs = [Nested(name: "NestedStruct", nestedKind: TypeMetadataKind.Struct)],
            NestedInterfaces = [Nested(name: "NestedInterface", nestedKind: TypeMetadataKind.Interface)],
            NestedEnums = [Nested(name: "NestedEnum", nestedKind: TypeMetadataKind.Enum)],
            Delegates = [CreateDelegate(name: "NestedDelegate", parentTypeFullName: outer.FullName)],
        };
    }

    private static ProjectMetadata CreateSampleProject()
    {
        const string FilePath = "FooViewModel.cs";
        var sibling = CreateDeclaration(name: "BarViewModel", filePath: FilePath) with { Accessibility = MetadataAccessibility.Internal };
        var external = CreateDeclaration(name: "ExternalViewModel", filePath: "ExternalViewModel.cs");
        var contract = CreateDeclaration(name: "IModel", kind: TypeMetadataKind.Interface, filePath: FilePath);
        var foo = CreateDeclaration(name: "FooViewModel", filePath: FilePath) with
        {
            Attributes = [new AttributeMetadata(Name: "Generate", FullName: "Sample.GenerateAttribute", Arguments: [new AttributeArgumentMetadata(Name: null, Value: "value")])],
            BaseTypes = [CreateReference(type: contract)],
            Properties = new[] { sibling, external }.Select(
                selector: type => new PropertyMetadata(
                    Name: type.Name,
                    FullName: "Sample.FooViewModel." + type.Name,
                    Type: CreateReference(type: type),
                    Accessibility: MetadataAccessibility.Public,
                    HasGetter: true,
                    HasSetter: true,
                    IsRequired: false,
                    Attributes: [])
                {
                    ParentTypeFullName = "Sample.FooViewModel",
                }).ToArray(),
            Methods = [new MethodMetadata(Name: "Run", FullName: "Sample.FooViewModel.Run", ReturnType: CreateReference(type: sibling), Accessibility: MetadataAccessibility.Public, IsStatic: false, IsAbstract: false, IsGeneric: false, Parameters: [], Attributes: [], ParentTypeFullName: "Sample.FooViewModel")],
            Constants = [new ConstantMetadata(Name: "Version", FullName: "Sample.FooViewModel.Version", Accessibility: MetadataAccessibility.Public, Type: CreateReference(type: sibling), Value: "1", Attributes: [], ParentTypeFullName: "Sample.FooViewModel")],
        };
        var callback = CreateDelegate(name: "Callback", filePath: FilePath);
        var project = CreateProject(
            types:
            [
                foo, sibling,
                CreateDeclaration(name: "StaticModel", filePath: FilePath) with { IsStatic = true },
                CreateDeclaration(name: "ModelRecord", kind: TypeMetadataKind.Record, filePath: FilePath),
                CreateDeclaration(name: "ModelStruct", kind: TypeMetadataKind.Struct, filePath: FilePath),
                contract,
                CreateDeclaration(name: "ModelStatus", kind: TypeMetadataKind.Enum, filePath: FilePath),
                external,
            ]);
        return project with
        {
            SourceFiles = [project.SourceFiles[0] with { Delegates = [callback] }, project.SourceFiles[1]],
            Delegates = [callback],
        };
    }

    private static ProjectMetadata CreateProject(IReadOnlyList<TypeMetadata> types) =>
        new(
            ProjectPath: "Sample.csproj",
            SourceFiles: types.GroupBy(keySelector: type => type.FileLocations[0], comparer: StringComparer.OrdinalIgnoreCase)
                .Select(selector: group => new SourceFileMetadata(Path: group.Key, Types: group.ToArray())).ToArray(),
            Types: types,
            Diagnostics: []);

    private static TypeMetadata CreateDeclaration(string name, TypeMetadataKind kind = TypeMetadataKind.Class, string filePath = "Models.cs") =>
        new(
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
            Location = new SourceLocation(Path: filePath, Line: 1, Column: 1),
            FileLocations = [filePath],
        };

    private static TypeMetadataReference CreateReference(TypeMetadata type) =>
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

    private static DelegateMetadata CreateDelegate(string name, string parentTypeFullName = "", string filePath = "Models.cs") =>
        new(
            Name: name,
            FullName: string.IsNullOrEmpty(value: parentTypeFullName) ? "Sample." + name : parentTypeFullName + "." + name,
            Accessibility: MetadataAccessibility.Public,
            ReturnType: CreateReference(type: CreateDeclaration(name: "Result")),
            IsGeneric: false,
            Parameters: [],
            Attributes: [],
            ParentTypeFullName: parentTypeFullName)
        {
            Location = new SourceLocation(Path: filePath, Line: 1, Column: 1),
        };
}
