using System.Text;

namespace Typewriter.CodeModel;

public static class NameCaseFormatter
{
    public static string Format(
        string? value,
        NameCase nameCase)
    {
        var text = value ?? string.Empty;
        if (nameCase == NameCase.Original)
        {
            return text;
        }

        if (nameCase == NameCase.LegacyCamelCase)
        {
            return ToLegacyCamelCase(value: text);
        }

        var words = SplitWords(value: text);
        if (words.Count == 0)
        {
            return string.Empty;
        }

        return nameCase switch
        {
            NameCase.PascalCase => string.Concat(values: words.Select(selector: ToPascalWord)),
            NameCase.CamelCase => ToCamelCase(words: words),
            NameCase.LowerCase => string.Concat(values: words).ToLowerInvariant(),
            NameCase.UpperCase => string.Concat(values: words).ToUpperInvariant(),
            NameCase.LowerSnakeCase => JoinWords(words: words, separator: "_", convert: static word => word.ToLowerInvariant()),
            NameCase.UpperSnakeCase => JoinWords(words: words, separator: "_", convert: static word => word.ToUpperInvariant()),
            NameCase.LowerKebabCase => JoinWords(words: words, separator: "-", convert: static word => word.ToLowerInvariant()),
            NameCase.UpperKebabCase => JoinWords(words: words, separator: "-", convert: static word => word.ToUpperInvariant()),
            NameCase.TrainCase => JoinWords(words: words, separator: "-", convert: ToPascalWord),
            NameCase.LowerDotCase => JoinWords(words: words, separator: ".", convert: static word => word.ToLowerInvariant()),
            NameCase.UpperDotCase => JoinWords(words: words, separator: ".", convert: static word => word.ToUpperInvariant()),
            NameCase.LowerPathCase => JoinWords(words: words, separator: "/", convert: static word => word.ToLowerInvariant()),
            NameCase.UpperPathCase => JoinWords(words: words, separator: "/", convert: static word => word.ToUpperInvariant()),
            _ => text,
        };
    }

#pragma warning disable MA0051,S3776
    public static bool TryParse(
        string? value,
        out NameCase nameCase)
#pragma warning restore MA0051,S3776
    {
        nameCase = NameCase.Original;
        if (string.IsNullOrWhiteSpace(value: value))
        {
            return false;
        }

        var normalized = value.Replace(oldValue: "_", newValue: string.Empty, comparisonType: StringComparison.Ordinal)
            .Replace(oldValue: "-", newValue: string.Empty, comparisonType: StringComparison.Ordinal)
            .Replace(oldValue: ".", newValue: string.Empty, comparisonType: StringComparison.Ordinal);
        foreach (var candidate in System.Enum.GetValues<NameCase>())
        {
            var candidateName = candidate.ToString();
            if (normalized.Equals(value: candidateName, comparisonType: StringComparison.OrdinalIgnoreCase)
                || normalized.Equals(value: candidateName.Replace(oldValue: "Case", newValue: string.Empty, comparisonType: StringComparison.Ordinal), comparisonType: StringComparison.OrdinalIgnoreCase))
            {
                nameCase = candidate;
                return true;
            }
        }

        if (normalized.Equals(value: "camel", comparisonType: StringComparison.OrdinalIgnoreCase)
            || normalized.Equals(value: "lowercamel", comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            nameCase = NameCase.CamelCase;
            return true;
        }

        if (normalized.Equals(value: "legacycamel", comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            nameCase = NameCase.LegacyCamelCase;
            return true;
        }

        if (normalized.Equals(value: "pascal", comparisonType: StringComparison.OrdinalIgnoreCase)
            || normalized.Equals(value: "uppercamel", comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            nameCase = NameCase.PascalCase;
            return true;
        }

        if (normalized.Equals(value: "lower", comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            nameCase = NameCase.LowerCase;
            return true;
        }

        if (normalized.Equals(value: "upper", comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            nameCase = NameCase.UpperCase;
            return true;
        }

        if (normalized.Equals(value: "snake", comparisonType: StringComparison.OrdinalIgnoreCase)
            || normalized.Equals(value: "lowersnake", comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            nameCase = NameCase.LowerSnakeCase;
            return true;
        }

        if (normalized.Equals(value: "kebab", comparisonType: StringComparison.OrdinalIgnoreCase)
            || normalized.Equals(value: "lowerkebab", comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            nameCase = NameCase.LowerKebabCase;
            return true;
        }

        return false;
    }

    private static string ToLegacyCamelCase(string value)
    {
        if (string.IsNullOrEmpty(value: value) || char.IsLower(c: value[index: 0]))
        {
            return value;
        }

        return value.Length == 1
            ? value.ToLowerInvariant()
            : char.ToLowerInvariant(c: value[index: 0]) + value[1..];
    }

    private static string ToCamelCase(IReadOnlyList<string> words)
    {
        var pascal = string.Concat(values: words.Select(selector: ToPascalWord));
        return pascal.Length == 0
            ? pascal
            : char.ToLowerInvariant(c: pascal[index: 0]) + pascal[1..];
    }

    private static string ToPascalWord(string word)
    {
        if (string.IsNullOrEmpty(value: word))
        {
            return string.Empty;
        }

        var lower = word.ToLowerInvariant();
        return lower.Length == 1
            ? lower.ToUpperInvariant()
            : char.ToUpperInvariant(c: lower[index: 0]) + lower[1..];
    }

    private static string JoinWords(
        IReadOnlyList<string> words,
        string separator,
        Func<string, string> convert) =>
        string.Join(separator: separator, values: words.Select(selector: convert));

    private static IReadOnlyList<string> SplitWords(string value)
    {
        var words = new List<string>();
        var current = new StringBuilder(capacity: value.Length);
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index: index];
            if (!char.IsLetterOrDigit(c: character))
            {
                AddCurrentWord(words: words, current: current);
                continue;
            }

            if (current.Length > 0
                && StartsNewWord(previous: current[^1], current: character, next: index + 1 < value.Length ? value[index: index + 1] : null))
            {
                AddCurrentWord(words: words, current: current);
            }

            current.Append(value: character);
        }

        AddCurrentWord(words: words, current: current);
        return words;
    }

    private static bool StartsNewWord(
        char previous,
        char current,
        char? next)
    {
        if (char.IsDigit(c: previous) != char.IsDigit(c: current))
        {
            return true;
        }

        if (char.IsLower(c: previous) && char.IsUpper(c: current))
        {
            return true;
        }

        return char.IsUpper(c: previous)
               && char.IsUpper(c: current)
               && next.HasValue
               && char.IsLower(c: next.Value);
    }

    private static void AddCurrentWord(
        List<string> words,
        StringBuilder current)
    {
        if (current.Length == 0)
        {
            return;
        }

        words.Add(item: current.ToString());
        current.Clear();
    }
}
