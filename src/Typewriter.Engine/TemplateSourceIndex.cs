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
        var codeEnds = new Dictionary<int, int>();
        for (var index = 0; index < content.Length; index++)
        {
            if (content[index: index] == '$' && TryIndexCode(content: content, index: ref index, ends: ends, codeEnd: out var codeEnd))
            {
                codeEnds.Add(key: index, value: codeEnd);
            }
        }

        // Each suffix owns a persistent stack of unmatched closing brackets. A code
        // marker skips its body, but brackets inside retained scalar output still
        // have their own matches, including matches beyond the code delimiter.
        var followingClosings = new int[content.Length + 1];
        followingClosings[content.Length] = -1;
        for (var index = content.Length - 1; index >= 0; index--)
        {
            var next = followingClosings[index + 1];
            if (codeEnds.TryGetValue(key: index, value: out var codeEnd))
            {
                next = followingClosings[codeEnd + 1];
            }
            else if (content[index: index] == ']')
            {
                next = index;
            }
            else if (content[index: index] == '[' && next >= 0)
            {
                ends.Add(key: index, value: next);
                next = followingClosings[next + 1];
            }

            followingClosings[index] = next;
        }

        return ends;
    }

    private static bool TryIndexCode(
        string content,
        ref int index,
        IDictionary<int, int> ends,
        out int codeEnd)
    {
        codeEnd = -1;
        var cursor = index + 1;
        if (cursor < content.Length && content[index: cursor] == '$')
        {
            index = cursor;
            return false;
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

        codeEnd = end;
        return true;
    }
}
