using System.Globalization;
using AwesomeAssertions;
using Typewriter.Abstractions;
using Xunit;
using CodeClass = Typewriter.CodeModel.Class;
using CodeFile = Typewriter.CodeModel.File;

namespace Typewriter.Engine.Tests;

public sealed class TemplateAdapterConcurrencyTests
{
    [Fact]
    public void ParallelDeferredTraversalPreservesFilterContextsAndSourceFileIdentity()
    {
        var types = Enumerable.Range(start: 0, count: 64).Select(selector: CreateDeclaration).ToArray();
        var sources = types.GroupBy(keySelector: type => type.FileLocations[0], comparer: StringComparer.Ordinal)
            .Select(selector: group => new SourceFileMetadata(Path: group.Key, Types: group.ToArray()))
            .ToArray();
        var project = AdapterTestData.CreateProject(types: types, sources: sources);
        var factory = new TemplateCodeModelAdapterFactory(metadata: project, templatePath: "models.tst");
        var file = factory.CreateFile(project: project);
        var models = file.Classes.ToArray();
        var options = new ParallelOptions { CancellationToken = TestContext.Current.CancellationToken };

        Parallel.For(
            fromInclusive: 0,
            toExclusive: models.Length * 4,
            parallelOptions: options,
            body: iteration =>
            {
                var index = iteration % models.Length;
                var model = models[index];
                var metadata = types[index];
                factory.CreateFile(project: project).Should().BeSameAs(file);
                AssertRegistrationsAndParents(factory: factory, model: model, metadata: metadata);
                AssertSourceFileIdentity(factory: factory, model: model, metadata: metadata);
            });

        models.Select(selector: model => model.Parent).Distinct().Should().HaveCount(4);
    }

    private static void AssertRegistrationsAndParents(TemplateCodeModelAdapterFactory factory, CodeClass model, TypeMetadata metadata)
    {
        factory.GetFilterContext(item: model).Should().BeSameAs(metadata);
        factory.GetFilterContext(item: model.Type).Should().BeSameAs(metadata);
        for (var index = 0; index < metadata.Properties.Count; index++)
        {
            var property = model.Properties[index];
            property.Parent.Should().BeSameAs(model);
            factory.GetFilterContext(item: property).Should().BeSameAs(metadata.Properties[index]);
            var typeProperty = model.Type.Properties[index];
            typeProperty.Parent.Should().BeSameAs(model.Type);
            factory.GetFilterContext(item: typeProperty).Should().BeSameAs(metadata.Properties[index]);
        }

        var method = model.Methods.Single();
        method.Parent.Should().BeSameAs(model);
        factory.GetFilterContext(item: method).Should().BeSameAs(metadata.Methods.Single());
        var parameter = method.Parameters.Single();
        parameter.Parent.Should().BeSameAs(method);
        factory.GetFilterContext(item: parameter).Should().BeSameAs(metadata.Methods.Single().Parameters.Single());
        var attribute = model.Attributes.Single();
        attribute.Parent.Should().BeSameAs(model);
        factory.GetFilterContext(item: attribute).Should().BeSameAs(metadata.Attributes.Single());
        attribute.Arguments.Single().Parent.Should().BeSameAs(attribute);
        attribute.Value.Should().Be("text");
    }

    private static void AssertSourceFileIdentity(TemplateCodeModelAdapterFactory factory, CodeClass model, TypeMetadata metadata)
    {
        var parent = model.Parent.Should().BeOfType<CodeFile>().Which;
        parent.Path.Should().Be(metadata.FileLocations.Single());
        model.Type.Parent.Should().BeSameAs(parent);
        factory.ResolveParentFile(type: metadata).Should().BeSameAs(parent);
        var fileModel = parent.Classes.Single(predicate: item => item.Name == model.Name);
        fileModel.Parent.Should().BeSameAs(parent);
        fileModel.Type.Parent.Should().BeSameAs(parent);
        factory.GetFilterContext(item: fileModel).Should().BeSameAs(metadata);
        factory.TryAdapt(context: metadata, targetType: typeof(CodeClass), adapted: out var adapted).Should().BeTrue();
        var independentModel = adapted.Should().BeOfType<CodeClass>().Which;
        independentModel.Should().NotBeSameAs(model);
        independentModel.Parent.Should().BeSameAs(parent);
        independentModel.Properties[0].Parent.Should().BeSameAs(independentModel);
        factory.GetFilterContext(item: independentModel.Properties[0]).Should().BeSameAs(metadata.Properties[0]);
    }

    private static TypeMetadata CreateDeclaration(int index)
    {
        var name = "Model" + index.ToString(provider: CultureInfo.InvariantCulture);
        var fullName = "Sample." + name;
        var parameter = new ParameterMetadata(
            Name: "value",
            FullName: fullName + ".Run.value",
            Type: AdapterTestData.CreateReference(),
            HasDefaultValue: false,
            DefaultValue: null,
            Attributes: [],
            ParentMethodFullName: fullName + ".Run");
        return AdapterTestData.CreateType(name: name) with
        {
            FileLocations = ["Models" + (index % 4).ToString(provider: CultureInfo.InvariantCulture) + ".cs"],
            Properties = Enumerable.Range(start: 0, count: 8)
                .Select(selector: propertyIndex => new PropertyMetadata(
                    Name: "Value" + propertyIndex.ToString(provider: CultureInfo.InvariantCulture),
                    FullName: fullName + ".Value" + propertyIndex.ToString(provider: CultureInfo.InvariantCulture),
                    Type: AdapterTestData.CreateReference(),
                    Accessibility: MetadataAccessibility.Public,
                    HasGetter: true,
                    HasSetter: true,
                    IsRequired: false,
                    Attributes: [])
                {
                    ParentTypeFullName = fullName,
                })
                .ToArray(),
            Methods = [new MethodMetadata(
                Name: "Run",
                FullName: fullName + ".Run",
                ReturnType: AdapterTestData.CreateReference(),
                Accessibility: MetadataAccessibility.Public,
                IsStatic: false,
                IsAbstract: false,
                IsGeneric: false,
                Parameters: [parameter],
                Attributes: [],
                ParentTypeFullName: fullName),
            ],
            Attributes = [new AttributeMetadata(
                Name: "Example",
                FullName: "Sample.Example",
                Arguments: [new AttributeArgumentMetadata(Name: null, Value: "text")]),
            ],
        };
    }
}
