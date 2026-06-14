using System.Globalization;
using Typewriter.Abstractions;

namespace Typewriter.Engine;

internal static class TemplateAttributeValueFormatter
{
    public static string Format(AttributeMetadata attribute)
    {
        ArgumentNullException.ThrowIfNull(argument: attribute);

        if (attribute.Arguments.Count == 0)
        {
            return string.Empty;
        }

        if (attribute.Arguments.Count == 1
            && string.IsNullOrWhiteSpace(value: attribute.Arguments[index: 0].Name))
        {
            return attribute.Arguments[index: 0].Value ?? string.Empty;
        }

        return string.Join(
            separator: ", ",
            values: attribute.Arguments.Select(selector: FormatArgument));
    }

    private static string FormatArgument(AttributeArgumentMetadata argument)
    {
        var value = FormatValue(value: argument.Value);
        return string.IsNullOrWhiteSpace(value: argument.Name)
            ? value
            : string.Concat(str0: argument.Name, str1: " = ", str2: value);
    }

    private static string FormatValue(string? value)
    {
        if (value is null)
        {
            return "null";
        }

        if (IsSourceLike(value: value))
        {
            return value;
        }

        return string.Concat(str0: "\"", str1: value.Replace(oldValue: "\"", newValue: "\\\"", comparisonType: StringComparison.Ordinal), str2: "\"");
    }

    private static bool IsSourceLike(string value)
    {
        return value.StartsWith(value: "typeof(", comparisonType: StringComparison.Ordinal)
            || value.StartsWith(value: "nameof(", comparisonType: StringComparison.Ordinal)
            || value.StartsWith(value: '\"')
            || value.StartsWith(value: '\'')
            || value.Equals(value: "true", comparisonType: StringComparison.OrdinalIgnoreCase)
            || value.Equals(value: "false", comparisonType: StringComparison.OrdinalIgnoreCase)
            || value.Equals(value: "null", comparisonType: StringComparison.OrdinalIgnoreCase)
            || decimal.TryParse(s: value, style: NumberStyles.Number, provider: CultureInfo.InvariantCulture, result: out _);
    }
}
