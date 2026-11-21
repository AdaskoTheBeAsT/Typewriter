using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Typewriter.VisualStudio;

internal static class CSharpBlockScanner
{
    private const string PreprocessorSymbol = "TYPEWRITER";

    private static readonly string[] CompatibilityPrefixes =
    [
        "using ",
        "#r ",
        "#reference ",
        "#load ",
        "Template",
        "public ",
        "private ",
        "internal ",
        "protected ",
        "static ",
        "const ",
        "bool ",
        "string ",
        "char ",
        "int ",
        "long ",
        "void ",
        "List<",
        "IEnumerable<",
    ];

    private static readonly string[] PartialCompatibilityPrefixes =
    [
        "boo",
        "cha",
        "con",
        "int",
        "lon",
        "pri",
        "pro",
        "pub",
        "sta",
        "str",
        "usi",
        "voi",
    ];

    public static bool IsCSharpBlock(
        string block,
        bool allowPartial = false)
    {
        if (block is null)
        {
            return false;
        }

        var startIndex = SkipLeadingTrivia(block: block);
        if (startIndex >= block.Length)
        {
            return false;
        }

        var lineEnd = block.IndexOfAny(anyOf: ['\r', '\n'], startIndex: startIndex);
        var firstLine = lineEnd < 0
            ? block.Substring(startIndex: startIndex)
            : block.Substring(startIndex: startIndex, length: lineEnd - startIndex);
        return StartsWithPreprocessorIfDirective(line: firstLine)
            || CompatibilityPrefixes.Any(predicate: prefix => firstLine.StartsWith(value: prefix, comparisonType: StringComparison.Ordinal))
            || (allowPartial && PartialCompatibilityPrefixes.Any(predicate: prefix => firstLine.StartsWith(value: prefix, comparisonType: StringComparison.Ordinal)));
    }

    public static string StripDisabledPreprocessorText(string code)
    {
        if (string.IsNullOrEmpty(value: code) || code.IndexOf(value: '#') < 0)
        {
            return code ?? string.Empty;
        }

        var result = new StringBuilder(capacity: code.Length);
        var frames = new Stack<PreprocessorFrame>();
        var index = 0;
        while (index < code.Length)
        {
            var newLineIndex = code.IndexOf(value: '\n', startIndex: index);
            var lineEnd = newLineIndex < 0 ? code.Length : newLineIndex;
            var contentEnd = lineEnd > index && code[index: lineEnd - 1] == '\r'
                ? lineEnd - 1
                : lineEnd;
            var line = code.Substring(startIndex: index, length: contentEnd - index);
            index = newLineIndex < 0 ? code.Length : newLineIndex + 1;

            if (TryHandlePreprocessorDirective(line: line, frames: frames))
            {
                result.Append(value: '\n');
                continue;
            }

            if (frames.Count == 0 || frames.Peek().CurrentActive)
            {
                result.Append(value: line);
            }

            result.Append(value: '\n');
        }

        return result.ToString();
    }

    public static bool IsInsideCSharpBlock(
        string text,
        int position)
    {
        if (text is null)
        {
            return false;
        }

        var index = 0;
        while (index < text.Length)
        {
            var start = text.IndexOf(value: "${", startIndex: index, comparisonType: StringComparison.Ordinal);
            if (start < 0)
            {
                return false;
            }

            var end = FindBalancedEnd(content: text, openIndex: start + 1);
            if (end < 0)
            {
                return false;
            }

            if (position >= start
                && position <= end + 1
                && IsCSharpBlock(
                    block: text.Substring(startIndex: start + 2, length: end - start - 2),
                    allowPartial: IsStandaloneBlockStart(text: text, dollarIndex: start)))
            {
                return true;
            }

            index = end + 1;
        }

        return false;
    }

    public static IEnumerable<string> GetHelperNames(string text)
    {
        var helpers = new List<string>();
        if (string.IsNullOrEmpty(value: text))
        {
            return helpers;
        }

        var index = 0;
        while (index < text.Length)
        {
            var start = text.IndexOf(value: "${", startIndex: index, comparisonType: StringComparison.Ordinal);
            if (start < 0)
            {
                break;
            }

            var end = FindBalancedEnd(content: text, openIndex: start + 1);
            var contentStart = start + 2;
            var contentEnd = end < 0 ? text.Length : end;
            var block = text.Substring(startIndex: contentStart, length: contentEnd - contentStart);
            if (!IsCSharpBlock(
                    block: block,
                    allowPartial: IsStandaloneBlockStart(text: text, dollarIndex: start)))
            {
                index = start + 2;
                continue;
            }

            var activeBlock = StripDisabledPreprocessorText(code: block);
            foreach (Match match in Regex.Matches(
                         input: activeBlock,
                         pattern: @"(?:public\s+|private\s+|internal\s+|protected\s+|static\s+)*[A-Za-z_][A-Za-z0-9_<>,\.\s\?]*\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\(",
                         options: RegexOptions.CultureInvariant))
            {
                var name = match.Groups[groupname: "name"].Value;
                if (name is not "Template" and not "if" and not "for" and not "foreach" and not "while" and not "switch")
                {
                    helpers.Add(item: name);
                }
            }

            index = end < 0 ? text.Length : end + 1;
        }

        return helpers;
    }

    public static int FindBalancedEnd(
        string content,
        int openIndex)
    {
        if (content is null
            || openIndex < 0
            || openIndex >= content.Length
            || content[index: openIndex] != '{')
        {
            return -1;
        }

        return ScanBalancedBraces(content: content, startIndex: openIndex + 1);
    }

    private static int SkipLeadingTrivia(string block)
    {
        var index = 0;
        while (index < block.Length)
        {
            while (index < block.Length && char.IsWhiteSpace(c: block[index: index]))
            {
                index++;
            }

            if (StartsWith(content: block, index: index, value: "//"))
            {
                index = SkipSingleLineComment(content: block, index: index + 2);
                continue;
            }

            if (StartsWith(content: block, index: index, value: "/*"))
            {
                index = SkipMultiLineComment(content: block, index: index + 2);
                continue;
            }

            break;
        }

        return index;
    }

    private static int ScanBalancedBraces(
        string content,
        int startIndex)
    {
        var depth = 1;
        var index = startIndex;
        while (index < content.Length)
        {
            if (TrySkipNonCodeConstruct(content: content, index: ref index))
            {
                continue;
            }

            var current = content[index: index];
            if (current == '{')
            {
                depth++;
            }
            else if (current == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return index;
                }
            }

            index++;
        }

        return -1;
    }

    private static bool TrySkipNonCodeConstruct(
        string content,
        ref int index)
    {
        if (StartsWith(content: content, index: index, value: "//"))
        {
            index = SkipSingleLineComment(content: content, index: index + 2);
            return true;
        }

        if (StartsWith(content: content, index: index, value: "/*"))
        {
            index = SkipMultiLineComment(content: content, index: index + 2);
            return true;
        }

        if (content[index: index] == '\'')
        {
            index = SkipCharacterLiteral(content: content, index: index + 1);
            return true;
        }

        return TrySkipStringLiteral(content: content, index: ref index);
    }

    private static bool TrySkipStringLiteral(
        string content,
        ref int index)
    {
        var current = content[index: index];
        if (current == '@')
        {
            return TrySkipAtPrefixedString(content: content, index: ref index);
        }

        if (current == '$')
        {
            return TrySkipDollarPrefixedString(content: content, index: ref index);
        }

        if (current != '"')
        {
            return false;
        }

        var quoteCount = CountConsecutive(content: content, index: index, character: '"');
        index = quoteCount >= 3
            ? SkipRawString(content: content, index: index + quoteCount, quoteCount: quoteCount)
            : SkipRegularString(content: content, index: index + 1, isInterpolated: false);
        return true;
    }

    private static bool TrySkipAtPrefixedString(
        string content,
        ref int index)
    {
        if (StartsWith(content: content, index: index, value: "@\""))
        {
            index = SkipVerbatimString(content: content, index: index + 2, isInterpolated: false);
            return true;
        }

        if (StartsWith(content: content, index: index, value: "@$\""))
        {
            index = SkipVerbatimString(content: content, index: index + 3, isInterpolated: true);
            return true;
        }

        return false;
    }

    private static bool TrySkipDollarPrefixedString(
        string content,
        ref int index)
    {
        if (StartsWith(content: content, index: index, value: "$@\""))
        {
            index = SkipVerbatimString(content: content, index: index + 3, isInterpolated: true);
            return true;
        }

        var dollarCount = CountConsecutive(content: content, index: index, character: '$');
        var quoteStart = index + dollarCount;
        if (quoteStart >= content.Length || content[index: quoteStart] != '"')
        {
            return false;
        }

        var quoteCount = CountConsecutive(content: content, index: quoteStart, character: '"');
        if (quoteCount >= 3)
        {
            index = SkipInterpolatedRawString(
                content: content,
                index: quoteStart + quoteCount,
                dollarCount: dollarCount,
                quoteCount: quoteCount);
            return true;
        }

        if (dollarCount != 1)
        {
            return false;
        }

        index = SkipRegularString(content: content, index: quoteStart + 1, isInterpolated: true);
        return true;
    }

    private static int SkipSingleLineComment(
        string content,
        int index)
    {
        while (index < content.Length && content[index: index] is not '\r' and not '\n')
        {
            index++;
        }

        return index;
    }

    private static int SkipMultiLineComment(
        string content,
        int index)
    {
        var end = content.IndexOf(value: "*/", startIndex: index, comparisonType: StringComparison.Ordinal);
        return end < 0 ? content.Length : end + 2;
    }

    private static int SkipCharacterLiteral(
        string content,
        int index)
    {
        while (index < content.Length)
        {
            var current = content[index: index];
            if (current == '\\')
            {
                index += index + 1 < content.Length ? 2 : 1;
                continue;
            }

            if (current == '\'')
            {
                return index + 1;
            }

            if (current is '\r' or '\n')
            {
                return index;
            }

            index++;
        }

        return content.Length;
    }

    private static int SkipRegularString(
        string content,
        int index,
        bool isInterpolated)
    {
        while (index < content.Length)
        {
            var current = content[index: index];
            if (current == '\\')
            {
                index += index + 1 < content.Length ? 2 : 1;
                continue;
            }

            if (current == '"')
            {
                return index + 1;
            }

            if (isInterpolated
                && (current == '{' || current == '}')
                && TrySkipInterpolatedBrace(content: content, index: ref index))
            {
                continue;
            }

            index++;
        }

        return content.Length;
    }

    private static int SkipVerbatimString(
        string content,
        int index,
        bool isInterpolated)
    {
        while (index < content.Length)
        {
            var current = content[index: index];
            if (current == '"')
            {
                if (index + 1 < content.Length && content[index: index + 1] == '"')
                {
                    index += 2;
                    continue;
                }

                return index + 1;
            }

            if (isInterpolated
                && (current == '{' || current == '}')
                && TrySkipInterpolatedBrace(content: content, index: ref index))
            {
                continue;
            }

            index++;
        }

        return content.Length;
    }

    private static bool TrySkipInterpolatedBrace(
        string content,
        ref int index)
    {
        var current = content[index: index];
        if (index + 1 < content.Length && content[index: index + 1] == current)
        {
            index += 2;
            return true;
        }

        if (current != '{')
        {
            return false;
        }

        var holeEnd = ScanBalancedBraces(content: content, startIndex: index + 1);
        index = holeEnd < 0 ? content.Length : holeEnd + 1;
        return true;
    }

    private static int SkipRawString(
        string content,
        int index,
        int quoteCount)
    {
        while (index < content.Length)
        {
            if (content[index: index] == '"')
            {
                var quotes = CountConsecutive(content: content, index: index, character: '"');
                if (quotes >= quoteCount)
                {
                    return index + quotes;
                }

                index += quotes;
                continue;
            }

            index++;
        }

        return content.Length;
    }

    private static int SkipInterpolatedRawString(
        string content,
        int index,
        int dollarCount,
        int quoteCount)
    {
        while (index < content.Length)
        {
            var current = content[index: index];
            if (current == '"')
            {
                var quotes = CountConsecutive(content: content, index: index, character: '"');
                if (quotes >= quoteCount)
                {
                    return index + quotes;
                }

                index += quotes;
                continue;
            }

            if (current == '{')
            {
                index = SkipRawInterpolationBraces(content: content, index: index, dollarCount: dollarCount);
                continue;
            }

            index++;
        }

        return content.Length;
    }

    private static int SkipRawInterpolationBraces(
        string content,
        int index,
        int dollarCount)
    {
        var braces = CountConsecutive(content: content, index: index, character: '{');
        if (braces < dollarCount)
        {
            return index + braces;
        }

        var holeEnd = ScanBalancedBraces(content: content, startIndex: index + braces);
        if (holeEnd < 0)
        {
            return content.Length;
        }

        var nextIndex = holeEnd + 1;
        var extraCloseBraces = Math.Min(
            val1: dollarCount - 1,
            val2: CountConsecutive(content: content, index: nextIndex, character: '}'));
        return nextIndex + extraCloseBraces;
    }

    private static int CountConsecutive(
        string content,
        int index,
        char character)
    {
        var cursor = index;
        while (cursor < content.Length && content[index: cursor] == character)
        {
            cursor++;
        }

        return cursor - index;
    }

    private static bool IsStandaloneBlockStart(
        string text,
        int dollarIndex)
    {
        for (var index = dollarIndex - 1; index >= 0 && text[index: index] is not '\r' and not '\n'; index--)
        {
            if (!char.IsWhiteSpace(c: text[index: index]))
            {
                return false;
            }
        }

        for (var index = dollarIndex + 2; index < text.Length && text[index: index] is not '\r' and not '\n'; index++)
        {
            if (!char.IsWhiteSpace(c: text[index: index]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool StartsWithPreprocessorIfDirective(string line)
    {
        if (line.Length == 0 || line[index: 0] != '#')
        {
            return false;
        }

        var index = 1;
        while (index < line.Length && line[index: index] is ' ' or '\t')
        {
            index++;
        }

        return index + 2 < line.Length
            && line[index: index] == 'i'
            && line[index: index + 1] == 'f'
            && line[index: index + 2] is ' ' or '\t' or '(' or '!';
    }

    private static bool TryHandlePreprocessorDirective(
        string line,
        Stack<PreprocessorFrame> frames)
    {
        var index = 0;
        while (index < line.Length && line[index: index] is ' ' or '\t')
        {
            index++;
        }

        if (index >= line.Length || line[index: index] != '#')
        {
            return false;
        }

        index++;
        while (index < line.Length && line[index: index] is ' ' or '\t')
        {
            index++;
        }

        var keywordStart = index;
        while (index < line.Length && line[index: index] is >= 'a' and <= 'z')
        {
            index++;
        }

        if (index < line.Length && (char.IsLetterOrDigit(c: line[index: index]) || line[index: index] == '_'))
        {
            return false;
        }

        var keyword = line.Substring(startIndex: keywordStart, length: index - keywordStart);
        var rest = line.Substring(startIndex: index);
        var isActive = frames.Count == 0 || frames.Peek().CurrentActive;

        switch (keyword)
        {
            case "if":
                var taken = isActive && EvaluatePreprocessorExpression(expression: rest);
                frames.Push(item: new PreprocessorFrame(parentActive: isActive, branchTaken: taken, currentActive: taken));
                return true;

            case "elif":
                if (frames.Count > 0)
                {
                    var top = frames.Pop();
                    var elifTaken = top.ParentActive && !top.BranchTaken && EvaluatePreprocessorExpression(expression: rest);
                    frames.Push(item: new PreprocessorFrame(parentActive: top.ParentActive, branchTaken: top.BranchTaken || elifTaken, currentActive: elifTaken));
                }

                return true;

            case "else":
                if (frames.Count > 0)
                {
                    var top = frames.Pop();
                    var elseTaken = top.ParentActive && !top.BranchTaken;
                    frames.Push(item: new PreprocessorFrame(parentActive: top.ParentActive, branchTaken: true, currentActive: elseTaken));
                }

                return true;

            case "endif":
                if (frames.Count > 0)
                {
                    _ = frames.Pop();
                }

                return true;

            default:
                return false;
        }
    }

    private static bool EvaluatePreprocessorExpression(string expression)
    {
        var commentIndex = expression.IndexOf(value: "//", comparisonType: StringComparison.Ordinal);
        var text = commentIndex >= 0
            ? expression.Substring(startIndex: 0, length: commentIndex)
            : expression;
        var parser = new PreprocessorExpressionParser(text: text);
        return parser.ParseOr();
    }

    private static bool StartsWith(
        string content,
        int index,
        string value) =>
        index + value.Length <= content.Length
        && string.Compare(strA: content, indexA: index, strB: value, indexB: 0, length: value.Length, comparisonType: StringComparison.Ordinal) == 0;

    private readonly struct PreprocessorFrame
    {
        public PreprocessorFrame(
            bool parentActive,
            bool branchTaken,
            bool currentActive)
        {
            ParentActive = parentActive;
            BranchTaken = branchTaken;
            CurrentActive = currentActive;
        }

        public bool ParentActive { get; }

        public bool BranchTaken { get; }

        public bool CurrentActive { get; }
    }

    private struct PreprocessorExpressionParser
    {
        private readonly string _text;
        private int _index;

        public PreprocessorExpressionParser(string text)
        {
            _text = text;
            _index = 0;
        }

        public bool ParseOr()
        {
            var value = ParseAnd();
            while (Consume(token: "||"))
            {
                var right = ParseAnd();
                value = value || right;
            }

            return value;
        }

        private bool ParseAnd()
        {
            var value = ParseEquality();
            while (Consume(token: "&&"))
            {
                var right = ParseEquality();
                value = value && right;
            }

            return value;
        }

        private bool ParseEquality()
        {
            var value = ParseUnary();
            while (true)
            {
                if (Consume(token: "=="))
                {
                    var right = ParseUnary();
                    value = value == right;
                }
                else if (Consume(token: "!="))
                {
                    var right = ParseUnary();
                    value = value != right;
                }
                else
                {
                    break;
                }
            }

            return value;
        }

        private bool ParseUnary()
        {
            SkipWhitespace();
            if (Consume(token: "!"))
            {
                return !ParseUnary();
            }

            if (Consume(token: "("))
            {
                var inner = ParseOr();
                _ = Consume(token: ")");
                return inner;
            }

            var start = _index;
            if (_index < _text.Length && (char.IsLetter(c: _text[index: _index]) || _text[index: _index] == '_'))
            {
                _index++;
                while (_index < _text.Length && (char.IsLetterOrDigit(c: _text[index: _index]) || _text[index: _index] == '_'))
                {
                    _index++;
                }
            }

            var identifier = _text.Substring(startIndex: start, length: _index - start);
            return string.Equals(a: identifier, b: PreprocessorSymbol, comparisonType: StringComparison.Ordinal)
                || string.Equals(a: identifier, b: "true", comparisonType: StringComparison.Ordinal);
        }

        private void SkipWhitespace()
        {
            while (_index < _text.Length && char.IsWhiteSpace(c: _text[index: _index]))
            {
                _index++;
            }
        }

        private bool Consume(string token)
        {
            SkipWhitespace();
            if (StartsWith(content: _text, index: _index, value: token))
            {
                _index += token.Length;
                return true;
            }

            return false;
        }
    }
}
