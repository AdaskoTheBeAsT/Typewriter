namespace Typewriter.Engine;

internal static class TemplateCodeBlockScanner
{
    private static readonly string[] CompatibilityPrefixes =
    [
        "using ",
        "#r ",
        "#reference ",
        "#load ",
        "Template",
        "public ",
        "private ",
        "internal ",
        "protected ",
        "static ",
        "const ",
        "bool ",
        "string ",
        "char ",
        "int ",
        "long ",
        "void ",
        "List<",
        "IEnumerable<",
    ];

    private static readonly string[] PartialCompatibilityPrefixes =
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

    public static bool IsCompatibilityCodeBlock(
        string block,
        bool allowPartial = false)
    {
        ArgumentNullException.ThrowIfNull(argument: block);

        var startIndex = SkipLeadingTrivia(block: block);
        if (startIndex >= block.Length)
        {
            return false;
        }

        var lineEnd = block.IndexOfAny(anyOf: ['\r', '\n'], startIndex: startIndex);
        var firstLine = lineEnd < 0 ? block[startIndex..] : block[startIndex..lineEnd];
        return CompatibilityPrefixes.Any(predicate: prefix => firstLine.StartsWith(value: prefix, comparisonType: StringComparison.Ordinal))
            || (allowPartial && PartialCompatibilityPrefixes.Any(predicate: prefix => firstLine.StartsWith(value: prefix, comparisonType: StringComparison.Ordinal)));
    }

    public static int FindBalancedEnd(
        string content,
        int openIndex)
    {
        ArgumentNullException.ThrowIfNull(argument: content);

        if (openIndex < 0
            || openIndex >= content.Length
            || content[index: openIndex] != '{')
        {
            return -1;
        }

        return ScanBalancedBraces(content: content, startIndex: openIndex + 1);
    }

    private static int SkipLeadingTrivia(string block)
    {
        var index = 0;
        while (index < block.Length)
        {
            while (index < block.Length && char.IsWhiteSpace(c: block[index: index]))
            {
                index++;
            }

            if (StartsWith(content: block, index: index, value: "//"))
            {
                index = SkipSingleLineComment(content: block, index: index + 2);
                continue;
            }

            if (StartsWith(content: block, index: index, value: "/*"))
            {
                index = SkipMultiLineComment(content: block, index: index + 2);
                continue;
            }

            break;
        }

        return index;
    }

    private static int ScanBalancedBraces(
        string content,
        int startIndex)
    {
        var depth = 1;
        var index = startIndex;
        while (index < content.Length)
        {
            if (TrySkipNonCodeConstruct(content: content, index: ref index))
            {
                continue;
            }

            var current = content[index: index];
            if (current == '{')
            {
                depth++;
            }
            else if (current == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return index;
                }
            }

            index++;
        }

        return -1;
    }

    private static bool TrySkipNonCodeConstruct(
        string content,
        ref int index)
    {
        if (StartsWith(content: content, index: index, value: "//"))
        {
            index = SkipSingleLineComment(content: content, index: index + 2);
            return true;
        }

        if (StartsWith(content: content, index: index, value: "/*"))
        {
            index = SkipMultiLineComment(content: content, index: index + 2);
            return true;
        }

        if (content[index: index] == '\'')
        {
            index = SkipCharacterLiteral(content: content, index: index + 1);
            return true;
        }

        return TrySkipStringLiteral(content: content, index: ref index);
    }

    private static bool TrySkipStringLiteral(
        string content,
        ref int index)
    {
        var current = content[index: index];
        if (current == '@')
        {
            return TrySkipAtPrefixedString(content: content, index: ref index);
        }

        if (current == '$')
        {
            return TrySkipDollarPrefixedString(content: content, index: ref index);
        }

        if (current != '"')
        {
            return false;
        }

        var quoteCount = CountConsecutive(content: content, index: index, character: '"');
        index = quoteCount >= 3
            ? SkipRawString(content: content, index: index + quoteCount, quoteCount: quoteCount)
            : SkipRegularString(content: content, index: index + 1, isInterpolated: false);
        return true;
    }

    private static bool TrySkipAtPrefixedString(
        string content,
        ref int index)
    {
        if (StartsWith(content: content, index: index, value: "@\""))
        {
            index = SkipVerbatimString(content: content, index: index + 2, isInterpolated: false);
            return true;
        }

        if (StartsWith(content: content, index: index, value: "@$\""))
        {
            index = SkipVerbatimString(content: content, index: index + 3, isInterpolated: true);
            return true;
        }

        return false;
    }

    private static bool TrySkipDollarPrefixedString(
        string content,
        ref int index)
    {
        if (StartsWith(content: content, index: index, value: "$@\""))
        {
            index = SkipVerbatimString(content: content, index: index + 3, isInterpolated: true);
            return true;
        }

        var dollarCount = CountConsecutive(content: content, index: index, character: '$');
        var quoteStart = index + dollarCount;
        if (quoteStart >= content.Length || content[index: quoteStart] != '"')
        {
            return false;
        }

        var quoteCount = CountConsecutive(content: content, index: quoteStart, character: '"');
        if (quoteCount >= 3)
        {
            index = SkipInterpolatedRawString(
                content: content,
                index: quoteStart + quoteCount,
                dollarCount: dollarCount,
                quoteCount: quoteCount);
            return true;
        }

        if (dollarCount != 1)
        {
            return false;
        }

        index = SkipRegularString(content: content, index: quoteStart + 1, isInterpolated: true);
        return true;
    }

    private static int SkipSingleLineComment(
        string content,
        int index)
    {
        while (index < content.Length && content[index: index] is not '\r' and not '\n')
        {
            index++;
        }

        return index;
    }

    private static int SkipMultiLineComment(
        string content,
        int index)
    {
        var end = content.IndexOf(value: "*/", startIndex: index, comparisonType: StringComparison.Ordinal);
        return end < 0 ? content.Length : end + 2;
    }

    private static int SkipCharacterLiteral(
        string content,
        int index)
    {
        while (index < content.Length)
        {
            var current = content[index: index];
            if (current == '\\')
            {
                index += index + 1 < content.Length ? 2 : 1;
                continue;
            }

            if (current == '\'')
            {
                return index + 1;
            }

            if (current is '\r' or '\n')
            {
                return index;
            }

            index++;
        }

        return content.Length;
    }

    private static int SkipRegularString(
        string content,
        int index,
        bool isInterpolated)
    {
        while (index < content.Length)
        {
            var current = content[index: index];
            if (current == '\\')
            {
                index += index + 1 < content.Length ? 2 : 1;
                continue;
            }

            if (current == '"')
            {
                return index + 1;
            }

            if (isInterpolated
                && (current == '{' || current == '}')
                && TrySkipInterpolatedBrace(content: content, index: ref index))
            {
                continue;
            }

            index++;
        }

        return content.Length;
    }

    private static int SkipVerbatimString(
        string content,
        int index,
        bool isInterpolated)
    {
        while (index < content.Length)
        {
            var current = content[index: index];
            if (current == '"')
            {
                if (index + 1 < content.Length && content[index: index + 1] == '"')
                {
                    index += 2;
                    continue;
                }

                return index + 1;
            }

            if (isInterpolated
                && (current == '{' || current == '}')
                && TrySkipInterpolatedBrace(content: content, index: ref index))
            {
                continue;
            }

            index++;
        }

        return content.Length;
    }

    private static bool TrySkipInterpolatedBrace(
        string content,
        ref int index)
    {
        var current = content[index: index];
        if (index + 1 < content.Length && content[index: index + 1] == current)
        {
            index += 2;
            return true;
        }

        if (current != '{')
        {
            return false;
        }

        var holeEnd = ScanBalancedBraces(content: content, startIndex: index + 1);
        index = holeEnd < 0 ? content.Length : holeEnd + 1;
        return true;
    }

    private static int SkipRawString(
        string content,
        int index,
        int quoteCount)
    {
        while (index < content.Length)
        {
            if (content[index: index] == '"')
            {
                var quotes = CountConsecutive(content: content, index: index, character: '"');
                if (quotes >= quoteCount)
                {
                    return index + quotes;
                }

                index += quotes;
                continue;
            }

            index++;
        }

        return content.Length;
    }

    private static int SkipInterpolatedRawString(
        string content,
        int index,
        int dollarCount,
        int quoteCount)
    {
        while (index < content.Length)
        {
            var current = content[index: index];
            if (current == '"')
            {
                var quotes = CountConsecutive(content: content, index: index, character: '"');
                if (quotes >= quoteCount)
                {
                    return index + quotes;
                }

                index += quotes;
                continue;
            }

            if (current == '{')
            {
                index = SkipRawInterpolationBraces(content: content, index: index, dollarCount: dollarCount);
                continue;
            }

            index++;
        }

        return content.Length;
    }

    private static int SkipRawInterpolationBraces(
        string content,
        int index,
        int dollarCount)
    {
        var braces = CountConsecutive(content: content, index: index, character: '{');
        if (braces < dollarCount)
        {
            return index + braces;
        }

        var holeEnd = ScanBalancedBraces(content: content, startIndex: index + braces);
        if (holeEnd < 0)
        {
            return content.Length;
        }

        var nextIndex = holeEnd + 1;
        var extraCloseBraces = Math.Min(
            val1: dollarCount - 1,
            val2: CountConsecutive(content: content, index: nextIndex, character: '}'));
        return nextIndex + extraCloseBraces;
    }

    private static int CountConsecutive(
        string content,
        int index,
        char character)
    {
        var cursor = index;
        while (cursor < content.Length && content[index: cursor] == character)
        {
            cursor++;
        }

        return cursor - index;
    }

    private static bool StartsWith(
        string content,
        int index,
        string value) =>
        index + value.Length <= content.Length
        && string.Compare(strA: content, indexA: index, strB: value, indexB: 0, length: value.Length, comparisonType: StringComparison.Ordinal) == 0;
}
