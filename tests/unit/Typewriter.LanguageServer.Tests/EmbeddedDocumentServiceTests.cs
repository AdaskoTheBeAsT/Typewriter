using Xunit;

namespace Typewriter.LanguageServer.Tests;

public sealed class EmbeddedDocumentServiceTests
{
    private const string Template =
        "${\n"
        + "    string Suffix(Typewriter.CodeModel.Class c) => c.Name;\n"
        + "}\n"
        + "export const url = `${environment.apiBaseUrl}/api`;\n"
        + "export interface $Name {\n"
        + "}\n";

    [Fact]
    public void GetSnapshotForTypeScriptBlanksCSharpBlocksAndPreservesInterpolation()
    {
        var document = CreateDocument(text: Template);

        var snapshot = EmbeddedDocumentService.GetSnapshot(document: document, kind: "typescript");

        snapshot.Should().NotBeNull();
        snapshot!.LanguageId.Should().Be("typescript");
        snapshot.Content.Should().HaveLength(Template.Length);
        snapshot.Content.Should().NotContain("Suffix");
        snapshot.Content.Should().Contain("`${environment.apiBaseUrl}/api`");
        snapshot.Content.Should().Contain("export interface $Name {");
        snapshot.Version.Should().Be(3);
    }

    [Fact]
    public void GetSnapshotForCSharpProjectsHelperBlockIntoHostDocument()
    {
        var document = CreateDocument(text: Template);

        var snapshot = EmbeddedDocumentService.GetSnapshot(document: document, kind: "csharp");

        snapshot.Should().NotBeNull();
        snapshot!.LanguageId.Should().Be("csharp");
        snapshot.Content.Should().Contain("string Suffix(Typewriter.CodeModel.Class c) => c.Name;");
        snapshot.Content.Should().Contain("class TypewriterTemplateHost");
        snapshot.Content.Should().NotContain("export const url");
    }

    [Fact]
    public void GetSnapshotForCSharpReturnsNullWithoutHelperBlocks()
    {
        var document = CreateDocument(text: "export interface $Name {\n}\n");

        var snapshot = EmbeddedDocumentService.GetSnapshot(document: document, kind: "csharp");

        snapshot.Should().BeNull();
    }

    [Fact]
    public void GetSnapshotForUnknownKindReturnsNull()
    {
        var document = CreateDocument(text: Template);

        var snapshot = EmbeddedDocumentService.GetSnapshot(document: document, kind: "template");

        snapshot.Should().BeNull();
    }

    [Fact]
    public void GetPositionInfoMapsCSharpPositionToVirtualDocument()
    {
        var document = CreateDocument(text: Template);
        var templatePosition = TextPositions.GetPosition(text: Template, offset: Template.IndexOf(value: "Suffix", comparisonType: StringComparison.Ordinal));

        var info = EmbeddedDocumentService.GetPositionInfo(document: document, position: templatePosition);

        info.Kind.Should().Be("csharp");
        info.VirtualPosition.Should().NotBeNull();
        var virtualSource = EmbeddedDocumentService.GetSnapshot(document: document, kind: "csharp")!.Content;
        var virtualOffset = TextPositions.GetOffset(text: virtualSource, position: info.VirtualPosition!);
        virtualSource.Substring(startIndex: virtualOffset, length: 6).Should().Be("Suffix");
    }

    [Fact]
    public void GetPositionInfoReturnsIdentityPositionForTypeScript()
    {
        var document = CreateDocument(text: Template);
        var templatePosition = TextPositions.GetPosition(text: Template, offset: Template.IndexOf(value: "export const", comparisonType: StringComparison.Ordinal));

        var info = EmbeddedDocumentService.GetPositionInfo(document: document, position: templatePosition);

        info.Kind.Should().Be("typescript");
        info.VirtualPosition.Should().Be(templatePosition);
    }

    [Fact]
    public void GetPositionInfoReturnsTemplateKindForTemplateToken()
    {
        var document = CreateDocument(text: Template);
        var templatePosition = TextPositions.GetPosition(text: Template, offset: Template.IndexOf(value: "$Name", comparisonType: StringComparison.Ordinal) + 1);

        var info = EmbeddedDocumentService.GetPositionInfo(document: document, position: templatePosition);

        info.Kind.Should().Be("template");
        info.VirtualPosition.Should().BeNull();
    }

    [Fact]
    public void MapRangeToTemplateRoundTripsCSharpRange()
    {
        var document = CreateDocument(text: Template);
        var templateStartOffset = Template.IndexOf(value: "Suffix", comparisonType: StringComparison.Ordinal);
        var templateStart = TextPositions.GetPosition(text: Template, offset: templateStartOffset);
        var templateEnd = TextPositions.GetPosition(text: Template, offset: templateStartOffset + 6);
        var virtualStart = EmbeddedDocumentService.GetPositionInfo(document: document, position: templateStart).VirtualPosition!;
        var virtualEnd = EmbeddedDocumentService.GetPositionInfo(document: document, position: templateEnd).VirtualPosition!;

        var mapped = EmbeddedDocumentService.MapRangeToTemplate(
            document: document,
            kind: "csharp",
            range: new LspRange(Start: virtualStart, End: virtualEnd));

        mapped.Should().NotBeNull();
        mapped!.Start.Should().Be(templateStart);
        mapped.End.Should().Be(templateEnd);
    }

    [Fact]
    public void MapRangeToTemplateReturnsSameRangeForTypeScript()
    {
        var document = CreateDocument(text: Template);
        var range = new LspRange(
            Start: new LspPosition(Line: 3, Character: 0),
            End: new LspPosition(Line: 3, Character: 6));

        var mapped = EmbeddedDocumentService.MapRangeToTemplate(document: document, kind: "typescript", range: range);

        mapped.Should().Be(range);
    }

    [Fact]
    public void MapRangeToTemplateReturnsNullForUnknownKind()
    {
        var document = CreateDocument(text: Template);
        var range = new LspRange(
            Start: new LspPosition(Line: 0, Character: 0),
            End: new LspPosition(Line: 0, Character: 1));

        var mapped = EmbeddedDocumentService.MapRangeToTemplate(document: document, kind: "template", range: range);

        mapped.Should().BeNull();
    }

    private static TextDocumentState CreateDocument(string text) =>
        new(
            Uri: "file:///c%3A/templates/Models.tst",
            Path: @"c:\templates\Models.tst",
            Text: text,
            Version: 3);
}
