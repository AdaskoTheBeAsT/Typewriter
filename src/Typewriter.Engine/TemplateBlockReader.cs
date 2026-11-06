namespace Typewriter.Engine;

internal static class TemplateBlockReader
{
    public static bool TryRead(
        string template,
        ref int index,
        char open,
        char close,
        out string? block)
    {
        block = null;
        var openIndex = index + 1;
        if (openIndex >= template.Length || template[index: openIndex] != open)
        {
            return false;
        }

        var end = FindEnd(content: template, openIndex: openIndex, open: open, close: close);
        if (end < 0)
        {
            index = template.Length - 1;
            return true;
        }

        block = template.Substring(startIndex: openIndex + 1, length: end - openIndex - 1);
        index = end;
        return true;
    }

    public static int FindEnd(
        string content,
        int openIndex,
        char open,
        char close)
    {
        if (open == '(')
        {
            return TemplateCodeBlockScanner.FindBalancedEnd(content: content, openIndex: openIndex);
        }

        var depth = 0;
        for (var index = openIndex; index < content.Length; index++)
        {
            if (content[index: index] == '$' && TrySkipCode(content: content, index: ref index))
            {
                continue;
            }

            if (content[index: index] == open)
            {
                depth++;
            }
            else if (content[index: index] == close && --depth == 0)
            {
                return index;
            }
        }

        return -1;
    }

    private static bool TrySkipCode(
        string content,
        ref int index)
    {
        var cursor = index + 1;
        if (cursor < content.Length && content[index: cursor] == '$')
        {
            index = cursor;
            return true;
        }

        while (cursor < content.Length && (char.IsLetterOrDigit(c: content[index: cursor]) || content[index: cursor] == '_'))
        {
            cursor++;
        }

        if (cursor >= content.Length || content[index: cursor] is not ('(' or '{'))
        {
            return false;
        }

        var end = TemplateCodeBlockScanner.FindBalancedEnd(content: content, openIndex: cursor);
        if (end < 0)
        {
            return false;
        }

        index = end;
        return true;
    }
}
