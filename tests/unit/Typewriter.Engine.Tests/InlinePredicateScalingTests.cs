using AwesomeAssertions;
using Typewriter.Abstractions;
using Xunit;

namespace Typewriter.Engine.Tests;

public sealed class InlinePredicateScalingTests
{
    [Theory]
    [InlineData("$Classes[$Methods[$Name($Parameters[$Name][, ])]]", "Run(value)")]
    [InlineData("$Classes[$Methods[$Name(\"$Parameters[$Name][, ]\")]]", "Run(\"value\")")]
    [InlineData("$Classes[$Methods[$Name($Parameters(p => p.Name != \"]\")[$Name][, ])]]", "Run(value)")]
    public void ScalarParenthesesPreserveNestedCollectionBlocks(string content, string expected)
    {
        var diagnostics = new List<GenerationDiagnostic>();
        var template = new TemplateDocument(Path: "method.tst", Content: content, OutputPath: null);
        var renderer = new TemplateRenderer(typeMapper: new TypeScriptTypeMapper());

        var output = renderer.Render(template: template, metadata: new ProjectMetadata(ProjectPath: "Sample.csproj", SourceFiles: [], Types: [CreateMethodOwner()], Diagnostics: []), diagnostics: diagnostics);

        output.Should().Be(expected);
        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void MalformedCSharpOpenersAreIndexedWithoutRepeatedSuffixScans()
    {
        var source = new TemplateSourceIndex(content: string.Concat(values: Enumerable.Repeat(element: "$Classes(", count: 4000)) + "$Enums(e => true)[$Name]");

        source.FindBlockEnd(openIndex: "$Classes".Length).Should().Be(-1);
        source.FindBlockEnd(openIndex: source.Content.LastIndexOf(value: '(')).Should().BePositive();
    }

    [Fact]
    public void ScannerDoesNotUseTheCallStackForNestedTemplateBlocks()
    {
        var content = string.Concat(values: Enumerable.Repeat(element: "$Classes[", count: 20000)) + "$Properties(p => true)[$Name]" + new string(c: ']', count: 20000);

        var predicates = TemplateInlinePredicateScanner.Find(content: content);

        predicates.Should().ContainSingle().Which.CollectionName.Should().Be("Properties");
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void ScannerKeepsLocationsAcrossManyPredicates(string lineEnding)
    {
        var content = string.Join(separator: lineEnding, values: Enumerable.Repeat(element: "$Classes(c => true)[$Name;]", count: 2000));

        var predicates = TemplateInlinePredicateScanner.Find(content: content);

        predicates.Should().HaveCount(2000);
        predicates[^1].Line.Should().Be(2000);
        predicates[^1].Column.Should().Be(10);
        predicates.Select(selector: predicate => predicate.MethodName).Distinct(StringComparer.Ordinal).Should().ContainSingle();
    }

    [Fact]
    public void DeeplyNestedRenderingDoesNotCopyEveryEnclosingBlock()
    {
        const int depth = 600;
        var template = new TemplateDocument(
            Path: "nested.tst",
            Content: "$Classes[" + string.Concat(values: Enumerable.Repeat(element: "$IsAbstract[", count: depth)) + "done" + new string(c: ']', count: depth + 1),
            OutputPath: null);
        var type = new TypeMetadata(
            Name: "Sample",
            FullName: "Sample",
            Namespace: string.Empty,
            Kind: TypeMetadataKind.Class,
            Accessibility: MetadataAccessibility.Public,
            Properties: [],
            Attributes: [],
            BaseTypes: [],
            EnumValues: [],
            IsNullableAware: true)
        {
            IsAbstract = true,
        };
        var metadata = new ProjectMetadata(ProjectPath: "Sample.csproj", SourceFiles: [], Types: [type], Diagnostics: []);
        var diagnostics = new List<GenerationDiagnostic>();
        var renderer = new TemplateRenderer(typeMapper: new TypeScriptTypeMapper());
        var before = GC.GetAllocatedBytesForCurrentThread();

        var output = renderer.Render(template: template, metadata: metadata, diagnostics: diagnostics);

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        output.Should().Be("done");
        diagnostics.Should().BeEmpty();
        allocated.Should().BeLessThan(expected: template.Content.Length * 256L, because: "nested bodies must share their source text and output buffer");
    }

    [Fact]
    public void NestedDiagnosticsUseThePositionInTheWholeTemplate()
    {
        var template = new TemplateDocument(Path: "nested.tst", Content: "$Classes[\n  $MissingMember\n]", OutputPath: null);
        var type = new TypeMetadata(
            Name: "Sample",
            FullName: "Sample",
            Namespace: string.Empty,
            Kind: TypeMetadataKind.Class,
            Accessibility: MetadataAccessibility.Public,
            Properties: [],
            Attributes: [],
            BaseTypes: [],
            EnumValues: [],
            IsNullableAware: true);
        var diagnostics = new List<GenerationDiagnostic>();
        var renderer = new TemplateRenderer(typeMapper: new TypeScriptTypeMapper());

        _ = renderer.Render(template: template, metadata: new ProjectMetadata(ProjectPath: "Sample.csproj", SourceFiles: [], Types: [type], Diagnostics: []), diagnostics: diagnostics);

        var diagnostic = diagnostics.Should().ContainSingle().Which;
        diagnostic.Line.Should().Be(2);
        diagnostic.Column.Should().Be(3);
    }

    [Fact]
    public void CachedSyntaxDoesNotChangeRecordEqualityOrSurviveAContentClone()
    {
        var template = new TemplateDocument(Path: "one.tst", Content: "$Classes[$Name]", OutputPath: null);
        var hashCode = template.GetHashCode();
        var first = template.SourceIndex;

        var changed = template with { Content = "$Enums[$Name]" };

        template.SourceIndex.Should().BeSameAs(first);
        template.GetHashCode().Should().Be(hashCode);
        changed.SourceIndex.Should().NotBeSameAs(first);
        changed.SourceIndex.Content.Should().Be(changed.Content);
    }

    private static TypeMetadata CreateMethodOwner()
    {
        var reference = new TypeMetadataReference(
            Name: "Int32",
            FullName: "System.Int32",
            Namespace: "System",
            IsNullable: false,
            IsCollection: false,
            IsDictionary: false,
            IsEnum: false,
            IsPrimitive: true,
            IsDateLike: false,
            ElementType: null,
            TypeArguments: []);
        return new TypeMetadata(
            Name: "Sample",
            FullName: "Sample",
            Namespace: string.Empty,
            Kind: TypeMetadataKind.Class,
            Accessibility: MetadataAccessibility.Public,
            Properties: [],
            Attributes: [],
            BaseTypes: [],
            EnumValues: [],
            IsNullableAware: true)
        {
            Methods =
            [
                new(
                    Name: "Run",
                    FullName: "Sample.Run",
                    ReturnType: reference,
                    Accessibility: MetadataAccessibility.Public,
                    IsStatic: false,
                    IsAbstract: false,
                    IsGeneric: false,
                    Parameters: [new(Name: "value", FullName: "Sample.Run.value", Type: reference, HasDefaultValue: false, DefaultValue: null, Attributes: [], ParentMethodFullName: "Sample.Run")],
                    Attributes: [],
                    ParentTypeFullName: "Sample"),
            ],
        };
    }
}
