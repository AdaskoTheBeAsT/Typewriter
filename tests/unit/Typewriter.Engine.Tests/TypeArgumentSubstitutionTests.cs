using AwesomeAssertions;
using Typewriter.Abstractions;
using Xunit;
using CodeType = Typewriter.CodeModel.Type;

namespace Typewriter.Engine.Tests;

public sealed class TypeArgumentSubstitutionTests
{
    [Fact]
    public void NestedGenericInterfaceSubstitutionVisitsSharedCollectionNodesOnce()
    {
        const int Depth = 64;
        var argumentLists = new List<AdapterCountingList<TypeMetadataReference>>();
        var nested = AdapterTestData.CreateReference(name: "T", fullName: "T") with { IsPrimitive = false };
        for (var level = 0; level < Depth; level++)
        {
            var arguments = new AdapterCountingList<TypeMetadataReference>(items: [nested], maximumItemReads: 4);
            argumentLists.Add(item: arguments);
            nested = AdapterTestData.CreateReference(name: "List", fullName: "System.Collections.Generic.List") with
            {
                IsPrimitive = false,
                IsCollection = true,
                ElementType = nested,
                TypeArguments = arguments,
            };
        }

        var parent = AdapterTestData.CreateType(name: "IParent") with
        {
            Kind = TypeMetadataKind.Interface,
            TypeParameters = [new TypeParameterMetadata(Name: "T")],
        };
        var parentArguments = new AdapterCountingList<TypeMetadataReference>(items: [nested], maximumItemReads: 4);
        argumentLists.Add(item: parentArguments);
        var child = AdapterTestData.CreateType(name: "IChild") with
        {
            Kind = TypeMetadataKind.Interface,
            TypeParameters = [new TypeParameterMetadata(Name: "T")],
            BaseTypes =
            [
                AdapterTestData.CreateReference(name: parent.Name, fullName: parent.FullName) with
                {
                    IsPrimitive = false,
                    TypeArguments = parentArguments,
                },
            ],
        };
        var factory = new TemplateCodeModelAdapterFactory(
            metadata: AdapterTestData.CreateProject(types: [parent, child]),
            templatePath: "models.tst");
        var reference = AdapterTestData.CreateReference(name: child.Name, fullName: child.FullName) with
        {
            IsPrimitive = false,
            TypeArguments = [AdapterTestData.CreateReference()],
        };
        factory.TryAdapt(context: reference, targetType: typeof(CodeType), adapted: out var adapted).Should().BeTrue();
        var type = adapted.Should().BeAssignableTo<CodeType>().Which;

        type.Interfaces.Count.Should().Be(1);

        var current = type.Interfaces.Single().TypeArguments.Single();
        for (var level = 0; level < Depth; level++)
        {
            current = current.ElementType ?? throw new InvalidOperationException(message: "Missing substituted collection element.");
        }

        current.Name.Should().Be("string");
        argumentLists.Sum(selector: list => list.ItemReads).Should().BeLessThanOrEqualTo(2 * (Depth + 1));
    }

    [Fact]
    public void SubstitutionPreservesNullableSharedNodesIdentityAndLazyInterfaceCycles()
    {
        var parameter = AdapterTestData.CreateReference(name: "T", fullName: "T") with { IsNullable = true };
        var replacement = AdapterTestData.CreateReference();
        var containing = AdapterTestData.CreateReference(name: "Owner", fullName: "Sample.Owner");
        var interfaceItems = new List<TypeMetadataReference>();
        var interfaces = new AdapterCountingList<TypeMetadataReference>(items: interfaceItems);
        var target = AdapterTestData.CreateReference(name: "Container", fullName: "Sample.Owner.Container") with
        {
            AssemblyName = "Contracts",
            Arity = 3,
            ContainingType = containing,
            TypeArguments = [parameter],
            ElementType = parameter,
            Interfaces = interfaces,
        };
        interfaceItems.Add(item: target);
        var substitution = new TypeArgumentSubstitution(
            substitutions: new Dictionary<string, TypeMetadataReference>(comparer: StringComparer.Ordinal) { ["T"] = replacement });

        var result = substitution.Substitute(target: target);

        interfaces.Enumerations.Should().Be(0);
        result.Should().NotBeSameAs(target);
        result.Arity.Should().Be(3);
        result.AssemblyName.Should().Be("Contracts");
        result.ContainingType.Should().BeSameAs(containing);
        result.TypeArguments[0].Should().BeSameAs(result.ElementType);
        var element = result.ElementType.Should().BeOfType<TypeMetadataReference>().Which;
        element.IsNullable.Should().BeTrue();
        element.FullName.Should().Be("System.String");
        replacement.IsNullable.Should().BeFalse();
        result.Interfaces.Single().Should().BeSameAs(result);
        interfaces.Enumerations.Should().Be(1);
        substitution.Substitute(target: target).Should().BeSameAs(result);
        substitution.Substitute(target: containing).Should().BeSameAs(containing);
    }

    [Fact]
    public void ParallelLazyInterfaceSubstitutionUsesTheSameCompletedResults()
    {
        var parameter = AdapterTestData.CreateReference(name: "T", fullName: "T");
        var shared = AdapterTestData.CreateReference(name: "List", fullName: "System.Collections.Generic.List") with
        {
            IsCollection = true,
            ElementType = parameter,
            TypeArguments = [parameter],
        };
        var inherited = AdapterTestData.CreateReference(name: "IParent", fullName: "Sample.IParent") with { TypeArguments = [shared] };
        var source = AdapterTestData.CreateReference(name: "IChild", fullName: "Sample.IChild") with
        {
            TypeArguments = [shared],
            Interfaces = [inherited],
        };
        var substitution = new TypeArgumentSubstitution(
            substitutions: new Dictionary<string, TypeMetadataReference>(comparer: StringComparer.Ordinal) { ["T"] = AdapterTestData.CreateReference() });
        var roots = Enumerable.Range(start: 0, count: 32)
            .Select(selector: _ => substitution.Substitute(target: source with { }))
            .ToArray();
        var options = new ParallelOptions { CancellationToken = TestContext.Current.CancellationToken };

        Parallel.ForEach(
            source: roots,
            parallelOptions: options,
            body: root =>
            {
                var argument = root.Interfaces.Single().TypeArguments.Single();
                argument.Should().BeSameAs(root.TypeArguments.Single());
                argument.Should().BeSameAs(roots[0].TypeArguments.Single());
                argument.TypeArguments.Single().FullName.Should().Be("System.String");
                argument.ElementType.Should().BeSameAs(argument.TypeArguments.Single());
            });
    }
}
