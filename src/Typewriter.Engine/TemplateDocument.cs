using System.Runtime.CompilerServices;
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
    private static readonly ConditionalWeakTable<TemplateDocument, IReadOnlyList<TemplateInlinePredicate>> InlinePredicateCache = [];
    private static readonly ConditionalWeakTable<TemplateDocument, TemplateSourceIndex> SourceIndexCache = [];
    private static readonly ConditionalWeakTable<TemplateDocument, IReadOnlyDictionary<(string CollectionName, string Filter), string>> InlinePredicateMethodCache = [];

    internal IReadOnlyDictionary<string, TemplateCompatibilityMethod> CompatibilityMethods { get; init; } =
        new Dictionary<string, TemplateCompatibilityMethod>(comparer: StringComparer.Ordinal);

    internal IReadOnlyList<TemplateCodeBlock> CodeBlocks { get; init; } = [];

    internal bool HasCompiledCode => CodeBlocks.Count > 0 || InlinePredicates.Count > 0;

    internal TemplateSourceIndex SourceIndex =>
        SourceIndexCache.GetValue(key: this, createValueCallback: static template => new TemplateSourceIndex(content: template.Content));

    internal IReadOnlyDictionary<(string CollectionName, string Filter), string> InlinePredicateMethods =>
        InlinePredicateMethodCache.GetValue(
            key: this,
            createValueCallback: static template => template.InlinePredicates
                .GroupBy(keySelector: predicate => (predicate.CollectionName, predicate.Filter))
                .ToDictionary(keySelector: group => group.Key, elementSelector: group => group.First().MethodName));

    internal IReadOnlyList<TemplateInlinePredicate> InlinePredicates =>
        InlinePredicateCache.GetValue(
            key: this,
            createValueCallback: template => TemplateInlinePredicateScanner.Find(
                content: template.InlinePredicateSource is { } source && source.Content.Equals(value: template.Content, comparisonType: StringComparison.Ordinal)
                    ? source.OriginalContent
                    : template.Content));

    private (string Content, string OriginalContent)? InlinePredicateSource { get; init; }

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

        var renderContent = content.TrimStart('\uFEFF', '\r', '\n');
        return new TemplateDocument(
            Path: template.Path,
            Content: renderContent,
            OutputPath: outputPath)
        {
            CompatibilityMethods = compatibilityMethods,
            CodeBlocks = codeBlocks,
            InlinePredicateSource = (renderContent, StripDirectiveLines(content: template.Content, preserveLines: true)),
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

    private static string StripDirectiveLines(
        string content,
        bool preserveLines = false)
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
                if (preserveLines)
                {
                    result.Append(value: '\n');
                }

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
        var ends = new Dictionary<int, int>();
        for (var index = 0; index < content.Length; index++)
        {
            if (content[index: index] == '$'
                && index + 1 < content.Length
                && content[index: index + 1] == '{')
            {
                if (!ends.TryGetValue(key: index + 1, value: out var end))
                {
                    end = TemplateCodeBlockScanner.IndexBalancedEnd(content: content, openIndex: index + 1, ends: ends);
                }

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

                if (!TemplateCodeBlockScanner.IsCompatibilityCodeBlock(block: content.AsSpan(start: index + 2, length: end - index - 2)))
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
        var lines = new TemplateLineMap(content: content);
        var ends = new Dictionary<int, int>();
        for (var index = 0; index < content.Length; index++)
        {
            if (content[index: index] != '$'
                || index + 1 >= content.Length
                || content[index: index + 1] != '{')
            {
                continue;
            }

            if (!ends.TryGetValue(key: index + 1, value: out var end))
            {
                end = TemplateCodeBlockScanner.IndexBalancedEnd(content: content, openIndex: index + 1, ends: ends);
            }

            if (end < 0)
            {
                yield break;
            }

            if (!TemplateCodeBlockScanner.IsCompatibilityCodeBlock(block: content.AsSpan(start: index + 2, length: end - index - 2)))
            {
                continue;
            }

            yield return new TemplateCodeBlock(
                Content: content.Substring(startIndex: index + 2, length: end - index - 2),
                StartLine: lines.GetPosition(index: index).Line);
#pragma warning disable S127 // "for" loop stop conditions should be invariant
            index = end;
#pragma warning restore S127 // "for" loop stop conditions should be invariant
        }
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
                     options: RegexOptions.CultureInvariant | RegexOptions.NonBacktracking,
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
        var ends = new Dictionary<int, int>();
#pragma warning disable SA1118 // Parameter should not span multiple lines
        foreach (Match match in Regex.Matches(
                     input: block,
                     pattern: @"(?:public\s+|private\s+|internal\s+|protected\s+|static\s+)*" +
                              @"(?<returnType>bool|string)\s+" +
                              @"(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*" +
                              @"\(\s*(?<parameterType>[A-Za-z_][A-Za-z0-9_<>.,\s]*)\s+" +
                              @"(?<parameterName>[A-Za-z_][A-Za-z0-9_]*)\s*\)\s*" +
                              @"\{",
                     options: RegexOptions.CultureInvariant | RegexOptions.NonBacktracking,
                     matchTimeout: RegexTimeout))
        {
            var openBrace = match.Index + match.Length - 1;
            if (!ends.TryGetValue(key: openBrace, value: out var closeBrace))
            {
                closeBrace = TemplateCodeBlockScanner.IndexBalancedEnd(content: block, openIndex: openBrace, ends: ends);
            }

            if (closeBrace < 0)
            {
                continue;
            }

            var body = block.Substring(startIndex: openBrace + 1, length: closeBrace - openBrace - 1);
            var returnStatements = Regex.Matches(
                input: body,
                pattern: @"return\s+(?<expression>[^;]+);",
                options: RegexOptions.CultureInvariant | RegexOptions.Singleline | RegexOptions.NonBacktracking,
                matchTimeout: RegexTimeout);
            if (returnStatements.Count == 0)
            {
                continue;
            }

            var expression = returnStatements.Count == 1
                && !body.Contains(value: "if", comparisonType: StringComparison.Ordinal)
                && !body.Contains(value: "foreach", comparisonType: StringComparison.Ordinal)
                    ? returnStatements[0].Groups[groupname: ExpressionGroupName].Value
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

    private static string? FindSingleFileMode(string block)
    {
        const string marker = "SingleFileMode";
        var markerIndex = block.IndexOf(value: marker, comparisonType: StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            return null;
        }

        var openQuote = block.IndexOfAny(anyOf: ['"', '\''], startIndex: markerIndex);
        if (openQuote < 0)
        {
            return null;
        }

        var closeQuote = block.IndexOf(value: block[index: openQuote], startIndex: openQuote + 1);
        return closeQuote < 0
            ? null
            : block.Substring(startIndex: openQuote + 1, length: closeQuote - openQuote - 1);
    }
}
