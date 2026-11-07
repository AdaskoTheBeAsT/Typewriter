namespace Typewriter.Engine;

internal sealed class TemplateLineMap
{
    private readonly int[] _lineStarts;
    private readonly int _length;

    public TemplateLineMap(string content)
    {
        var starts = new List<int> { 0 };
        for (var index = 0; index < content.Length; index++)
        {
            if (content[index: index] == '\n')
            {
                starts.Add(item: index + 1);
            }
        }

        _lineStarts = starts.ToArray();
        _length = content.Length;
    }

    public (int Line, int Column) GetPosition(int index)
    {
        index = Math.Clamp(value: index, min: 0, max: _length);
        var line = Array.BinarySearch(array: _lineStarts, value: index);
        if (line < 0)
        {
            line = ~line - 1;
        }

        return (line + 1, index - _lineStarts[line] + 1);
    }
}
