using Typewriter.Engine;
using Xunit;

namespace Typewriter.Engine.Tests;

public sealed class UnifiedDiffBuilderTests
{
    [Fact]
    public void BuildReturnsEmptyForIdenticalContent()
    {
        const string content = "line1\nline2\nline3";

        var diff = UnifiedDiffBuilder.Build(path: "file.ts", oldContent: content, newContent: content);

        diff.Should().BeEmpty();
    }

    [Fact]
    public void BuildReturnsAllInsertsForNewFile()
    {
        var diff = UnifiedDiffBuilder.Build(path: "file.ts", oldContent: string.Empty, newContent: "line1\nline2\nline3");

        diff.Should().Contain("--- file.ts");
        diff.Should().Contain("+++ file.ts");
        diff.Should().Contain("@@ -0,0 +1,3 @@");
        diff.Should().Contain("+line1");
        diff.Should().Contain("+line2");
        diff.Should().Contain("+line3");
    }

    [Fact]
    public void BuildReturnsAllDeletesForDeletedFile()
    {
        var diff = UnifiedDiffBuilder.Build(path: "file.ts", oldContent: "line1\nline2\nline3", newContent: string.Empty);

        diff.Should().Contain("@@ -1,3 +0,0 @@");
        diff.Should().Contain("-line1");
        diff.Should().Contain("-line2");
        diff.Should().Contain("-line3");
    }

    [Fact]
    public void BuildReturnsDiffForSingleLineChange()
    {
        var diff = UnifiedDiffBuilder.Build(
            path: "file.ts",
            oldContent: "line1\nold\nline3",
            newContent: "line1\nnew\nline3");

        diff.Should().Contain("--- file.ts");
        diff.Should().Contain("+++ file.ts");
        diff.Should().Contain("@@ -1,3 +1,3 @@");
        diff.Should().Contain(" line1");
        diff.Should().Contain("-old");
        diff.Should().Contain("+new");
        diff.Should().Contain(" line3");
    }

    [Fact]
    public void BuildReturnsDiffForInsertedLine()
    {
        var diff = UnifiedDiffBuilder.Build(
            path: "file.ts",
            oldContent: "line1\nline3",
            newContent: "line1\nline2\nline3");

        diff.Should().Contain("@@ -1,2 +1,3 @@");
        diff.Should().Contain(" line1");
        diff.Should().Contain("+line2");
        diff.Should().Contain(" line3");
    }

    [Fact]
    public void BuildReturnsDiffForDeletedLine()
    {
        var diff = UnifiedDiffBuilder.Build(
            path: "file.ts",
            oldContent: "line1\nline2\nline3",
            newContent: "line1\nline3");

        diff.Should().Contain("@@ -1,3 +1,2 @@");
        diff.Should().Contain(" line1");
        diff.Should().Contain("-line2");
        diff.Should().Contain(" line3");
    }

    [Fact]
    public void BuildHandlesCrlfLineEndings()
    {
        var diff = UnifiedDiffBuilder.Build(
            path: "file.ts",
            oldContent: "line1\r\nold\r\nline3\r\n",
            newContent: "line1\r\nnew\r\nline3\r\n");

        diff.Should().Contain("-old");
        diff.Should().Contain("+new");
        diff.Should().NotContain("\r");
    }

    [Fact]
    public void BuildReturnsDiffForLineEndingOnlyChange()
    {
        var diff = UnifiedDiffBuilder.Build(
            path: "file.ts",
            oldContent: "line1\r\nline2\r\n",
            newContent: "line1\nline2\n");

        diff.Should().Contain("--- file.ts");
        diff.Should().Contain("+++ file.ts");
        diff.Should().Contain("@@ -1,2 +1,2 @@");
        diff.Should().Contain("-line1");
        diff.Should().Contain("+line1");
        diff.Should().NotContain("\r");
    }

    [Fact]
    public void BuildReturnsDiffForFinalNewlineOnlyChange()
    {
        var diff = UnifiedDiffBuilder.Build(
            path: "file.ts",
            oldContent: "line1",
            newContent: "line1\n");

        diff.Should().Contain("-line1");
        diff.Should().Contain("+line1");
        diff.Should().Contain("\\ No newline at end of file");
    }

    [Fact]
    public void BuildMergesAdjacentHunks()
    {
        const string oldContent = "ctx1\nctx2\nctx3\nold1\nctx4\nold2\nctx5\nctx6\nctx7";
        const string newContent = "ctx1\nctx2\nctx3\nnew1\nctx4\nnew2\nctx5\nctx6\nctx7";

        var diff = UnifiedDiffBuilder.Build(path: "file.ts", oldContent: oldContent, newContent: newContent);

        var hunkCount = CountOccurrences(text: diff, value: "@@ -");
        hunkCount.Should().Be(1, because: "adjacent changes within 3 context lines should be merged into a single hunk");
    }

    [Fact]
    public void BuildSeparatesDistantHunks()
    {
        const string oldContent = "old1\na\nb\nc\nd\ne\nf\ng\nh\nold2";
        const string newContent = "new1\na\nb\nc\nd\ne\nf\ng\nh\nnew2";

        var diff = UnifiedDiffBuilder.Build(path: "file.ts", oldContent: oldContent, newContent: newContent);

        var hunkCount = CountOccurrences(text: diff, value: "@@ -");
        hunkCount.Should().Be(2, because: "changes separated by more than 6 context lines should produce two hunks");
    }

    [Fact]
    public void BuildReturnsDiffForMultiLineReplacement()
    {
        var diff = UnifiedDiffBuilder.Build(
            path: "file.ts",
            oldContent: "export class Old {\n    prop: string;\n}",
            newContent: "export interface New {\n    prop: number;\n    extra: boolean;\n}");

        diff.Should().Contain("-export class Old {");
        diff.Should().Contain("-    prop: string;");
        diff.Should().Contain("+export interface New {");
        diff.Should().Contain("+    prop: number;");
        diff.Should().Contain("+    extra: boolean;");
        diff.Should().Contain(" }");
    }

    private static int CountOccurrences(
        string text,
        string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value: value, startIndex: index, comparisonType: StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }
}
