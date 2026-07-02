namespace Typewriter.LanguageServer;

/// <summary>
/// Builds virtual embedded-language documents for .tst templates and translates
/// positions and ranges between the template and each virtual document.
/// </summary>
internal static class EmbeddedDocumentService
{
    public const string TemplateKind = "template";
    public const string CSharpKind = "csharp";
    public const string TypeScriptKind = "typescript";

    public static EmbeddedDocumentSnapshot? GetSnapshot(
        TextDocumentState document,
        string kind)
    {
        if (IsCSharpKind(kind: kind))
        {
            var virtualDocument = EmbeddedCSharpDocument.Create(templateText: document.Text);
            return virtualDocument is null
                ? null
                : new EmbeddedDocumentSnapshot(
                    Kind: CSharpKind,
                    LanguageId: "csharp",
                    Content: virtualDocument.Source,
                    Version: document.Version);
        }

        if (IsTypeScriptKind(kind: kind))
        {
            return new EmbeddedDocumentSnapshot(
                Kind: TypeScriptKind,
                LanguageId: "typescript",
                Content: EmbeddedTypeScriptDocument.Create(templateText: document.Text),
                Version: document.Version);
        }

        return null;
    }

    public static EmbeddedPositionInfo GetPositionInfo(
        TextDocumentState document,
        LspPosition position)
    {
        var kind = TemplateEmbeddedLanguage.GetKindAt(document: document, position: position);
        if (kind == EmbeddedLanguageKind.CSharp)
        {
            var virtualDocument = EmbeddedCSharpDocument.Create(templateText: document.Text);
            if (virtualDocument is not null
                && virtualDocument.TryMapToVirtual(
                    templateOffset: document.GetOffset(position: position),
                    virtualOffset: out var virtualOffset))
            {
                return new EmbeddedPositionInfo(
                    Kind: CSharpKind,
                    VirtualPosition: TextPositions.GetPosition(text: virtualDocument.Source, offset: virtualOffset));
            }

            return new EmbeddedPositionInfo(Kind: CSharpKind, VirtualPosition: null);
        }

        return kind == EmbeddedLanguageKind.TypeScript
            ? new EmbeddedPositionInfo(Kind: TypeScriptKind, VirtualPosition: position)
            : new EmbeddedPositionInfo(Kind: TemplateKind, VirtualPosition: null);
    }

    public static LspRange? MapRangeToTemplate(
        TextDocumentState document,
        string kind,
        LspRange range)
    {
        if (IsTypeScriptKind(kind: kind))
        {
            return range;
        }

        if (!IsCSharpKind(kind: kind))
        {
            return null;
        }

        var virtualDocument = EmbeddedCSharpDocument.Create(templateText: document.Text);
        if (virtualDocument is null
            || !virtualDocument.TryMapToTemplate(
                virtualOffset: TextPositions.GetOffset(text: virtualDocument.Source, position: range.Start),
                templateOffset: out var templateStart)
            || !virtualDocument.TryMapToTemplate(
                virtualOffset: TextPositions.GetOffset(text: virtualDocument.Source, position: range.End),
                templateOffset: out var templateEnd))
        {
            return null;
        }

        return new LspRange(
            Start: TextPositions.GetPosition(text: document.Text, offset: templateStart),
            End: TextPositions.GetPosition(text: document.Text, offset: templateEnd));
    }

    private static bool IsCSharpKind(string kind) =>
        string.Equals(a: kind, b: CSharpKind, comparisonType: StringComparison.OrdinalIgnoreCase);

    private static bool IsTypeScriptKind(string kind) =>
        string.Equals(a: kind, b: TypeScriptKind, comparisonType: StringComparison.OrdinalIgnoreCase);
}
