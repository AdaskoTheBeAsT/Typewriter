using System.Text;
using Typewriter.Abstractions;

namespace Typewriter.Engine;

internal static class FileNameConventionFormatter
{
    public static string Format(string value, FileNameConvention convention)
    {
        if (string.IsNullOrWhiteSpace(value: value)
            || convention == FileNameConvention.Preserve)
        {
            return value;
        }

        var words = SplitWords(value: value);
        if (words.Count == 0)
        {
            return value;
        }

        return convention switch
        {
            FileNameConvention.Kebab => string.Join(separator: '-', values: words.Select(selector: ToLower)),
            FileNameConvention.Pascal => string.Concat(values: words.Select(selector: ToPascal)),
            FileNameConvention.Camel => ToCamel(words: words),
            FileNameConvention.Snake => string.Join(separator: '_', values: words.Select(selector: ToLower)),
            _ => value,
        };
    }

    private static IReadOnlyList<string> SplitWords(string value)
    {
        var words = new List<string>();
        var builder = new StringBuilder();

        for (var i = 0; i < value.Length; i++)
        {
            var current = value[index: i];
            if (!char.IsLetterOrDigit(c: current))
            {
                AddWord(words: words, builder: builder);
                continue;
            }

            if (builder.Length > 0
                && IsWordBoundary(value: value, index: i))
            {
                AddWord(words: words, builder: builder);
            }

            builder.Append(value: current);
        }

        AddWord(words: words, builder: builder);
        return words;
    }

    private static bool IsWordBoundary(string value, int index)
    {
        var previous = value[index: index - 1];
        var current = value[index: index];
        if (!char.IsLetterOrDigit(c: previous)
            || !char.IsLetterOrDigit(c: current))
        {
            return false;
        }

        if (char.IsLower(c: previous)
            && char.IsUpper(c: current))
        {
            return true;
        }

        return char.IsUpper(c: previous)
            && char.IsUpper(c: current)
            && index + 1 < value.Length
            && char.IsLower(c: value[index: index + 1]);
    }

    private static void AddWord(ICollection<string> words, StringBuilder builder)
    {
        if (builder.Length == 0)
        {
            return;
        }

        words.Add(item: builder.ToString());
        builder.Clear();
    }

    private static string ToCamel(IReadOnlyList<string> words)
    {
        if (words.Count == 0)
        {
            return string.Empty;
        }

        return ToLower(value: words[index: 0]) + string.Concat(values: words.Skip(count: 1).Select(selector: ToPascal));
    }

    private static string ToPascal(string value)
    {
        var lower = ToLower(value: value);
        return char.ToUpperInvariant(c: lower[index: 0]) + lower[1..];
    }

    private static string ToLower(string value) => value.ToLowerInvariant();
}
