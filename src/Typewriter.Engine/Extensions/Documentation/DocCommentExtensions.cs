using System.Text;
using System.Text.RegularExpressions;
using Typewriter.CodeModel;

namespace Typewriter.Extensions.Documentation;

public static class DocCommentExtensions
{
    private const string SeeSelfClosingPattern = "<see\\s+(?<attr>cref|langword|href)\\s*=\\s*\"(?<val>[^\"]*)\"\\s*/>";
    private const string SeeWithContentPattern = "<see\\s+(?<attr>cref|href)\\s*=\\s*\"(?<val>[^\"]*)\"\\s*>(?<text>.*?)</see>";
    private const string ReferencePattern = "<(?:paramref|typeparamref)\\s+name\\s*=\\s*\"(?<val>[^\"]*)\"\\s*/>";
    private const string CodePattern = "<c>(?<val>.*?)</c>";
    private const string ParaPattern = "</?para\\s*/?>";
    private const string MultiSpacePattern = "[ \\t]{2,}";

    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(value: 1);

    public static string ToJsDoc(this DocComment docComment)
    {
        ArgumentNullException.ThrowIfNull(argument: docComment);

        var lines = new List<string>();

        var summary = ConvertInlineTags(text: docComment.Summary);
        if (!string.IsNullOrWhiteSpace(value: summary))
        {
            lines.Add(item: summary);
        }

        foreach (var parameter in docComment.Parameters)
        {
            var description = ConvertInlineTags(text: parameter.Description);
            lines.Add(item: $"@param {parameter.Name} {description}".TrimEnd());
        }

        var returns = ConvertInlineTags(text: docComment.Returns);
        if (!string.IsNullOrWhiteSpace(value: returns))
        {
            lines.Add(item: $"@returns {returns}");
        }

        if (lines.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        builder.Append(value: "/**\n");
        foreach (var line in lines)
        {
            builder.Append(value: " * ").Append(value: line).Append(value: '\n');
        }

        builder.Append(value: " */");
        return builder.ToString();
    }

    public static string ToJsDocSummary(this DocComment docComment)
    {
        ArgumentNullException.ThrowIfNull(argument: docComment);

        return ConvertInlineTags(text: docComment.Summary);
    }

    public static string ToJsDocReturns(this DocComment docComment)
    {
        ArgumentNullException.ThrowIfNull(argument: docComment);

        return ConvertInlineTags(text: docComment.Returns);
    }

    private static string ConvertInlineTags(string? text)
    {
        if (string.IsNullOrEmpty(value: text))
        {
            return string.Empty;
        }

        var result = Regex.Replace(
            input: text,
            pattern: SeeWithContentPattern,
            evaluator: ReplaceSeeWithContent,
            options: RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline,
            matchTimeout: RegexTimeout);
        result = Regex.Replace(
            input: result,
            pattern: SeeSelfClosingPattern,
            evaluator: ReplaceSeeSelfClosing,
            options: RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            matchTimeout: RegexTimeout);
        result = Regex.Replace(
            input: result,
            pattern: ReferencePattern,
            evaluator: static match => $"`{match.Groups[groupname: "val"].Value}`",
            options: RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            matchTimeout: RegexTimeout);
        result = Regex.Replace(
            input: result,
            pattern: CodePattern,
            evaluator: static match => $"`{match.Groups[groupname: "val"].Value}`",
            options: RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline,
            matchTimeout: RegexTimeout);
        result = Regex.Replace(
            input: result,
            pattern: ParaPattern,
            replacement: " ",
            options: RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            matchTimeout: RegexTimeout);
        result = Regex.Replace(
            input: result,
            pattern: MultiSpacePattern,
            replacement: " ",
            options: RegexOptions.CultureInvariant,
            matchTimeout: RegexTimeout);
        return result.Trim();
    }

    private static string ReplaceSeeSelfClosing(Match match)
    {
        var attribute = match.Groups[groupname: "attr"].Value;
        var value = match.Groups[groupname: "val"].Value;
        if (attribute.Equals(value: "cref", comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            return $"{{@link {SimpleCrefName(cref: value)}}}";
        }

        return attribute.Equals(value: "href", comparisonType: StringComparison.OrdinalIgnoreCase)
            ? $"{{@link {value}}}"
            : $"`{value}`";
    }

    private static string ReplaceSeeWithContent(Match match)
    {
        var attribute = match.Groups[groupname: "attr"].Value;
        var value = match.Groups[groupname: "val"].Value;
        var inner = match.Groups[groupname: "text"].Value.Trim();
        var target = attribute.Equals(value: "cref", comparisonType: StringComparison.OrdinalIgnoreCase)
            ? SimpleCrefName(cref: value)
            : value;
        return string.IsNullOrEmpty(value: inner)
            ? $"{{@link {target}}}"
            : $"{{@link {target} | {inner}}}";
    }

    private static string SimpleCrefName(string cref)
    {
        var value = cref;
        if (value.Length > 2 && value[1] == ':')
        {
            value = value[2..];
        }

        var parenIndex = value.IndexOf(value: '(');
        if (parenIndex >= 0)
        {
            value = value[..parenIndex];
        }

        var dotIndex = value.LastIndexOf(value: '.');
        if (dotIndex >= 0 && dotIndex < value.Length - 1)
        {
            value = value[(dotIndex + 1)..];
        }

        return value;
    }
}
