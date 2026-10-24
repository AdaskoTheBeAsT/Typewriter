using Typewriter.Engine;

namespace Typewriter.LanguageServer;

internal static class TemplateEmbeddedLanguage
{
    public static EmbeddedLanguageKind GetKindAt(
        TextDocumentState document,
        LspPosition position)
    {
        var offset = document.GetOffset(position: position);
        if (IsInsideCSharpBlock(text: document.Text, offset: offset))
        {
            return EmbeddedLanguageKind.CSharp;
        }

        var token = document.GetToken(position: position);
        if (token.HasTemplatePrefix
            || IsTemplateCompletionOffset(text: document.Text, offset: offset))
        {
            return EmbeddedLanguageKind.Template;
        }

        return EmbeddedLanguageKind.TypeScript;
    }

    public static bool TryReadCSharpBlock(
        string text,
        int dollarIndex,
        out EmbeddedLanguageRegion region) =>
        TryReadCSharpBlock(text: text, dollarIndex: dollarIndex, allowUnclosed: false, region: out region);

    public static bool TryReadCSharpBlock(
        string text,
        int dollarIndex,
        bool allowUnclosed,
        out EmbeddedLanguageRegion region)
    {
        region = default!;
        if (dollarIndex < 0
            || dollarIndex + 1 >= text.Length
            || text[index: dollarIndex] != '$'
            || text[index: dollarIndex + 1] != '{')
        {
            return false;
        }

        var end = TemplateCodeBlockScanner.FindBalancedEnd(content: text, openIndex: dollarIndex + 1);
        if (end < 0 && !allowUnclosed)
        {
            return false;
        }

        var contentStart = dollarIndex + 2;
        var contentEnd = end < 0 ? text.Length : end;
        if (!TemplateCodeBlockScanner.IsCompatibilityCodeBlock(
                block: text[contentStart..contentEnd],
                allowPartial: IsStandaloneBlockStart(text: text, dollarIndex: dollarIndex)))
        {
            return false;
        }

        region = new EmbeddedLanguageRegion(
            Kind: EmbeddedLanguageKind.CSharp,
            Start: dollarIndex,
            End: end < 0 ? text.Length : end + 1,
            ContentStart: contentStart,
            ContentEnd: contentEnd);
        return true;
    }

    public static bool IsTemplateTokenStart(
        string text,
        int index) =>
        index >= 0
        && index + 1 < text.Length
        && text[index: index] == '$'
        && IsIdentifierStart(value: text[index: index + 1]);

    public static int ReadTemplateTokenEnd(
        string text,
        int start)
    {
        var end = start + 2;
        while (end < text.Length && IsIdentifierPart(value: text[index: end]))
        {
            end++;
        }

        return end;
    }

    private static bool IsInsideCSharpBlock(
        string text,
        int offset)
    {
        var index = 0;
        while (index < text.Length)
        {
            if (text[index: index] != '$')
            {
                index++;
                continue;
            }

            if (!TryReadCSharpBlock(text: text, dollarIndex: index, allowUnclosed: true, region: out var region))
            {
                index++;
                continue;
            }

            if (offset >= region.Start && offset <= region.End)
            {
                return true;
            }

            index = region.End;
        }

        return false;
    }

    private static bool IsTemplateCompletionOffset(
        string text,
        int offset)
    {
        if (offset > 0 && offset <= text.Length && text[index: offset - 1] == '$')
        {
            return true;
        }

        if (offset >= 0 && offset < text.Length && text[index: offset] == '$')
        {
            return true;
        }

        return false;
    }

    private static bool IsStandaloneBlockStart(
        string text,
        int dollarIndex)
    {
        for (var index = dollarIndex - 1; index >= 0 && text[index: index] is not '\r' and not '\n'; index--)
        {
            if (!char.IsWhiteSpace(c: text[index: index]))
            {
                return false;
            }
        }

        for (var index = dollarIndex + 2; index < text.Length && text[index: index] is not '\r' and not '\n'; index++)
        {
            if (!char.IsWhiteSpace(c: text[index: index]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsIdentifierStart(char value) =>
        char.IsLetter(c: value) || value == '_';

    private static bool IsIdentifierPart(char value) =>
        char.IsLetterOrDigit(c: value) || value == '_';
}
