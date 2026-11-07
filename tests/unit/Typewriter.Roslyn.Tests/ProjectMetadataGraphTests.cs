using System.Collections;
using System.Globalization;
using AwesomeAssertions;
using Typewriter.Abstractions;
using Xunit;

namespace Typewriter.Roslyn.Tests;

public sealed class ProjectMetadataGraphTests
{
    [Fact]
    public void ChainMaterializesEachLocalPayloadOnlyAtTheRequestedRoot()
    {
        const int ProjectCount = 1024;
        var payloads = new List<CountingList<TypeMetadata>>();
        ProjectMetadataGraph? root = null;
        for (var index = 0; index < ProjectCount; index++)
        {
            var payload = new CountingList<TypeMetadata>(items: [CreateType(name: "Type" + index.ToString(provider: CultureInfo.InvariantCulture))]);
            payloads.Add(item: payload);
            root = CreateGraph(name: "Project" + index.ToString(provider: CultureInfo.InvariantCulture), types: payload, references: root is null ? [] : [root]);
        }

        payloads.Should().OnlyContain(predicate: payload => payload.ItemsRead == 0);
        root.Should().NotBeNull();
        var metadata = root.GetMetadata(cancellationToken: CancellationToken.None);

        metadata.Types.Should().HaveCount(expected: ProjectCount);
        payloads.Should().OnlyContain(predicate: payload => payload.ItemsRead == 1);
        root.GetMetadata(cancellationToken: CancellationToken.None).Should().BeSameAs(expected: metadata);
        payloads.Should().OnlyContain(predicate: payload => payload.ItemsRead == 1);
    }

    [Fact]
    public void LayeredDiamondsVisitSharedPayloadsOnceInsteadOfFollowingEveryPath()
    {
        const int LayerCount = 24;
        var payloads = new List<CountingList<TypeMetadata>>();
        IReadOnlyList<ProjectMetadataGraph> previous = [];
        for (var layer = 0; layer < LayerCount; layer++)
        {
            var next = new List<ProjectMetadataGraph>();
            for (var branch = 0; branch < 2; branch++)
            {
                var name = "Layer" + layer.ToString(provider: CultureInfo.InvariantCulture) + "Branch" + branch.ToString(provider: CultureInfo.InvariantCulture);
                var payload = new CountingList<TypeMetadata>(items: [CreateType(name: name)]);
                payloads.Add(item: payload);
                next.Add(item: CreateGraph(name: name, types: payload, references: previous));
            }

            previous = next;
        }

        var root = CreateGraph(name: "Root", types: [], references: previous);
        var metadata = root.GetMetadata(cancellationToken: CancellationToken.None);

        metadata.Types.Should().HaveCount(expected: LayerCount * 2);
        metadata.Diagnostics.Should().BeEmpty();
        payloads.Should().OnlyContain(predicate: payload => payload.ItemsRead == 1);
    }

    [Fact]
    public void MergePreservesFirstOccurrenceAssemblyIdentityOrderingAndDiagnosticMultiplicity()
    {
        var sharedType = CreateType(name: "Same");
        var otherAssemblyType = sharedType with { AssemblyName = "Other" };
        var rootType = sharedType with { Documentation = "Root wins" };
        var sharedPath = Path.Combine(path1: Path.GetTempPath(), path2: "Typewriter.Graph.Shared.cs");
        var leafFile = new SourceFileMetadata(Path: sharedPath, Types: [sharedType]);
        var rootFile = new SourceFileMetadata(Path: sharedPath, Types: [rootType]);
        var leafDiagnostic = CreateDiagnostic(message: "Leaf");
        var leaf = CreateGraph(name: "Leaf", types: [sharedType, otherAssemblyType], references: [], sourceFiles: [leafFile], diagnostics: [leafDiagnostic]);
        var left = CreateGraph(name: "Left", types: [CreateType(name: "Zed")], references: [leaf], diagnostics: [CreateDiagnostic(message: "Left")]);
        var right = CreateGraph(name: "Right", types: [CreateType(name: "Alpha")], references: [leaf], diagnostics: [CreateDiagnostic(message: "Right")]);
        var root = CreateGraph(name: "Root", types: [rootType], references: [left, right], sourceFiles: [rootFile], diagnostics: [CreateDiagnostic(message: "Root")]);

        var metadata = root.GetMetadata(cancellationToken: CancellationToken.None);

        metadata.SourceFiles.Should().ContainSingle().Which.Should().BeSameAs(expected: rootFile);
        metadata.Types.Select(selector: type => (type.Name, type.AssemblyName)).Should().Equal(
            ("Alpha", "Assembly"),
            ("Same", "Assembly"),
            ("Same", "Other"),
            ("Zed", "Assembly"));
        metadata.Types[1].Should().BeSameAs(expected: rootType);
        metadata.Diagnostics.Select(selector: diagnostic => diagnostic.Message).Should().Equal("Root", "Left", "Leaf", "Right", "Leaf");
        metadata.Diagnostics[2].Should().BeSameAs(expected: leafDiagnostic);
        metadata.Diagnostics[4].Should().BeSameAs(expected: leafDiagnostic);
    }

    [Fact]
    public void CancellationDoesNotPublishAnIncompleteMergedView()
    {
        var leaf = CreateGraph(name: "Leaf", types: [CreateType(name: "Leaf")], references: []);
        var root = CreateGraph(name: "Root", types: [CreateType(name: "Root")], references: [leaf]);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Action act = () => root.GetMetadata(cancellationToken: cancellation.Token);

        act.Should().Throw<OperationCanceledException>();
        root.GetMetadata(cancellationToken: CancellationToken.None).Types.Should().HaveCount(expected: 2);
    }

    private static ProjectMetadataGraph CreateGraph(
        string name,
        IReadOnlyList<TypeMetadata> types,
        IReadOnlyList<ProjectMetadataGraph> references,
        IReadOnlyList<SourceFileMetadata>? sourceFiles = null,
        IReadOnlyList<GenerationDiagnostic>? diagnostics = null) =>
        new(
            localMetadata: new ProjectMetadata(
                ProjectPath: Path.Combine(path1: Path.GetTempPath(), path2: name + ".csproj"),
                SourceFiles: sourceFiles ?? [],
                Types: types,
                Diagnostics: diagnostics ?? []),
            references: references);

    private static TypeMetadata CreateType(string name) =>
        new(
            Name: name,
            FullName: "Scaling." + name,
            Namespace: "Scaling",
            Kind: TypeMetadataKind.Class,
            Accessibility: MetadataAccessibility.Public,
            Properties: [],
            Attributes: [],
            BaseTypes: [],
            EnumValues: [],
            IsNullableAware: true)
        {
            AssemblyName = "Assembly",
        };

    private static GenerationDiagnostic CreateDiagnostic(string message) =>
        new(
            File: null,
            Line: null,
            Column: null,
            Severity: DiagnosticSeverity.Warning,
            Message: message);

    private sealed class CountingList<T>(IReadOnlyList<T> items) : IReadOnlyList<T>
    {
        public int ItemsRead { get; private set; }

        public int Count => items.Count;

        public T this[int index]
        {
            get
            {
                ItemsRead++;
                return items[index];
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            for (var index = 0; index < items.Count; index++)
            {
                yield return this[index];
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
