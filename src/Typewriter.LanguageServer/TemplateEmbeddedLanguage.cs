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

        var end = FindBalancedEnd(content: text, openIndex: dollarIndex + 1, open: '{', close: '}');
        if (end < 0 && !allowUnclosed)
        {
            return false;
        }

        var contentStart = dollarIndex + 2;
        var contentEnd = end < 0 ? text.Length : end;
        if (!IsCompatibilityCodeBlock(
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

    private static bool IsCompatibilityCodeBlock(
        string block,
        bool allowPartial)
    {
        var trimmed = block.TrimStart();
        if (trimmed.Length == 0)
        {
            return false;
        }

        var firstLineEnd = trimmed.IndexOfAny(anyOf: ['\r', '\n']);
        var firstLine = firstLineEnd < 0 ? trimmed : trimmed[..firstLineEnd];
        return firstLine.StartsWith(value: "using ", comparisonType: StringComparison.Ordinal)
            || firstLine.StartsWith(value: "#r ", comparisonType: StringComparison.Ordinal)
            || firstLine.StartsWith(value: "#reference ", comparisonType: StringComparison.Ordinal)
            || firstLine.StartsWith(value: "Template", comparisonType: StringComparison.Ordinal)
            || firstLine.StartsWith(value: "public ", comparisonType: StringComparison.Ordinal)
            || firstLine.StartsWith(value: "private ", comparisonType: StringComparison.Ordinal)
            || firstLine.StartsWith(value: "internal ", comparisonType: StringComparison.Ordinal)
            || firstLine.StartsWith(value: "protected ", comparisonType: StringComparison.Ordinal)
            || firstLine.StartsWith(value: "static ", comparisonType: StringComparison.Ordinal)
            || firstLine.StartsWith(value: "const ", comparisonType: StringComparison.Ordinal)
            || firstLine.StartsWith(value: "bool ", comparisonType: StringComparison.Ordinal)
            || firstLine.StartsWith(value: "string ", comparisonType: StringComparison.Ordinal)
            || firstLine.StartsWith(value: "char ", comparisonType: StringComparison.Ordinal)
            || firstLine.StartsWith(value: "int ", comparisonType: StringComparison.Ordinal)
            || firstLine.StartsWith(value: "long ", comparisonType: StringComparison.Ordinal)
            || firstLine.StartsWith(value: "void ", comparisonType: StringComparison.Ordinal)
            || firstLine.StartsWith(value: "List<", comparisonType: StringComparison.Ordinal)
            || firstLine.StartsWith(value: "IEnumerable<", comparisonType: StringComparison.Ordinal)
            || (allowPartial && IsPartialCompatibilityStart(firstLine: firstLine));
    }

    private static bool IsPartialCompatibilityStart(string firstLine)
    {
#pragma warning disable CC0001 // You should use 'var' whenever possible.
        string[] prefixes =
        [
            "boo",
            "cha",
            "con",
            "int",
            "lon",
            "pri",
            "pro",
            "pub",
            "sta",
            "str",
            "usi",
            "voi",
        ];
#pragma warning restore CC0001 // You should use 'var' whenever possible.

        return prefixes.Any(predicate: prefix => firstLine.StartsWith(value: prefix, comparisonType: StringComparison.Ordinal));
    }

    private static int FindBalancedEnd(
        string content,
        int openIndex,
        char open,
        char close)
    {
        var depth = 0;
        for (var index = openIndex; index < content.Length; index++)
        {
            if (content[index: index] == open)
            {
                depth++;
            }
            else if (content[index: index] == close)
            {
                depth--;
                if (depth == 0)
                {
                    return index;
                }
            }
        }

        return -1;
    }

    private static bool IsIdentifierStart(char value) =>
        char.IsLetter(c: value) || value == '_';

    private static bool IsIdentifierPart(char value) =>
        char.IsLetterOrDigit(c: value) || value == '_';
}
