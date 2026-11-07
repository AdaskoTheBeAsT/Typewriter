using AwesomeAssertions;
using Typewriter.Abstractions;
using Xunit;

namespace Typewriter.Engine.Tests;

public sealed class TemplateSyntaxIndexTests
{
    [Fact]
    public void NestedLiteralCodeMarkersDoNotCopyEveryEnclosingBody()
    {
        const int count = 2000;
        var content = string.Concat(values: Enumerable.Repeat(element: "${foo", count: count)) + new string(c: '}', count: count);
        var diagnostics = new List<GenerationDiagnostic>();
        var file = new TemplateFile(Path: "literal.tst", Content: content);
        var before = GC.GetAllocatedBytesForCurrentThread();

        var template = TemplateDocument.Parse(template: file, diagnostics: diagnostics);

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        template.Content.Should().Be(content + "\n");
        template.CodeBlocks.Should().BeEmpty();
        diagnostics.Should().BeEmpty();
        allocated.Should().BeLessThan(expected: content.Length * 128L);
    }

    [Fact]
    public void NestedScalarParenthesesAreIndexedWithoutRecursion()
    {
        var content = string.Concat(values: Enumerable.Repeat(element: "$Name(", count: 6000)) + "$Parameters[$Name]" + new string(c: ')', count: 6000);
        var source = new TemplateSourceIndex(content: content);

        source.FindBlockEnd(openIndex: content.IndexOf(value: '[')).Should().Be(content.LastIndexOf(value: ']'));
    }

    [Theory]
    [InlineData(-1, 1, 1)]
    [InlineData(0, 1, 1)]
    [InlineData(1, 1, 2)]
    [InlineData(2, 1, 3)]
    [InlineData(3, 2, 1)]
    [InlineData(5, 2, 3)]
    [InlineData(6, 3, 1)]
    [InlineData(7, 3, 2)]
    [InlineData(100, 3, 2)]
    public void LineMapHandlesCrLfAndBoundaryPositions(int index, int line, int column)
    {
        var lines = new TemplateLineMap(content: "a\r\nbb\nc");

        lines.GetPosition(index: index).Should().Be((line, column));
    }

    [Fact]
    public void SliceReadsOnlyItsOwnIdentifier()
    {
        var slice = new TemplateSlice(Source: new TemplateSourceIndex(content: "abcdef"), Start: 1, Length: 3);

        slice.ReadIdentifier(start: 0).Should().Be("bcd");
    }

    [Fact]
    public void CompatibilityScanningDoesNotBacktrackOverLongInvalidParameters()
    {
        var diagnostics = new List<GenerationDiagnostic>();
        var file = new TemplateFile(Path: "invalid.tst", Content: "${\nstring Select(" + new string(c: 'A', count: 10000) + "\n}\n");

        var template = TemplateDocument.Parse(template: file, diagnostics: diagnostics);

        template.CompatibilityMethods.Should().BeEmpty();
    }
}
