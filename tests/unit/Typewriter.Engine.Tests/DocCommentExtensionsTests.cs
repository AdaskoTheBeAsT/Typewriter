using Typewriter.CodeModel;
using Typewriter.Extensions.Documentation;
using Xunit;

namespace Typewriter.Engine.Tests;

public sealed class DocCommentExtensionsTests
{
    [Fact]
    public void ToJsDocSummaryConvertsSeeCrefToLink()
    {
        var docComment = new DocComment
        {
            Summary = "Defines the types of a <see cref=\"T:Sample.GeometryFieldType\" />.",
        };

        docComment.ToJsDocSummary().Should().Be("Defines the types of a {@link GeometryFieldType}.");
    }

    [Fact]
    public void ToJsDocSummaryConvertsInlineForms()
    {
        var docComment = new DocComment
        {
            Summary = "Use <c>map</c> when value is <see langword=\"null\"/>; see <see href=\"https://example.com\">docs</see> and <paramref name=\"value\"/>.",
        };

        docComment.ToJsDocSummary().Should().Be("Use `map` when value is `null`; see {@link https://example.com | docs} and `value`.");
    }

    [Fact]
    public void ToJsDocReturnsConvertsSeeCref()
    {
        var docComment = new DocComment
        {
            Returns = "The <see cref=\"T:Sample.GeometryFieldType\"/> instance.",
        };

        docComment.ToJsDocReturns().Should().Be("The {@link GeometryFieldType} instance.");
    }

    [Fact]
    public void ToJsDocBuildsBlockWithSummaryParamsAndReturns()
    {
        var docComment = new DocComment
        {
            Summary = "Echoes a <see cref=\"T:Sample.GeometryFieldType\"/>.",
            Returns = "The echoed value.",
            Parameters = new ParameterCommentCollection(
            [
                new ParameterComment { Name = "value", Description = "The <paramref name=\"value\"/> to echo." },
            ]),
        };

        var jsDoc = docComment.ToJsDoc();

        jsDoc.Should().Be(
            "/**\n"
            + " * Echoes a {@link GeometryFieldType}.\n"
            + " * @param value The `value` to echo.\n"
            + " * @returns The echoed value.\n"
            + " */");
    }

    [Fact]
    public void ToJsDocReturnsEmptyWhenNoContent()
    {
        var docComment = new DocComment();

        docComment.ToJsDoc().Should().BeEmpty();
    }
}
