using Typewriter.Abstractions;

namespace Typewriter.Engine;

public static class OutputContentFormatter
{
    public static string Format(
        string content,
        OutputConfiguration output)
    {
        ArgumentNullException.ThrowIfNull(argument: content);
        ArgumentNullException.ThrowIfNull(argument: output);

        var formatted = NormalizeToLineFeed(content: content);
        formatted = ApplyIndentation(content: formatted, style: output.IndentStyle, size: output.IndentSize);
        if (output.TrimTrailingWhitespace)
        {
            formatted = TrimTrailingWhitespace(content: formatted);
        }

        if (output.InsertFinalNewline)
        {
            formatted = EnsureFinalNewline(content: formatted);
        }

        return output.Newline.Equals(value: "crlf", comparisonType: StringComparison.OrdinalIgnoreCase)
            ? formatted.Replace(oldValue: "\n", newValue: "\r\n", comparisonType: StringComparison.Ordinal)
            : formatted;
    }

    private static string NormalizeToLineFeed(string content)
    {
        return content.Replace(oldValue: "\r\n", newValue: "\n", comparisonType: StringComparison.Ordinal)
            .Replace(oldChar: '\r', newChar: '\n');
    }

    private static string ApplyIndentation(
        string content,
        IndentStyle style,
        int size)
    {
        if (style == IndentStyle.Preserve)
        {
            return content;
        }

        var indentSize = Math.Max(val1: 1, val2: size);
        var lines = content.Split(separator: '\n');
        var unit = DetectSpaceIndentUnit(lines: lines) ?? indentSize;
        for (var index = 0; index < lines.Length; index++)
        {
            lines[index] = ReindentLine(line: lines[index], style: style, indentSize: indentSize, unit: unit);
        }

        return string.Join(separator: '\n', value: lines);
    }

    private static int? DetectSpaceIndentUnit(string[] lines)
    {
        var unit = 0;
        foreach (var line in lines)
        {
            var (_, spaces, contentIndex) = MeasureLeadingWhitespace(line: line);
            if (contentIndex >= line.Length || spaces == 0)
            {
                continue;
            }

            unit = GreatestCommonDivisor(left: unit, right: spaces);
        }

        return unit == 0 ? null : unit;
    }

    private static string ReindentLine(
        string line,
        IndentStyle style,
        int indentSize,
        int unit)
    {
        var (tabs, spaces, contentIndex) = MeasureLeadingWhitespace(line: line);
        if (contentIndex >= line.Length || (tabs == 0 && spaces == 0))
        {
            return line;
        }

        var levels = tabs + (spaces / unit);
        var remainder = spaces % unit;
        var indentation = style == IndentStyle.Tab
            ? new string(c: '\t', count: levels) + new string(c: ' ', count: remainder)
            : new string(c: ' ', count: (levels * indentSize) + remainder);

        return indentation + line[contentIndex..];
    }

    private static (int Tabs, int Spaces, int ContentIndex) MeasureLeadingWhitespace(string line)
    {
        var tabs = 0;
        var spaces = 0;
        var index = 0;
        while (index < line.Length)
        {
            if (line[index: index] == '\t')
            {
                tabs++;
            }
            else if (line[index: index] == ' ')
            {
                spaces++;
            }
            else
            {
                break;
            }

            index++;
        }

        return (tabs, spaces, index);
    }

    private static int GreatestCommonDivisor(
        int left,
        int right)
    {
        while (right != 0)
        {
            (left, right) = (right, left % right);
        }

        return left;
    }

    private static string TrimTrailingWhitespace(string content)
    {
        var lines = content.Split(separator: '\n');
        for (var index = 0; index < lines.Length; index++)
        {
            lines[index] = lines[index].TrimEnd(' ', '\t');
        }

        return string.Join(separator: '\n', value: lines);
    }

    private static string EnsureFinalNewline(string content)
    {
        return content.Length == 0 || content.EndsWith(value: '\n')
            ? content
            : content + "\n";
    }
}
