using System.Text;
using System.Text.RegularExpressions;
using Typewriter.Abstractions;

namespace Typewriter.Engine;

public sealed record TemplateDocument(
    string Path,
    string Content,
    string? OutputPath)
{
    private const string ExpressionGroupName = "expression";
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(seconds: 1);

    internal IReadOnlyDictionary<string, TemplateCompatibilityMethod> CompatibilityMethods { get; init; } =
        new Dictionary<string, TemplateCompatibilityMethod>(comparer: StringComparer.Ordinal);

    internal IReadOnlyList<TemplateCodeBlock> CodeBlocks { get; init; } = [];

    public static TemplateDocument Parse(
        TemplateFile template,
        ICollection<GenerationDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(argument: template);
        ArgumentNullException.ThrowIfNull(argument: diagnostics);

        var codeBlocks = FindCompatibilityCodeBlocks(content: template.Content).ToArray();
        var compatibilityMethods = FindCompatibilityMethods(codeBlocks: codeBlocks);
        var outputPath = FindOutputDirective(content: template.Content);
        var contentWithoutDirectives = StripDirectiveLines(content: template.Content);
        var content = StripCompatibilityBlocks(
            path: template.Path,
            content: contentWithoutDirectives,
            diagnostics: diagnostics,
            outputPath: ref outputPath);

        return new TemplateDocument(
            Path: template.Path,
            Content: content.TrimStart('\uFEFF', '\r', '\n'),
            OutputPath: outputPath)
        {
            CompatibilityMethods = compatibilityMethods,
            CodeBlocks = codeBlocks,
        };
    }

    private static string? FindOutputDirective(string content)
    {
        using var reader = new StringReader(s: content);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            var trimmed = line.Trim();
            var value = ReadDirective(line: trimmed, prefix: "// output:")
                ?? ReadDirective(line: trimmed, prefix: "// typewriter-output:");

            if (!string.IsNullOrWhiteSpace(value: value))
            {
                return value;
            }
        }

        return null;
    }

    private static string? ReadDirective(string line, string prefix)
    {
        if (!line.StartsWith(value: prefix, comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return line[prefix.Length..].Trim();
    }

    private static string StripDirectiveLines(string content)
    {
        using var reader = new StringReader(s: content);
        var result = new StringBuilder(capacity: content.Length);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith(value: "// output:", comparisonType: StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith(value: "// typewriter-output:", comparisonType: StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith(value: "// typewriter-template:", comparisonType: StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            result.Append(value: line).Append(value: '\n');
        }

        return result.ToString();
    }

    private static string StripCompatibilityBlocks(
        string path,
        string content,
        ICollection<GenerationDiagnostic> diagnostics,
        ref string? outputPath)
    {
        var result = new StringBuilder(capacity: content.Length);
        for (var index = 0; index < content.Length; index++)
        {
            if (content[index: index] == '$'
                && index + 1 < content.Length
                && content[index: index + 1] == '{')
            {
                var end = FindBalancedEnd(content: content, openIndex: index + 1, open: '{', close: '}');
                if (end < 0)
                {
                    diagnostics.Add(
                        item: new GenerationDiagnostic(
                            File: path,
                            Line: null,
                            Column: null,
                            Severity: DiagnosticSeverity.Error,
                            Message: "Template code block is not closed.",
                            Code: DiagnosticCodes.TemplateParseError));
                    return result.ToString();
                }

                var blockContent = content.Substring(startIndex: index + 2, length: end - index - 2);
                if (!IsCompatibilityCodeBlock(block: blockContent))
                {
                    result.Append(value: content[index: index]);
                    continue;
                }

                var block = content.Substring(startIndex: index, length: end - index + 1);
                outputPath ??= FindSingleFileMode(block: block);
#pragma warning disable S127 // "for" loop stop conditions should be invariant
                index = end;
#pragma warning restore S127 // "for" loop stop conditions should be invariant
                continue;
            }

            result.Append(value: content[index: index]);
        }

        return result.ToString();
    }

    private static IReadOnlyDictionary<string, TemplateCompatibilityMethod> FindCompatibilityMethods(IEnumerable<TemplateCodeBlock> codeBlocks)
    {
        var methods = new Dictionary<string, TemplateCompatibilityMethod>(comparer: StringComparer.Ordinal);
        foreach (var block in codeBlocks.Select(selector: codeBlock => codeBlock.Content))
        {
            AddExpressionBodiedMethods(block: block, methods: methods);
            AddReturnStatementMethods(block: block, methods: methods);
        }

        return methods;
    }

    private static IEnumerable<TemplateCodeBlock> FindCompatibilityCodeBlocks(string content)
    {
        for (var index = 0; index < content.Length; index++)
        {
            if (content[index: index] != '$'
                || index + 1 >= content.Length
                || content[index: index + 1] != '{')
            {
                continue;
            }

            var end = FindBalancedEnd(content: content, openIndex: index + 1, open: '{', close: '}');
            if (end < 0)
            {
                yield break;
            }

            var blockContent = content.Substring(startIndex: index + 2, length: end - index - 2);
            if (!IsCompatibilityCodeBlock(block: blockContent))
            {
                continue;
            }

            yield return new TemplateCodeBlock(
                Content: blockContent,
                StartLine: GetLineNumber(content: content, index: index));
#pragma warning disable S127 // "for" loop stop conditions should be invariant
            index = end;
#pragma warning restore S127 // "for" loop stop conditions should be invariant
        }
    }

    private static int GetLineNumber(
        string content,
        int index)
    {
        var line = 1;
        for (var cursor = 0; cursor < index && cursor < content.Length; cursor++)
        {
            if (content[index: cursor] == '\n')
            {
                line++;
            }
        }

        return line;
    }

    private static bool IsCompatibilityCodeBlock(string block)
    {
        var trimmed = block.TrimStart();
        if (trimmed.Length == 0)
        {
            return false;
        }

        var firstLineEnd = trimmed.IndexOfAny(anyOf: ['\r', '\n']);
        var firstLine = firstLineEnd < 0 ? trimmed : trimmed[..firstLineEnd];
        return firstLine.StartsWith(value: "using ", comparisonType: StringComparison.Ordinal)
            || firstLine.StartsWith(value: "#r ", comparisonType: StringComparison.Ordinal)
            || firstLine.StartsWith(value: "#reference ", comparisonType: StringComparison.Ordinal)
            || firstLine.StartsWith(value: "#load ", comparisonType: StringComparison.Ordinal)
            || firstLine.StartsWith(value: "Template", comparisonType: StringComparison.Ordinal)
            || firstLine.StartsWith(value: "public ", comparisonType: StringComparison.Ordinal)
            || firstLine.StartsWith(value: "private ", comparisonType: StringComparison.Ordinal)
            || firstLine.StartsWith(value: "internal ", comparisonType: StringComparison.Ordinal)
            || firstLine.StartsWith(value: "protected ", comparisonType: StringComparison.Ordinal)
            || firstLine.StartsWith(value: "static ", comparisonType: StringComparison.Ordinal)
            || firstLine.StartsWith(value: "const ", comparisonType: StringComparison.Ordinal)
            || firstLine.StartsWith(value: "bool ", comparisonType: StringComparison.Ordinal)
            || firstLine.StartsWith(value: "string ", comparisonType: StringComparison.Ordinal)
            || firstLine.StartsWith(value: "char ", comparisonType: StringComparison.Ordinal)
            || firstLine.StartsWith(value: "int ", comparisonType: StringComparison.Ordinal)
            || firstLine.StartsWith(value: "long ", comparisonType: StringComparison.Ordinal)
            || firstLine.StartsWith(value: "void ", comparisonType: StringComparison.Ordinal)
            || firstLine.StartsWith(value: "List<", comparisonType: StringComparison.Ordinal)
            || firstLine.StartsWith(value: "IEnumerable<", comparisonType: StringComparison.Ordinal);
    }

    private static void AddExpressionBodiedMethods(
        string block,
        IDictionary<string, TemplateCompatibilityMethod> methods)
    {
#pragma warning disable SA1118 // Parameter should not span multiple lines
        foreach (Match match in Regex.Matches(
                     input: block,
                     pattern: @"(?:public\s+|private\s+|internal\s+|protected\s+|static\s+)*" +
                              @"(?<returnType>bool|string)\s+" +
                              "(?<name>[A-Za-z_][A-Za-z0-9_]*)\\s*" +
                              "\\(\\s*(?<parameterType>[A-Za-z_][A-Za-z0-9_<>.,\\s]*)\\s+" +
                              "(?<parameterName>[A-Za-z_][A-Za-z0-9_]*)\\s*\\)\\s*=>\\s*" +
                              "(?<expression>[^;]+);",
                     options: RegexOptions.CultureInvariant,
                     matchTimeout: RegexTimeout))
        {
            AddMethod(match: match, methods: methods);
        }
#pragma warning restore SA1118 // Parameter should not span multiple lines
    }

    private static void AddReturnStatementMethods(
        string block,
        IDictionary<string, TemplateCompatibilityMethod> methods)
    {
#pragma warning disable SA1118 // Parameter should not span multiple lines
        foreach (Match match in Regex.Matches(
                     input: block,
                     pattern: @"(?:public\s+|private\s+|internal\s+|protected\s+|static\s+)*" +
                              @"(?<returnType>bool|string)\s+" +
                              @"(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*" +
                              @"\(\s*(?<parameterType>[A-Za-z_][A-Za-z0-9_<>.,\s]*)\s+" +
                              @"(?<parameterName>[A-Za-z_][A-Za-z0-9_]*)\s*\)\s*" +
                              @"\{",
                     options: RegexOptions.CultureInvariant,
                     matchTimeout: RegexTimeout))
        {
            var openBrace = match.Index + match.Length - 1;
            var closeBrace = FindBalancedEnd(content: block, openIndex: openBrace, open: '{', close: '}');
            if (closeBrace < 0)
            {
                continue;
            }

            var body = block.Substring(startIndex: openBrace + 1, length: closeBrace - openBrace - 1);
            var returnMatch = Regex.Match(
                input: body,
                pattern: @"return\s+(?<expression>[^;]+);",
                options: RegexOptions.CultureInvariant | RegexOptions.Singleline,
                matchTimeout: RegexTimeout);
            if (!returnMatch.Success)
            {
                continue;
            }

            var returnStatements = Regex.Matches(
                input: body,
                pattern: @"return\s+(?<expression>[^;]+);",
                options: RegexOptions.CultureInvariant | RegexOptions.Singleline,
                matchTimeout: RegexTimeout);
            var expression = returnStatements.Count == 1
                && !body.Contains(value: "if", comparisonType: StringComparison.Ordinal)
                && !body.Contains(value: "foreach", comparisonType: StringComparison.Ordinal)
                    ? returnMatch.Groups[groupname: ExpressionGroupName].Value
                    : null;

            AddMethod(match: match, methods: methods, expression: expression, body: body);
        }
#pragma warning restore SA1118 // Parameter should not span multiple lines
    }

    private static void AddMethod(
        Match match,
        IDictionary<string, TemplateCompatibilityMethod> methods,
        string? expression = null,
        string? body = null)
    {
        var returnType = match.Groups[groupname: "returnType"].Value;
        var kind = returnType.Equals(value: "bool", comparisonType: StringComparison.Ordinal)
            ? TemplateCompatibilityMethodKind.Predicate
            : TemplateCompatibilityMethodKind.String;
        var matchedExpression = match.Groups[groupname: ExpressionGroupName].Success
            ? match.Groups[groupname: ExpressionGroupName].Value
            : null;
        var methodExpression = expression ?? matchedExpression;
        var method = new TemplateCompatibilityMethod(
            Name: match.Groups[groupname: "name"].Value,
            Kind: kind,
            ParameterName: match.Groups[groupname: "parameterName"].Value,
            Expression: methodExpression,
            Body: body ?? methodExpression ?? string.Empty);
        methods[key: method.Name] = method;
    }

    private static int FindBalancedEnd(
        string content,
        int openIndex,
        char open,
        char close)
    {
        var depth = 0;
        for (var index = openIndex; index < content.Length; index++)
        {
            if (content[index: index] == open)
            {
                depth++;
            }
            else if (content[index: index] == close)
            {
                depth--;
                if (depth == 0)
                {
                    return index;
                }
            }
        }

        return -1;
    }

    private static string? FindSingleFileMode(string block)
    {
        const string marker = "SingleFileMode";
        var markerIndex = block.IndexOf(value: marker, comparisonType: StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            return null;
        }

        var openQuote = block.IndexOf(value: '"', startIndex: markerIndex);
        if (openQuote < 0)
        {
            return null;
        }

        var closeQuote = block.IndexOf(value: '"', startIndex: openQuote + 1);
        return closeQuote < 0
            ? null
            : block.Substring(startIndex: openQuote + 1, length: closeQuote - openQuote - 1);
    }
}
