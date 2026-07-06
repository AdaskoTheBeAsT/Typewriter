namespace Typewriter.LanguageServer;

/// <summary>
/// Projects a .tst template into a TypeScript view with identical offsets by
/// blanking C# helper blocks, so template and virtual positions map one-to-one.
/// TypeScript template-literal interpolation such as ${environment.apiBaseUrl}
/// is preserved because it is not recognized as a C# helper block.
/// </summary>
internal static class EmbeddedTypeScriptDocument
{
    public static string Create(string templateText)
    {
        var buffer = templateText.ToCharArray();
        var index = 0;
        while (index < templateText.Length)
        {
            if (templateText[index: index] != '$'
                || !TemplateEmbeddedLanguage.TryReadCSharpBlock(text: templateText, dollarIndex: index, allowUnclosed: true, region: out var region))
            {
                index++;
                continue;
            }

            BlankRegion(buffer: buffer, start: region.Start, end: region.End);
            index = Math.Max(val1: region.End, val2: index + 1);
        }

        return new string(value: buffer);
    }

    private static void BlankRegion(
        char[] buffer,
        int start,
        int end)
    {
        for (var index = start; index < end && index < buffer.Length; index++)
        {
            if (buffer[index] is not '\r' and not '\n')
            {
                buffer[index] = ' ';
            }
        }
    }
}
