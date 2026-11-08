namespace Typewriter.Engine;

internal readonly record struct TemplateSlice(
    TemplateSourceIndex Source,
    int Start,
    int Length)
{
    public TemplateSlice Empty => new(Source: Source, Start: Start, Length: 0);

    public string Text => Source.GetText(start: Start, length: Length);

    public char this[int index] => Source.Content[index: Start + index];

    public string ReadIdentifier(int start)
    {
        var identifier = Source.ReadIdentifier(start: Start + start);
        return identifier.Length > Length - start ? identifier[..(Length - start)] : identifier;
    }

    public bool TryReadBlock(
        ref int index,
        char open,
        out TemplateSlice? block)
    {
        block = null;
        var openIndex = index + 1;
        if (openIndex >= Length || this[index: openIndex] != open)
        {
            return false;
        }

        var end = Source.FindBlockEnd(openIndex: Start + openIndex);
        if (end < 0 || end >= Start + Length)
        {
            index = Length - 1;
            return true;
        }

        block = new TemplateSlice(Source: Source, Start: Start + openIndex + 1, Length: end - Start - openIndex - 1);
        index = end - Start;
        return true;
    }
}
