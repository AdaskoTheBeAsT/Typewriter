namespace Typewriter.LanguageServer;

internal sealed record TextDocumentState(
    string Uri,
    string Path,
    string Text,
    int? Version)
{
    public int GetOffset(LspPosition position)
    {
        var line = 0;
        var character = 0;
        for (var index = 0; index < Text.Length; index++)
        {
            if (line == position.Line && character == position.Character)
            {
                return index;
            }

            if (Text[index: index] == '\n')
            {
                line++;
                character = 0;
                continue;
            }

            character++;
        }

        return Text.Length;
    }

    public string GetLine(LspPosition position)
    {
        using var reader = new StringReader(s: Text);
        for (var line = 0; line <= position.Line; line++)
        {
            var textLine = reader.ReadLine();
            if (textLine is null)
            {
                return string.Empty;
            }

            if (line == position.Line)
            {
                return textLine;
            }
        }

        return string.Empty;
    }

    public TemplateToken GetToken(LspPosition position)
    {
        var offset = GetOffset(position: position);
        if (Text.Length == 0)
        {
            return new TemplateToken(Text: string.Empty, Start: offset, End: offset, HasTemplatePrefix: false);
        }

        offset = Math.Clamp(value: offset, min: 0, max: Text.Length);
        var cursor = offset < Text.Length && IsTokenCharacter(value: Text[index: offset])
            ? offset
            : offset - 1;
        if (cursor < 0 || cursor >= Text.Length || !IsTokenCharacter(value: Text[index: cursor]))
        {
            return new TemplateToken(Text: string.Empty, Start: offset, End: offset, HasTemplatePrefix: false);
        }

        var start = cursor;
        while (start > 0 && IsTokenCharacter(value: Text[index: start - 1]))
        {
            start--;
        }

        var end = cursor + 1;
        while (end < Text.Length && IsTokenCharacter(value: Text[index: end]))
        {
            end++;
        }

        var hasTemplatePrefix = start > 0 && Text[index: start - 1] == '$';
        return new TemplateToken(
            Text: Text[start..end],
            Start: hasTemplatePrefix ? start - 1 : start,
            End: end,
            HasTemplatePrefix: hasTemplatePrefix);
    }

    private static bool IsTokenCharacter(char value) =>
        char.IsLetterOrDigit(c: value)
        || value == '_'
        || value == '.'
        || value == '-'
        || value == '/'
        || value == '\\';
}
