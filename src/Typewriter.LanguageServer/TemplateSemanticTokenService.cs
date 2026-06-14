using System.Collections.Immutable;

namespace Typewriter.LanguageServer;

internal sealed partial class TemplateSemanticTokenService
{
    public static readonly string[] TokenTypes =
    [
        "namespace",
        "type",
        "class",
        "enum",
        "interface",
        "parameter",
        "variable",
        "property",
        "function",
        "method",
        "macro",
        "keyword",
        "modifier",
        "comment",
        "string",
        "number",
        "operator",
    ];

    public static readonly string[] TokenModifiers =
    [
        "declaration",
        "static",
        "readonly",
    ];

    private const int NamespaceToken = 0;
    private const int TypeToken = 1;
    private const int ClassToken = 2;
    private const int EnumToken = 3;
    private const int InterfaceToken = 4;
    private const int ParameterToken = 5;
    private const int VariableToken = 6;
    private const int PropertyToken = 7;
    private const int FunctionToken = 8;
    private const int MethodToken = 9;
    private const int MacroToken = 10;
    private const int KeywordToken = 11;
    private const int ModifierToken = 12;
    private const int CommentToken = 13;
    private const int StringToken = 14;
    private const int NumberToken = 15;
    private const int OperatorToken = 16;

    private static readonly ImmutableHashSet<string> CSharpKeywords =
        ImmutableHashSet.Create(
            equalityComparer: StringComparer.Ordinal,
            "abstract",
            "as",
            "base",
            "break",
            "case",
            "catch",
            "checked",
            "class",
            "const",
            "continue",
            "default",
            "delegate",
            "do",
            "else",
            "enum",
            "event",
            "explicit",
            "extern",
            "false",
            "finally",
            "fixed",
            "for",
            "foreach",
            "goto",
            "if",
            "implicit",
            "in",
            "interface",
            "internal",
            "is",
            "lock",
            "namespace",
            "new",
            "null",
            "operator",
            "out",
            "override",
            "params",
            "private",
            "protected",
            "public",
            "readonly",
            "record",
            "ref",
            "return",
            "sealed",
            "sizeof",
            "stackalloc",
            "static",
            "struct",
            "switch",
            "this",
            "throw",
            "true",
            "try",
            "typeof",
            "unchecked",
            "unsafe",
            "using",
            "virtual",
            "void",
            "volatile",
            "while",
            "with");

    private static readonly ImmutableHashSet<string> CSharpBuiltInTypes =
        ImmutableHashSet.Create(
            equalityComparer: StringComparer.Ordinal,
            "bool",
            "byte",
            "char",
            "decimal",
            "double",
            "dynamic",
            "float",
            "int",
            "long",
            "object",
            "sbyte",
            "short",
            "string",
            "uint",
            "ulong",
            "ushort");

    private static readonly ImmutableHashSet<string> CSharpCodeModelTypes =
        ImmutableHashSet.Create(
            equalityComparer: StringComparer.Ordinal,
            "Attribute",
            "AttributeArgument",
            "Class",
            "Constant",
            "Delegate",
            "Enum",
            "EnumValue",
            "Field",
            "File",
            "Interface",
            "Method",
            "Parameter",
            "Property",
            "Record",
            "Settings",
            "Type");

    private static readonly ImmutableHashSet<string> TypeScriptKeywords =
        ImmutableHashSet.Create(
            equalityComparer: StringComparer.Ordinal,
            "abstract",
            "as",
            "async",
            "await",
            "break",
            "case",
            "catch",
            "class",
            "const",
            "continue",
            "declare",
            "default",
            "delete",
            "do",
            "else",
            "enum",
            "export",
            "extends",
            "false",
            "finally",
            "for",
            "from",
            "function",
            "get",
            "if",
            "implements",
            "import",
            "in",
            "instanceof",
            "interface",
            "let",
            "new",
            "null",
            "of",
            "private",
            "protected",
            "public",
            "readonly",
            "return",
            "set",
            "static",
            "super",
            "switch",
            "this",
            "throw",
            "true",
            "try",
            "type",
            "typeof",
            "undefined",
            "var",
            "void",
            "while",
            "with",
            "yield");

    private static readonly ImmutableHashSet<string> TypeScriptBuiltInTypes =
        ImmutableHashSet.Create(
            equalityComparer: StringComparer.Ordinal,
            "any",
            "bigint",
            "boolean",
            "never",
            "number",
            "object",
            "Record",
            "string",
            "symbol",
            "unknown",
            "void");

#pragma warning disable CC0091, S2325
    public LspSemanticTokens GetSemanticTokens(TextDocumentState document)
    {
        var builder = new SemanticTokenBuilder(text: document.Text);
        ScanDocument(text: document.Text, builder: builder);
        return new LspSemanticTokens(Data: builder.Build());
    }
#pragma warning restore CC0091, S2325

    private static void ScanDocument(
        string text,
        SemanticTokenBuilder builder)
    {
        for (var index = 0; index < text.Length;)
        {
            if (TemplateEmbeddedLanguage.TryReadCSharpBlock(text: text, dollarIndex: index, region: out var csharpRegion))
            {
                builder.Add(start: csharpRegion.Start, length: 2, tokenType: MacroToken);
                ScanLanguageRange(text: text, start: csharpRegion.ContentStart, end: csharpRegion.ContentEnd, language: EmbeddedLanguageKind.CSharp, builder: builder);
                builder.Add(start: csharpRegion.End - 1, length: 1, tokenType: MacroToken);
                index = csharpRegion.End;
                continue;
            }

            if (TemplateEmbeddedLanguage.IsTemplateTokenStart(text: text, index: index))
            {
                var end = TemplateEmbeddedLanguage.ReadTemplateTokenEnd(text: text, start: index);
                builder.Add(start: index, length: end - index, tokenType: MacroToken);
                index = end;
                continue;
            }

            var nextSpecial = FindNextSpecial(text: text, start: index + 1);
            ScanLanguageRange(text: text, start: index, end: nextSpecial, language: EmbeddedLanguageKind.TypeScript, builder: builder);
            index = nextSpecial;
        }
    }

    private static int FindNextSpecial(
        string text,
        int start)
    {
        for (var index = start; index < text.Length; index++)
        {
            if (TemplateEmbeddedLanguage.IsTemplateTokenStart(text: text, index: index)
                || TemplateEmbeddedLanguage.TryReadCSharpBlock(text: text, dollarIndex: index, region: out _))
            {
                return index;
            }
        }

        return text.Length;
    }

#pragma warning disable MA0051,S3776
    private static void ScanLanguageRange(
        string text,
        int start,
        int end,
        EmbeddedLanguageKind language,
        SemanticTokenBuilder builder)
#pragma warning restore MA0051,S3776
    {
        var index = start;
        while (index < end)
        {
            var current = text[index: index];
            if (char.IsWhiteSpace(c: current))
            {
                index++;
                continue;
            }

            if (current == '/' && index + 1 < end)
            {
                if (text[index: index + 1] == '/')
                {
                    var commentEnd = index + 2;
                    while (commentEnd < end && text[index: commentEnd] is not '\r' and not '\n')
                    {
                        commentEnd++;
                    }

                    builder.Add(start: index, length: commentEnd - index, tokenType: CommentToken);
                    index = commentEnd;
                    continue;
                }

                if (text[index: index + 1] == '*')
                {
                    var commentEnd = index + 2;
                    while (commentEnd + 1 < end
                           && (text[index: commentEnd] != '*' || text[index: commentEnd + 1] != '/'))
                    {
                        commentEnd++;
                    }

                    commentEnd = Math.Min(val1: commentEnd + 2, val2: end);
                    builder.Add(start: index, length: commentEnd - index, tokenType: CommentToken);
                    index = commentEnd;
                    continue;
                }
            }

            if (current is '"' or '\'' || (language == EmbeddedLanguageKind.TypeScript && current == '`'))
            {
                var stringEnd = ReadStringEnd(text: text, start: index, end: end, quote: current);
                builder.Add(start: index, length: stringEnd - index, tokenType: StringToken);
                index = stringEnd;
                continue;
            }

            if (char.IsDigit(c: current))
            {
                var numberEnd = index + 1;
                while (numberEnd < end && IsNumberPart(value: text[index: numberEnd]))
                {
                    numberEnd++;
                }

                builder.Add(start: index, length: numberEnd - index, tokenType: NumberToken);
                index = numberEnd;
                continue;
            }

            if (IsIdentifierStart(value: current))
            {
                var identifierEnd = index + 1;
                while (identifierEnd < end && IsIdentifierPart(value: text[index: identifierEnd]))
                {
                    identifierEnd++;
                }

                AddIdentifier(text: text, start: index, end: identifierEnd, language: language, builder: builder);
                index = identifierEnd;
                continue;
            }

            if (IsOperator(value: current))
            {
                builder.Add(start: index, length: 1, tokenType: OperatorToken);
            }

            index++;
        }
    }

    private static int ReadStringEnd(
        string text,
        int start,
        int end,
        char quote)
    {
        var index = start + 1;
        while (index < end)
        {
            if (text[index: index] is '\r' or '\n')
            {
                return index;
            }

            if (text[index: index] == '\\')
            {
                index += 2;
                continue;
            }

            if (text[index: index] == quote)
            {
                return index + 1;
            }

            index++;
        }

        return end;
    }

    private static void AddIdentifier(
        string text,
        int start,
        int end,
        EmbeddedLanguageKind language,
        SemanticTokenBuilder builder)
    {
        var identifier = text[start..end];
        if (language == EmbeddedLanguageKind.CSharp)
        {
            if (CSharpBuiltInTypes.Contains(item: identifier) || CSharpCodeModelTypes.Contains(item: identifier))
            {
                builder.Add(start: start, length: end - start, tokenType: TypeToken);
            }
            else if (CSharpKeywords.Contains(item: identifier))
            {
                builder.Add(start: start, length: end - start, tokenType: GetCSharpKeywordToken(identifier: identifier));
            }
            else if (IsNamespaceLike(identifier: identifier))
            {
                builder.Add(start: start, length: end - start, tokenType: NamespaceToken);
            }
            else if (IsFollowedBy(text: text, start: end, value: '('))
            {
                builder.Add(start: start, length: end - start, tokenType: MethodToken);
            }
            else if (IsParameterName(text: text, start: start, identifier: identifier))
            {
                builder.Add(start: start, length: end - start, tokenType: ParameterToken);
            }

            return;
        }

        if (TypeScriptBuiltInTypes.Contains(item: identifier))
        {
            builder.Add(start: start, length: end - start, tokenType: TypeToken);
        }
        else if (TypeScriptKeywords.Contains(item: identifier))
        {
            builder.Add(start: start, length: end - start, tokenType: GetTypeScriptKeywordToken(identifier: identifier));
        }
        else if (IsFollowedBy(text: text, start: end, value: '('))
        {
            builder.Add(start: start, length: end - start, tokenType: FunctionToken);
        }
        else if (IsAfterDot(text: text, start: start))
        {
            builder.Add(start: start, length: end - start, tokenType: PropertyToken);
        }
        else if (char.IsUpper(c: identifier[index: 0]))
        {
            builder.Add(start: start, length: end - start, tokenType: TypeToken);
        }
        else
        {
            builder.Add(start: start, length: end - start, tokenType: VariableToken);
        }
    }

    private static int GetCSharpKeywordToken(string identifier) =>
        identifier switch
        {
            "class" => ClassToken,
            "enum" => EnumToken,
            "interface" => InterfaceToken,
            "private" or "protected" or "public" or "internal" or "static" or "readonly" or "sealed" or "abstract" => ModifierToken,
            _ => KeywordToken,
        };

    private static int GetTypeScriptKeywordToken(string identifier) =>
        identifier switch
        {
            "class" => ClassToken,
            "enum" => EnumToken,
            "interface" => InterfaceToken,
            "private" or "protected" or "public" or "static" or "readonly" or "abstract" => ModifierToken,
            _ => KeywordToken,
        };

#pragma warning disable CC0021 // Use nameof
    private static bool IsNamespaceLike(string identifier) =>
        identifier is "System" or "Typewriter" or "CodeModel";
#pragma warning restore CC0021 // Use nameof

    private static bool IsParameterName(
        string text,
        int start,
        string identifier)
    {
        if (identifier.Length == 0 || identifier[index: 0] != '@')
        {
            return false;
        }

        _ = text;
        _ = start;
        return true;
    }

    private static bool IsFollowedBy(
        string text,
        int start,
        char value)
    {
        for (var index = start; index < text.Length; index++)
        {
            if (char.IsWhiteSpace(c: text[index: index]))
            {
                continue;
            }

            return text[index: index] == value;
        }

        return false;
    }

    private static bool IsAfterDot(
        string text,
        int start)
    {
        for (var index = start - 1; index >= 0; index--)
        {
            if (char.IsWhiteSpace(c: text[index: index]))
            {
                continue;
            }

            return text[index: index] == '.';
        }

        return false;
    }

    private static bool IsIdentifierStart(char value) =>
        char.IsLetter(c: value) || value == '_' || value == '@';

    private static bool IsIdentifierPart(char value) =>
        char.IsLetterOrDigit(c: value) || value is '_' or '@';

    private static bool IsNumberPart(char value) =>
        char.IsLetterOrDigit(c: value) || value is '.' or '_';

    private static bool IsOperator(char value) =>
        value is '+'
            or '-'
            or '*'
            or '/'
            or '%'
            or '='
            or '!'
            or '<'
            or '>'
            or '&'
            or '|'
            or '?'
            or ':'
            or '.';

    private sealed class SemanticTokenBuilder
    {
        private readonly string _text;
        private readonly int[] _lineStarts;
        private readonly List<RawSemanticToken> _tokens = [];

        public SemanticTokenBuilder(string text)
        {
            _text = text;
            _lineStarts = GetLineStarts(text: text);
        }

        public void Add(
            int start,
            int length,
            int tokenType,
            int tokenModifiers = 0)
        {
            if (length <= 0 || start < 0 || start >= _text.Length)
            {
                return;
            }

            var end = Math.Min(val1: start + length, val2: _text.Length);
            var index = start;
            while (index < end)
            {
                if (_text[index: index] is '\r' or '\n')
                {
                    index++;
                    continue;
                }

                var line = GetLine(offset: index);
                var lineEnd = Math.Min(val1: GetLineContentEnd(line: line), val2: end);
                if (lineEnd <= index)
                {
                    index++;
                    continue;
                }

                _tokens.Add(
                    item: new RawSemanticToken(
                        Line: line,
                        Start: index - _lineStarts[line],
                        Length: lineEnd - index,
                        TokenTypeIndex: tokenType,
                        TokenModifierMask: tokenModifiers));
                index = lineEnd;
            }
        }

        public int[] Build()
        {
            var data = new List<int>(capacity: _tokens.Count * 5);
            var previousLine = 0;
            var previousStart = 0;
            foreach (var token in _tokens
                         .OrderBy(keySelector: item => item.Line)
                         .ThenBy(keySelector: item => item.Start)
                         .ThenBy(keySelector: item => item.Length))
            {
                var deltaLine = token.Line - previousLine;
                var deltaStart = deltaLine == 0
                    ? token.Start - previousStart
                    : token.Start;
                if (deltaLine < 0 || deltaStart < 0)
                {
                    continue;
                }

                data.Add(item: deltaLine);
                data.Add(item: deltaStart);
                data.Add(item: token.Length);
                data.Add(item: token.TokenTypeIndex);
                data.Add(item: token.TokenModifierMask);
                previousLine = token.Line;
                previousStart = token.Start;
            }

            return [.. data];
        }

        private static int[] GetLineStarts(string text)
        {
            var starts = new List<int> { 0 };
            for (var index = 0; index < text.Length; index++)
            {
                if (text[index: index] == '\n' && index + 1 < text.Length)
                {
                    starts.Add(item: index + 1);
                }
            }

            return [.. starts];
        }

        private int GetLine(int offset)
        {
            var index = Array.BinarySearch(array: _lineStarts, value: offset);
            return index >= 0 ? index : ~index - 1;
        }

        private int GetLineContentEnd(int line)
        {
            var end = line + 1 < _lineStarts.Length
                ? _lineStarts[line + 1]
                : _text.Length;

            if (end > _lineStarts[line] && _text[index: end - 1] == '\n')
            {
                end--;
            }

            if (end > _lineStarts[line] && _text[index: end - 1] == '\r')
            {
                end--;
            }

            return end;
        }
    }
}
