using System.Collections.Concurrent;

namespace Typewriter.Engine;

internal sealed class TemplateSourceIndex
{
    private readonly IReadOnlyDictionary<int, int> _blockEnds;
    private readonly ConcurrentDictionary<int, string> _identifiers = new();
    private readonly ConcurrentDictionary<(int Start, int Length), string> _texts = new();
    private readonly Lazy<TemplateLineMap> _lines;

    public TemplateSourceIndex(string content)
    {
        Content = content;
        _blockEnds = FindBlocks(content: content);
        _lines = new Lazy<TemplateLineMap>(valueFactory: () => new TemplateLineMap(content: content));
    }

    public string Content { get; }

    public TemplateSlice Root => new(Source: this, Start: 0, Length: Content.Length);

    public int FindBlockEnd(int openIndex) =>
        _blockEnds.TryGetValue(key: openIndex, value: out var end) ? end : -1;

    public (int Line, int Column) GetPosition(int index) => _lines.Value.GetPosition(index: index);

    public string GetText(int start, int length) =>
        length == 0
            ? string.Empty
            : _texts.GetOrAdd(
                key: (start, length),
                valueFactory: static (range, content) => content.Substring(startIndex: range.Start, length: range.Length),
                factoryArgument: Content);

    public string ReadIdentifier(int start) =>
        _identifiers.GetOrAdd(
            key: start,
            valueFactory: static (position, content) =>
            {
                var end = position;
                while (end < content.Length && (char.IsLetterOrDigit(c: content[index: end]) || content[index: end] == '_'))
                {
                    end++;
                }

                return content[position..end];
            },
            factoryArgument: Content);

    private static IReadOnlyDictionary<int, int> FindBlocks(string content)
    {
        var ends = new Dictionary<int, int>();
        var ranges = new Stack<(int Start, int End)>();
        ranges.Push(item: (0, content.Length));
        while (ranges.TryPop(result: out var range))
        {
            IndexTemplateBlocks(content: content, start: range.Start, end: range.End, ends: ends, ranges: ranges);
        }

        return ends;
    }

    private static void IndexTemplateBlocks(
        string content,
        int start,
        int end,
        IDictionary<int, int> ends,
        Stack<(int Start, int End)> ranges)
    {
        var pending = new Stack<int>();
        for (var index = start; index < end; index++)
        {
            if (content[index: index] == '$'
                && TrySkipCode(content: content, index: ref index, ends: ends, bodyStart: out var bodyStart))
            {
                if (bodyStart >= 0)
                {
                    // Scalars leave parentheses as output, so their contents may
                    // contain live template blocks even inside literal quotes.
                    ranges.Push(item: (bodyStart, index));
                }

                continue;
            }

            if (content[index: index] == '[')
            {
                pending.Push(item: index);
            }
            else if (content[index: index] == ']' && pending.TryPop(result: out var blockStart))
            {
                ends.Add(key: blockStart, value: index);
            }
        }
    }

    private static bool TrySkipCode(
        string content,
        ref int index,
        IDictionary<int, int> ends,
        out int bodyStart)
    {
        bodyStart = -1;
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

        if (!ends.TryGetValue(key: cursor, value: out var end))
        {
            end = TemplateCodeBlockScanner.IndexBalancedEnd(content: content, openIndex: cursor, ends: ends);
        }

        if (end < 0)
        {
            return false;
        }

        ends[key: cursor] = end;
        if (content[index: cursor] == '(')
        {
            bodyStart = cursor + 1;
        }

        index = end;
        return true;
    }
}
