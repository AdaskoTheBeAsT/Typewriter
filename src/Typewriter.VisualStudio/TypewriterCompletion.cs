using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;

namespace Typewriter.VisualStudio;

[Export(contractType: typeof(ICompletionSourceProvider))]
[ContentType(name: TypewriterEditorContentTypes.ContentTypeName)]
[Name(name: "Typewriter Completion")]
internal sealed class TypewriterCompletionSourceProvider : ICompletionSourceProvider
{
    public ICompletionSource TryCreateCompletionSource(ITextBuffer textBuffer) =>
        new TypewriterCompletionSource(textBuffer: textBuffer);
}

internal sealed class TypewriterCompletionSource : ICompletionSource
{
    private static readonly string[] TemplateCollections =
    [
        "Types",
        "Classes",
        "Records",
        "Structs",
        "Interfaces",
        "Enums",
        "Properties",
        "Methods",
        "Parameters",
        "Constants",
    ];

    private static readonly string[] TemplateScalars =
    [
        "Name",
        "name",
        "FullName",
        "Namespace",
        "Type",
        "ReturnType",
        "Value",
        "DefaultValue",
        "Parent",
        "IsNullable",
        "IsStatic",
        "IsAbstract",
        "IsGeneric",
        "IsStruct",
        "IsIndexer",
        "IsCollection",
        "IsDictionary",
        "IsEnum",
        "IsPrimitive",
        "Default",
        "OriginalName",
        "ClassName",
    ];

    private static readonly string[] CSharpKeywords =
    [
        "bool",
        "break",
        "case",
        "class",
        "const",
        "continue",
        "else",
        "false",
        "foreach",
        "if",
        "int",
        "long",
        "new",
        "null",
        "private",
        "public",
        "return",
        "static",
        "struct",
        "string",
        "switch",
        "true",
        "typeof",
        "using",
        "var",
        "void",
    ];

    private static readonly string[] CodeModelTypes =
    [
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
        "Struct",
        "Type",
    ];

    private static readonly string[] TypeScriptKeywords =
    [
        "as",
        "async",
        "await",
        "boolean",
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
        "keyof",
        "let",
        "namespace",
        "new",
        "null",
        "number",
        "of",
        "private",
        "protected",
        "public",
        "readonly",
        "return",
        "satisfies",
        "set",
        "static",
        "string",
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
        "yield",
    ];

    private static readonly string[] TypeScriptTypes =
    [
        "any",
        "Array",
        "bigint",
        "Date",
        "Map",
        "never",
        "object",
        "Omit",
        "Partial",
        "Pick",
        "Promise",
        "Readonly",
        "Record",
        "Required",
        "Set",
        "symbol",
        "unknown",
    ];

    private static readonly string[] CSharpBlockPrefixes =
    [
        "using",
        "#r",
        "#reference",
        "Template",
        "public",
        "private",
        "internal",
        "protected",
        "static",
        "const",
        "bool",
        "string",
        "char",
        "int",
        "long",
        "void",
        "List<",
        "IEnumerable<",
    ];

    private readonly ITextBuffer _textBuffer;
    private bool _isDisposed;

    public TypewriterCompletionSource(ITextBuffer textBuffer)
    {
        _textBuffer = textBuffer;
    }

    public void AugmentCompletionSession(
        ICompletionSession session,
        IList<CompletionSet> completionSets)
    {
        if (_isDisposed)
        {
            return;
        }

        var snapshot = _textBuffer.CurrentSnapshot;
        var triggerPoint = session.GetTriggerPoint(textSnapshot: snapshot);
        if (!triggerPoint.HasValue)
        {
            return;
        }

        var position = Math.Max(val1: 0, val2: Math.Min(val1: triggerPoint.Value.Position, val2: snapshot.Length));
        var text = snapshot.GetText();
        var context = GetCompletionContext(text: text, position: position);
        var completions = context switch
        {
            CompletionContext.CSharp => CreateCSharpCompletions(text: text),
            CompletionContext.Template => CreateTemplateCompletions(includeDollar: false),
            _ => CreateTypeScriptCompletions()
                .Concat(second: CreateTemplateCompletions(includeDollar: true))
                .ToArray(),
        };

        if (completions.Count == 0)
        {
            return;
        }

        completionSets.Add(
            item: new CompletionSet(
                moniker: "Typewriter",
                displayName: "Typewriter",
                applicableTo: CreateApplicableToSpan(snapshot: snapshot, position: position),
                completions: completions,
                completionBuilders: null));
    }

    public void Dispose()
    {
        _isDisposed = true;
    }

    private static IReadOnlyList<Completion> CreateTemplateCompletions(bool includeDollar)
    {
        var completions = new List<Completion>();
        foreach (var collection in TemplateCollections)
        {
            completions.Add(
                item: CreateCompletion(
                    displayText: includeDollar ? "$" + collection : collection,
                    insertionText: includeDollar ? "$" + collection + "[]" : collection + "[]",
                    description: "Typewriter collection"));
        }

        foreach (var scalar in TemplateScalars)
        {
            completions.Add(
                item: CreateCompletion(
                    displayText: includeDollar ? "$" + scalar : scalar,
                    insertionText: includeDollar ? "$" + scalar : scalar,
                    description: "Typewriter member"));
        }

        return completions;
    }

    private static IReadOnlyList<Completion> CreateCSharpCompletions(string text) =>
        CSharpKeywords
            .Select(selector: keyword => CreateCompletion(displayText: keyword, insertionText: keyword, description: "C# keyword"))
            .Concat(second: CodeModelTypes.Select(selector: type => CreateCompletion(displayText: type, insertionText: type, description: "Typewriter.CodeModel type")))
            .Concat(second: GetHelperNames(text: text).Select(selector: helper => CreateCompletion(displayText: helper, insertionText: helper, description: "Template helper")))
            .GroupBy(keySelector: completion => completion.DisplayText, comparer: StringComparer.Ordinal)
            .Select(selector: group => group.First())
            .OrderBy(keySelector: completion => completion.DisplayText, comparer: StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static IReadOnlyList<Completion> CreateTypeScriptCompletions() =>
        TypeScriptKeywords
            .Select(selector: keyword => CreateCompletion(displayText: keyword, insertionText: keyword, description: "TypeScript keyword"))
            .Concat(second: TypeScriptTypes.Select(selector: type => CreateCompletion(displayText: type, insertionText: type, description: "TypeScript type")))
            .OrderBy(keySelector: completion => completion.DisplayText, comparer: StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static Completion CreateCompletion(
        string displayText,
        string insertionText,
        string description) =>
        new(displayText: displayText, insertionText: insertionText, description: description, iconSource: null, iconAutomationText: null);

    private static ITrackingSpan CreateApplicableToSpan(
        ITextSnapshot snapshot,
        int position)
    {
        var start = position;
        while (start > 0 && IsIdentifierPart(value: snapshot[position: start - 1]))
        {
            start--;
        }

        if (start > 0 && snapshot[position: start - 1] == '$')
        {
            start--;
        }

        return snapshot.CreateTrackingSpan(
            start: start,
            length: position - start,
            trackingMode: SpanTrackingMode.EdgeInclusive);
    }

    private static CompletionContext GetCompletionContext(
        string text,
        int position)
    {
        if (IsInsideCSharpBlock(text: text, position: position))
        {
            return CompletionContext.CSharp;
        }

        return IsTemplatePosition(text: text, position: position)
            ? CompletionContext.Template
            : CompletionContext.TypeScript;
    }

    private static bool IsTemplatePosition(
        string text,
        int position)
    {
        if (position > 0 && position <= text.Length && text[index: position - 1] == '$')
        {
            return true;
        }

        if (position >= 0 && position < text.Length && text[index: position] == '$')
        {
            return true;
        }

        var start = Math.Max(val1: 0, val2: Math.Min(val1: position, val2: text.Length) - 1);
        while (start >= 0 && IsIdentifierPart(value: text[index: start]))
        {
            start--;
        }

        return start >= 0 && text[index: start] == '$';
    }

    private static bool IsInsideCSharpBlock(
        string text,
        int position)
    {
        var index = 0;
        while (index < text.Length)
        {
            var start = text.IndexOf(value: "${", startIndex: index, comparisonType: StringComparison.Ordinal);
            if (start < 0)
            {
                return false;
            }

            var end = FindBalancedEnd(text: text, openIndex: start + 1, open: '{', close: '}');
            if (end < 0)
            {
                return false;
            }

            if (position >= start
                && position <= end + 1
                && IsCSharpBlock(block: text.Substring(startIndex: start + 2, length: end - start - 2), allowPartial: IsStandaloneBlockStart(text: text, dollarIndex: start)))
            {
                return true;
            }

            index = end + 1;
        }

        return false;
    }

    private static bool IsCSharpBlock(
        string block,
        bool allowPartial)
    {
        var trimmed = block.TrimStart();
        if (trimmed.Length == 0)
        {
            return false;
        }

        var firstLineEnd = trimmed.IndexOfAny(anyOf: ['\r', '\n']);
        var firstLine = firstLineEnd < 0 ? trimmed : trimmed.Substring(startIndex: 0, length: firstLineEnd);
        if (CSharpBlockPrefixes.Any(predicate: prefix => firstLine.StartsWith(value: prefix, comparisonType: StringComparison.Ordinal)))
        {
            return true;
        }

        return allowPartial
            && new[] { "boo", "cha", "con", "int", "lon", "pri", "pro", "pub", "sta", "str", "usi", "voi" }
                .Any(predicate: prefix => firstLine.StartsWith(value: prefix, comparisonType: StringComparison.Ordinal));
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

    private static int FindBalancedEnd(
        string text,
        int openIndex,
        char open,
        char close)
    {
        var depth = 0;
        for (var index = openIndex; index < text.Length; index++)
        {
            if (text[index: index] == open)
            {
                depth++;
            }
            else if (text[index: index] == close)
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

    private static IEnumerable<string> GetHelperNames(string text) =>
        Regex.Matches(
                input: text,
                pattern: @"(?:public\s+|private\s+|internal\s+|protected\s+|static\s+)*[A-Za-z_][A-Za-z0-9_<>,\.\s\?]*\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\(",
                options: RegexOptions.CultureInvariant)
            .Cast<Match>()
            .Select(selector: match => match.Groups[groupname: "name"].Value)
            .Where(predicate: name => name is not "Template" and not "if" and not "for" and not "foreach" and not "while" and not "switch");

    private static bool IsIdentifierPart(char value) =>
        char.IsLetterOrDigit(c: value) || value == '_';

    private enum CompletionContext
    {
        Template,
        CSharp,
        TypeScript,
    }
}
