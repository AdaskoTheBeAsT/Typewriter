using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using AwesomeAssertions;
using Typewriter.Abstractions;
using Typewriter.Configuration;
using Xunit;
using CodeAttribute = Typewriter.CodeModel.Attribute;
using CodeClass = Typewriter.CodeModel.Class;
using CodeType = Typewriter.CodeModel.Type;

namespace Typewriter.Engine.Tests;

public sealed class LazyCodeModelTests
{
    [Theory]
    [InlineData(typeof(CodeClass), 17)]
    [InlineData(typeof(CodeType), 16)]
    public void DefaultValuesAreLazyStableIndependentAndThreadSafe(System.Type modelType, int expectedCount)
    {
        var properties = GetDefaultProperties(modelType: modelType);
        properties.Should().HaveCount(expectedCount);
        foreach (var property in properties)
        {
            var model = Activator.CreateInstance(type: modelType)!;
            foreach (var other in properties)
            {
                GetDefaultField(modelType: modelType, property: other).GetValue(obj: model).Should().BeNull();
            }

            var values = new object?[64];
            Parallel.For(
                fromInclusive: 0,
                toExclusive: values.Length,
                parallelOptions: new ParallelOptions { CancellationToken = TestContext.Current.CancellationToken },
                body: index => values[index] = property.GetValue(obj: model));
            values[0].Should().NotBeNull();
            values.Should().OnlyContain(predicate: value => ReferenceEquals(value, values[0]));
            property.GetValue(obj: Activator.CreateInstance(type: modelType)).Should().NotBeSameAs(values[0]);
            foreach (var other in properties.Where(predicate: other => other != property))
            {
                GetDefaultField(modelType: modelType, property: other).GetValue(obj: model).Should().BeNull();
            }
        }
    }

    [Theory]
    [InlineData(typeof(CodeClass))]
    [InlineData(typeof(CodeType))]
    public void ExplicitInitializersIncludingNullBypassDefaults(System.Type modelType)
    {
        foreach (var property in GetDefaultProperties(modelType: modelType))
        {
            var model = Activator.CreateInstance(type: modelType)!;
            var supplied = property.GetValue(obj: Activator.CreateInstance(type: modelType));
            property.SetValue(obj: model, value: supplied);
            property.GetValue(obj: model).Should().BeSameAs(supplied);
            property.SetValue(obj: model, value: null);
            property.GetValue(obj: model).Should().BeNull();
        }
    }

    [Fact]
    public void CountAndShortCircuitTraversalOnlyAdaptRequestedAttributes()
    {
        var declaration = AdapterTestData.CreateType() with
        {
            Attributes = Enumerable.Range(start: 0, count: 1000)
                .Select(selector: _ => new AttributeMetadata(Name: "Example", FullName: "Sample.Example", Arguments: []))
                .ToArray(),
        };
        var project = AdapterTestData.CreateProject(types: [declaration]);
        var factory = new TemplateCodeModelAdapterFactory(metadata: project, templatePath: "models.tst");
        var model = factory.CreateFile(project: project).Classes.Single();
        var registrations = (IEnumerable)typeof(TemplateCodeModelAdapterFactory)
            .GetField(name: "_metadataByItem", bindingAttr: BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(obj: factory)!;
        var before = registrations.Cast<object>().Count();

        model.Attributes.Count.Should().Be(1000);
        registrations.Cast<object>().Should().HaveCount(before);
        var attribute = model.Attributes.First(predicate: static _ => true);
        attribute.Parent.Should().BeSameAs(model);
        model.Attributes[0].Should().BeSameAs(attribute);
        registrations.Cast<object>().Should().HaveCount(before + 1);
        model.Attributes[999].Parent.Should().BeSameAs(model);
        registrations.Cast<object>().Should().HaveCount(before + 2);
        GC.KeepAlive(obj: model);
    }

    [Fact]
    public void FilterMetadataDoesNotKeepDiscardedAdaptersAlive()
    {
        var declaration = AdapterTestData.CreateType();
        var factory = new TemplateCodeModelAdapterFactory(metadata: AdapterTestData.CreateProject(types: [declaration]), templatePath: "models.tst");
        var reference = AdaptAndRelease(factory: factory, declaration: declaration);
        for (var attempt = 0; attempt < 5 && reference.IsAlive; attempt++)
        {
#pragma warning disable S1215 // Force collection to verify that the lookup does not own adapters.
            GC.Collect();
#pragma warning restore S1215 // Force collection to verify that the lookup does not own adapters.
            GC.WaitForPendingFinalizers();
        }

        reference.IsAlive.Should().BeFalse();
        GC.KeepAlive(obj: factory);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AttributeTypesCaptureMappingSettingsAndPreserveCustomEagerness(bool customSettings)
    {
        Settings settings = customSettings ? new CustomSettings() : new Settings();
        settings.UseDateType(dateType: "Before");
        var reference = AdapterTestData.CreateReference(name: "DateTime", fullName: "System.DateTime") with
        {
            IsPrimitive = false,
            IsDateLike = true,
        };
        var metadata = new AttributeMetadata(
            Name: "Example",
            FullName: "Sample.Example",
            Arguments: [new AttributeArgumentMetadata(Name: null, Value: null) { Type = reference, TypeValue = reference }])
        {
            Type = reference,
        };
        var factory = new TemplateCodeModelAdapterFactory(metadata: AdapterTestData.CreateProject(types: []), settings: settings);
        factory.TryAdapt(context: metadata, targetType: typeof(CodeAttribute), adapted: out var adapted).Should().BeTrue();
        var attribute = adapted.Should().BeOfType<CodeAttribute>().Which;
        var deferred = typeof(CodeAttribute).GetField(name: "_typeFactory", bindingAttr: BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(obj: attribute) as Lazy<CodeType?>;
        if (customSettings)
        {
            deferred.Should().BeNull();
            ((CustomSettings)settings).Reads.Should().BePositive();
        }
        else
        {
            deferred.Should().NotBeNull();
            deferred!.IsValueCreated.Should().BeFalse();
        }

        settings.UseDateType(dateType: "After");
        attribute.Type!.Name.Should().Be("Before");
        attribute.Type.Should().BeSameAs(attribute.Type);
        var argument = attribute.Arguments.Single();
        argument.Type!.Name.Should().Be("Before");
        argument.TypeValue!.Name.Should().Be("Before");
        argument.Type.Should().BeSameAs(argument.Type).And.NotBeSameAs(argument.TypeValue);
        argument.Parent.Should().BeSameAs(attribute);
    }

    private static PropertyInfo[] GetDefaultProperties(System.Type modelType) =>
        modelType.GetProperties().Where(predicate: property =>
            typeof(Typewriter.CodeModel.IFilterable).IsAssignableFrom(c: property.PropertyType) || property.Name == "Type").ToArray();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference AdaptAndRelease(TemplateCodeModelAdapterFactory factory, TypeMetadata declaration)
    {
        factory.TryAdapt(context: declaration, targetType: typeof(CodeClass), adapted: out var adapted).Should().BeTrue();
        factory.GetFilterContext(item: adapted!).Should().BeSameAs(declaration);
        return new WeakReference(target: adapted);
    }

    private static FieldInfo GetDefaultField(System.Type modelType, PropertyInfo property) =>
        modelType.GetField(
            name: "_" + char.ToLowerInvariant(c: property.Name[0]) + property.Name[1..],
            bindingAttr: BindingFlags.Instance | BindingFlags.NonPublic)!;

    private sealed class CustomSettings : Settings
    {
        public int Reads { get; private set; }

        public override string DateTypeGeneration
        {
            get
            {
                Reads++;
                return base.DateTypeGeneration;
            }
        }
    }
}
