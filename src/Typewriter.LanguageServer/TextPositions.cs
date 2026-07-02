namespace Typewriter.LanguageServer;

internal static class TextPositions
{
    public static LspPosition GetPosition(
        string text,
        int offset)
    {
        var line = 0;
        var character = 0;
        var limit = Math.Clamp(value: offset, min: 0, max: text.Length);
        for (var index = 0; index < limit; index++)
        {
            if (text[index: index] == '\n')
            {
                line++;
                character = 0;
                continue;
            }

            character++;
        }

        return new LspPosition(Line: line, Character: character);
    }

    public static int GetOffset(
        string text,
        LspPosition position)
    {
        var line = 0;
        var character = 0;
        for (var index = 0; index < text.Length; index++)
        {
            if (line == position.Line && character == position.Character)
            {
                return index;
            }

            if (text[index: index] == '\n')
            {
                line++;
                character = 0;
                continue;
            }

            character++;
        }

        return text.Length;
    }
}
