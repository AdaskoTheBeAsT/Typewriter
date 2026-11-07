using System.Globalization;
using AwesomeAssertions;
using Typewriter.Abstractions;
using Typewriter.Configuration;
using Xunit;
using CodeClass = Typewriter.CodeModel.Class;
using CodeType = Typewriter.CodeModel.Type;

namespace Typewriter.Engine.Tests;

public sealed class TemplateAdapterScalingTests
{
    [Fact]
    public void PerSourceFactoriesDoNotEnumerateTheSharedSourceMap()
    {
        var declaration = AdapterTestData.CreateType();
        var source = new SourceFileMetadata(Path: "Model.cs", Types: [declaration]);
        var sources = new AdapterCountingList<SourceFileMetadata>(
            items: Enumerable.Range(start: 0, count: 2000)
                .Select(selector: number => source with { Path = string.Create(provider: CultureInfo.InvariantCulture, handler: $"Model{number}.cs") }).ToArray());
        var project = AdapterTestData.CreateProject(types: [declaration], sources: sources);
        var index = ProjectMetadataIndex.Create(metadata: project);
        var scoped = project with { SourceFiles = [source] };
        sources.Enumerations = 0;

        for (var iteration = 0; iteration < 100; iteration++)
        {
            _ = new TemplateCodeModelAdapterFactory(metadata: scoped, settings: new Settings(), metadataIndex: index);
        }

        sources.Enumerations.Should().Be(0);
    }

    [Fact]
    public void NestedClassTraversalDoesNotBuildTheParallelTypeSubtrees()
    {
        const int Depth = 12;
        var properties = new AdapterCountingList<PropertyMetadata>(items: []);
        var root = AdapterTestData.CreateType(name: "Leaf") with { Properties = properties };
        for (var level = 1; level < Depth; level++)
        {
            root = AdapterTestData.CreateType(name: string.Create(provider: CultureInfo.InvariantCulture, handler: $"Level{level}")) with
            {
                Properties = properties,
                NestedClasses = [root],
            };
        }

        var project = AdapterTestData.CreateProject(types: [root]);
        var factory = new TemplateCodeModelAdapterFactory(metadata: project, templatePath: "models.tst");
        properties.Enumerations = 0;
        var current = factory.CreateFile(project: project).Classes.Single();
        for (var level = 1; level < Depth; level++)
        {
            var nested = current.NestedClasses.Single();
            nested.Should().BeSameAs(current.NestedClasses.Single());
            nested.Parent.Should().BeSameAs(current);
            nested.Type.Parent.Should().BeSameAs(current);
            current = nested;
        }

        current.Name.Should().Be("Leaf");
        properties.Enumerations.Should().BeLessThanOrEqualTo(2 * Depth);
    }

    [Fact]
    public void BaseClassTraversalDoesNotBuildTheParallelTypeBaseChains()
    {
        const int Depth = 12;
        var properties = new AdapterCountingList<PropertyMetadata>(items: []);
        var types = new List<TypeMetadata> { AdapterTestData.CreateType(name: "Base") with { Properties = properties } };
        for (var level = 1; level < Depth; level++)
        {
            var previous = types[index: level - 1];
            types.Add(item: AdapterTestData.CreateType(name: string.Create(provider: CultureInfo.InvariantCulture, handler: $"Level{level}")) with
            {
                Properties = properties,
                BaseTypes = [AdapterTestData.CreateReference(name: previous.Name, fullName: previous.FullName) with { IsPrimitive = false }],
            });
        }

        var project = AdapterTestData.CreateProject(types: types);
        var factory = new TemplateCodeModelAdapterFactory(metadata: project, templatePath: "models.tst");
        properties.Enumerations = 0;
        factory.TryAdapt(context: types[index: Depth - 1], targetType: typeof(CodeClass), adapted: out var adapted).Should().BeTrue();
        var current = adapted.Should().BeOfType<CodeClass>().Which;
        for (var level = 1; level < Depth; level++)
        {
            current = current.BaseClass.Should().BeOfType<CodeClass>().Which;
        }

        current.Name.Should().Be("Base");
        current.BaseClass.Should().BeNull();
        properties.Enumerations.Should().BeLessThanOrEqualTo(2 * Depth);
    }

    [Fact]
    public void UnusedFileCollectionsDoNotAdaptDeclarations()
    {
        var properties = new AdapterCountingList<PropertyMetadata>(items: []);
        var declaration = AdapterTestData.CreateType() with { Properties = properties };
        var project = AdapterTestData.CreateProject(types: [declaration]);
        var factory = new TemplateCodeModelAdapterFactory(metadata: project, templatePath: "models.tst");
        properties.Enumerations = 0;

        var file = factory.CreateFile(project: project);

        file.Name.Should().Be("Sample");
        properties.Enumerations.Should().Be(0);
        file.Classes.Single().Parent.Should().BeSameAs(file);
        properties.Enumerations.Should().Be(0);
        file.Classes.Single().Properties.Should().BeEmpty();
        properties.Enumerations.Should().Be(1);
    }

    [Fact]
    public void MemberParentNamesDoNotRebuildUnusedOwnerAttributeGraphs()
    {
        var attributes = new AdapterCountingList<AttributeMetadata>(
            items: [new AttributeMetadata(Name: "Example", FullName: "Sample.Example", Arguments: [new AttributeArgumentMetadata(Name: null, Value: "text")])]);
        var property = new PropertyMetadata(
            Name: "Value",
            FullName: "Sample.Model.Value",
            Type: AdapterTestData.CreateReference(),
            Accessibility: MetadataAccessibility.Public,
            HasGetter: true,
            HasSetter: true,
            IsRequired: false,
            Attributes: [])
        {
            ParentTypeFullName = "Sample.Model",
        };
        var declaration = AdapterTestData.CreateType() with { Attributes = attributes, Properties = [property] };
        var factory = new TemplateCodeModelAdapterFactory(
            metadata: AdapterTestData.CreateProject(types: [declaration]),
            templatePath: "models.tst");
        CodeClass? owner = null;

        for (var iteration = 0; iteration < 50; iteration++)
        {
            factory.TryAdapt(context: property, targetType: typeof(Typewriter.CodeModel.Property), adapted: out var adapted).Should().BeTrue();
            owner = adapted.Should().BeOfType<Typewriter.CodeModel.Property>().Which.Parent.Should().BeOfType<CodeClass>().Which;
            owner.Name.Should().Be("Model");
        }

        attributes.Enumerations.Should().Be(0);
        var attribute = owner!.Attributes.Single();
        attribute.Parent.Should().BeSameAs(owner);
        attribute.Arguments.Single().Parent.Should().BeSameAs(attribute);
        attributes.Enumerations.Should().Be(1);
    }

    [Fact]
    public void SharedMapperHonorsMutableVirtualSettingsWithoutReusingWrappers()
    {
        var project = AdapterTestData.CreateProject(types: []);
        var index = ProjectMetadataIndex.Create(metadata: project);
        var settings = new ObservableSettings { DateType = "FirstDate" };
        var factory = new TemplateCodeModelAdapterFactory(metadata: project, settings: settings, metadataIndex: index);
        var reference = AdapterTestData.CreateReference(name: "DateTime", fullName: "System.DateTime") with
        {
            IsNullable = true,
            IsPrimitive = false,
            IsDateLike = true,
        };

        var first = AdaptType(factory: factory, reference: reference);
        var initialReads = settings.Reads;
        settings.DateType = "SecondDate";
        var second = AdaptType(factory: factory, reference: reference);
        var other = new TemplateCodeModelAdapterFactory(
            metadata: project,
            settings: new ObservableSettings { DateType = "ThirdDate" },
            metadataIndex: index);

        first.Name.Should().Be("FirstDate");
        second.Name.Should().Be("SecondDate");
        second.Should().NotBeSameAs(first);
        second.IsNullable.Should().BeTrue();
        settings.Reads.Should().BeGreaterThan(initialReads);
        AdaptType(factory: other, reference: reference).Name.Should().Be("ThirdDate");
    }

    [Fact]
    public void NestedCollectionsDoNotBuildBothElementAndTypeArgumentTrees()
    {
        const int Depth = 12;
        var leafArguments = new AdapterCountingList<TypeMetadataReference>(items: []);
        var reference = AdapterTestData.CreateReference() with { TypeArguments = leafArguments };
        for (var level = 1; level < Depth; level++)
        {
            reference = AdapterTestData.CreateReference(name: "List", fullName: "System.Collections.Generic.List") with
            {
                IsPrimitive = false,
                IsCollection = true,
                ElementType = reference,
                TypeArguments = [reference],
            };
        }

        var factory = new TemplateCodeModelAdapterFactory(
            metadata: AdapterTestData.CreateProject(types: []),
            settings: new Settings());
        var current = AdaptType(factory: factory, reference: reference);
        for (var level = 1; level < Depth; level++)
        {
            var element = current.ElementType;
            element.Should().NotBeNull();
            current.ElementType.Should().BeSameAs(element);
            current = element;
        }

        current.Name.Should().Be("string");
        leafArguments.Enumerations.Should().Be(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AttributeArgumentGraphDeferralPreservesCustomSettingsEagerness(bool customSettings)
    {
        var arguments = new AdapterCountingList<AttributeArgumentMetadata>(
            items: [
                new AttributeArgumentMetadata(Name: "First", Value: "one"),
                new AttributeArgumentMetadata(Name: "Second", Value: "two"),
            ]);
        var metadata = new AttributeMetadata(Name: "Example", FullName: "Sample.Example", Arguments: arguments);
        Settings settings = customSettings ? new ObservableSettings() : new Settings();
        var factory = new TemplateCodeModelAdapterFactory(
            metadata: AdapterTestData.CreateProject(types: []),
            settings: settings);
        factory.TryAdapt(context: metadata, targetType: typeof(Typewriter.CodeModel.Attribute), adapted: out var adapted).Should().BeTrue();
        var attribute = adapted.Should().BeOfType<Typewriter.CodeModel.Attribute>().Which;

        arguments.Enumerations.Should().Be(customSettings ? 2 : 0);
        attribute.Value.Should().Be("First = \"one\", Second = \"two\"");
        arguments.Enumerations.Should().Be(customSettings ? 2 : 1);
        attribute.Arguments.Should().HaveCount(2);
        attribute.Arguments[0].Parent.Should().BeSameAs(attribute);
        arguments.Enumerations.Should().Be(2);
        new Typewriter.CodeModel.Attribute { Value = "explicit" }.Value.Should().Be("explicit");
    }

    [Fact]
    public void PublicCollectionConstructorsStillTakeEagerSnapshots()
    {
        var items = new List<CodeClass> { new() { Name = "Original" } };
        var collection = new Typewriter.CodeModel.ClassCollection(items: items);

        items.Clear();

        collection.Should().ContainSingle().Which.Name.Should().Be("Original");
    }

    [Fact]
    public void NavigationPropertiesRetainTheirPublicInitContracts()
    {
        var owner = new CodeClass { Name = "Owner" };
        var model = new CodeClass { BaseClass = owner, ContainingClass = owner };
        var type = new CodeType { BaseClass = owner, ContainingClass = owner };
        var recordOwner = new Typewriter.CodeModel.Record();
        var record = new Typewriter.CodeModel.Record { BaseRecord = recordOwner, ContainingRecord = recordOwner };

        model.BaseClass.Should().BeSameAs(owner);
        model.ContainingClass.Should().BeSameAs(owner);
        type.BaseClass.Should().BeSameAs(owner);
        type.ContainingClass.Should().BeSameAs(owner);
        record.BaseRecord.Should().BeSameAs(recordOwner);
        record.ContainingRecord.Should().BeSameAs(recordOwner);
    }

    private static CodeType AdaptType(TemplateCodeModelAdapterFactory factory, TypeMetadataReference reference)
    {
        factory.TryAdapt(context: reference, targetType: typeof(CodeType), adapted: out var adapted).Should().BeTrue();
        return adapted.Should().BeAssignableTo<CodeType>().Which;
    }

    private sealed class ObservableSettings : Settings
    {
        public string DateType { get; set; } = "Date";

        public int Reads { get; private set; }

        public override string DateTypeGeneration
        {
            get
            {
                Reads++;
                return DateType;
            }
        }
    }
}
