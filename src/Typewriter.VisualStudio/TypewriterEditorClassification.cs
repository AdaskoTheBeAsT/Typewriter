using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Windows.Media;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace Typewriter.VisualStudio;

internal static class TypewriterEditorContentTypes
{
    public const string ContentTypeName = "typewriter";

    [Export]
    [Name(name: ContentTypeName)]
    [BaseDefinition(name: "code")]
    [BaseDefinition(name: "code-languageserver-base")]
    internal static ContentTypeDefinition TypewriterContentTypeDefinition = null!;

    [Export]
    [FileExtension(fileExtension: ".tst")]
    [ContentType(name: ContentTypeName)]
    internal static FileExtensionToContentTypeDefinition TypewriterFileExtensionDefinition = null!;
}

internal static class TypewriterClassificationNames
{
    public const string Directive = "typewriter.directive";
    public const string Collection = "typewriter.collection";
    public const string Member = "typewriter.member";
    public const string BlockDelimiter = "typewriter.blockDelimiter";

    [Export]
    [Name(name: Directive)]
    [BaseDefinition(name: "text")]
    internal static ClassificationTypeDefinition DirectiveClassificationType = null!;

    [Export]
    [Name(name: Collection)]
    [BaseDefinition(name: "identifier")]
    internal static ClassificationTypeDefinition CollectionClassificationType = null!;

    [Export]
    [Name(name: Member)]
    [BaseDefinition(name: "identifier")]
    internal static ClassificationTypeDefinition MemberClassificationType = null!;

    [Export]
    [Name(name: BlockDelimiter)]
    [BaseDefinition(name: "operator")]
    internal static ClassificationTypeDefinition BlockDelimiterClassificationType = null!;
}

[Export(contractType: typeof(EditorFormatDefinition))]
[ClassificationType(ClassificationTypeNames = TypewriterClassificationNames.Directive)]
[Name(name: TypewriterClassificationNames.Directive)]
[UserVisible(userVisible: true)]
internal sealed class TypewriterDirectiveFormatDefinition : ClassificationFormatDefinition
{
    public TypewriterDirectiveFormatDefinition()
    {
        DisplayName = "Typewriter Directive";
        ForegroundColor = Colors.SeaGreen;
        IsBold = true;
    }
}

[Export(contractType: typeof(EditorFormatDefinition))]
[ClassificationType(ClassificationTypeNames = TypewriterClassificationNames.Collection)]
[Name(name: TypewriterClassificationNames.Collection)]
[UserVisible(userVisible: true)]
internal sealed class TypewriterCollectionFormatDefinition : ClassificationFormatDefinition
{
    public TypewriterCollectionFormatDefinition()
    {
        DisplayName = "Typewriter Collection";
        ForegroundColor = Colors.Teal;
        IsBold = true;
    }
}

[Export(contractType: typeof(EditorFormatDefinition))]
[ClassificationType(ClassificationTypeNames = TypewriterClassificationNames.Member)]
[Name(name: TypewriterClassificationNames.Member)]
[UserVisible(userVisible: true)]
internal sealed class TypewriterMemberFormatDefinition : ClassificationFormatDefinition
{
    public TypewriterMemberFormatDefinition()
    {
        DisplayName = "Typewriter Member";
        ForegroundColor = Colors.DarkCyan;
    }
}

[Export(contractType: typeof(EditorFormatDefinition))]
[ClassificationType(ClassificationTypeNames = TypewriterClassificationNames.BlockDelimiter)]
[Name(name: TypewriterClassificationNames.BlockDelimiter)]
[UserVisible(userVisible: true)]
internal sealed class TypewriterBlockDelimiterFormatDefinition : ClassificationFormatDefinition
{
    public TypewriterBlockDelimiterFormatDefinition()
    {
        DisplayName = "Typewriter C# Block Delimiter";
        ForegroundColor = Colors.MediumVioletRed;
        IsBold = true;
    }
}

[Export(contractType: typeof(IClassifierProvider))]
[ContentType(name: TypewriterEditorContentTypes.ContentTypeName)]
internal sealed class TypewriterClassifierProvider : IClassifierProvider
{
    private readonly IClassificationTypeRegistryService _classificationRegistry;

    [ImportingConstructor]
    public TypewriterClassifierProvider(IClassificationTypeRegistryService classificationRegistry)
    {
        _classificationRegistry = classificationRegistry;
    }

    public IClassifier GetClassifier(ITextBuffer textBuffer) =>
        textBuffer.Properties.GetOrCreateSingletonProperty(
            creator: () => new TypewriterClassifier(classificationRegistry: _classificationRegistry));
}

internal sealed class TypewriterClassifier : IClassifier
{
    private static readonly HashSet<string> CollectionNames = new(comparer: StringComparer.Ordinal)
    {
        "Classes",
        "Records",
        "Structs",
        "Interfaces",
        "Enums",
        "Properties",
        "Methods",
        "Parameters",
        "Constants",
        "Values",
        "EnumValues",
        "Fields",
        "StaticReadOnlyFields",
        "Events",
        "Delegates",
    };

    private static readonly HashSet<string> CSharpKeywords = new(comparer: StringComparer.Ordinal)
    {
        "bool",
        "break",
        "case",
        "char",
        "const",
        "else",
        "foreach",
        "for",
        "if",
        "IEnumerable",
        "internal",
        "int",
        "List",
        "long",
        "private",
        "protected",
        "public",
        "return",
        "static",
        "struct",
        "string",
        "switch",
        "Template",
        "using",
        "void",
        "while",
    };

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

    private readonly IClassificationType _directive;
    private readonly IClassificationType _collection;
    private readonly IClassificationType _member;
    private readonly IClassificationType _blockDelimiter;
    private readonly IClassificationType _comment;
    private readonly IClassificationType _keyword;
    private readonly IClassificationType _string;

    public TypewriterClassifier(IClassificationTypeRegistryService classificationRegistry)
    {
        _directive = GetClassificationType(registry: classificationRegistry, classificationName: TypewriterClassificationNames.Directive, fallbackName: "text");
        _collection = GetClassificationType(registry: classificationRegistry, classificationName: TypewriterClassificationNames.Collection, fallbackName: "identifier");
        _member = GetClassificationType(registry: classificationRegistry, classificationName: TypewriterClassificationNames.Member, fallbackName: "identifier");
        _blockDelimiter = GetClassificationType(registry: classificationRegistry, classificationName: TypewriterClassificationNames.BlockDelimiter, fallbackName: "operator");
        _comment = GetClassificationType(registry: classificationRegistry, classificationName: "comment", fallbackName: "text");
        _keyword = GetClassificationType(registry: classificationRegistry, classificationName: "keyword", fallbackName: "text");
        _string = GetClassificationType(registry: classificationRegistry, classificationName: "string", fallbackName: "text");
    }

    public event EventHandler<ClassificationChangedEventArgs> ClassificationChanged
    {
        add { }
        remove { }
    }

    public IList<ClassificationSpan> GetClassificationSpans(SnapshotSpan span)
    {
        var classifications = new List<ClassificationSpan>();
        if (span.IsEmpty)
        {
            return classifications;
        }

        var endPosition = span.End.Position;
        var text = span.Snapshot.GetText(startIndex: 0, length: endPosition);
        var position = 0;
        var inCSharpBlock = false;

        while (position < text.Length)
        {
            if (inCSharpBlock)
            {
                if (position < text.Length && text[index: position] == '}')
                {
                    AddClassification(classifications: classifications, requestedSpan: span, start: position, end: position + 1, classificationType: _blockDelimiter);
                    position++;
                    inCSharpBlock = false;
                }

                position = ClassifyCSharpBlock(text: text, position: position, span: span, classifications: classifications);
                continue;
            }

            if (IsLineStart(text: text, position: position)
                && TryGetDirectiveLineEnd(text: text, lineStart: position, lineEnd: out var directiveEnd))
            {
                AddClassification(classifications: classifications, requestedSpan: span, start: position, end: directiveEnd, classificationType: _directive);
                position = directiveEnd;
                continue;
            }

            if (StartsWith(text: text, position: position, value: "//"))
            {
                var lineEnd = FindLineEnd(text: text, position: position);
                AddClassification(classifications: classifications, requestedSpan: span, start: position, end: lineEnd, classificationType: _comment);
                position = lineEnd;
                continue;
            }

            if (StartsWith(text: text, position: position, value: "/*"))
            {
                var commentEnd = FindBlockEnd(text: text, position: position + 2, terminator: "*/");
                AddClassification(classifications: classifications, requestedSpan: span, start: position, end: commentEnd, classificationType: _comment);
                position = commentEnd;
                continue;
            }

            if (StartsWith(text: text, position: position, value: "${") && IsCSharpBlockStart(text: text, position: position + 2))
            {
                AddClassification(classifications: classifications, requestedSpan: span, start: position, end: position + 2, classificationType: _blockDelimiter);
                position += 2;
                inCSharpBlock = true;
                continue;
            }

            if (text[index: position] == '$'
                && position + 1 < text.Length
                && IsIdentifierStart(value: text[index: position + 1]))
            {
                var nameStart = position + 1;
                var nameEnd = ReadIdentifier(text: text, position: nameStart);
                var classificationType = CollectionNames.Contains(item: text.Substring(startIndex: nameStart, length: nameEnd - nameStart))
                    ? _collection
                    : _member;
                AddClassification(classifications: classifications, requestedSpan: span, start: position, end: nameEnd, classificationType: classificationType);
                position = nameEnd;
                continue;
            }

            position++;
        }

        return classifications;
    }

    private int ClassifyCSharpBlock(
        string text,
        int position,
        SnapshotSpan span,
        ICollection<ClassificationSpan> classifications)
    {
        if (StartsWith(text: text, position: position, value: "//"))
        {
            var lineEnd = FindLineEnd(text: text, position: position);
            AddClassification(classifications: classifications, requestedSpan: span, start: position, end: lineEnd, classificationType: _comment);
            return lineEnd;
        }

        if (StartsWith(text: text, position: position, value: "/*"))
        {
            var commentEnd = FindBlockEnd(text: text, position: position + 2, terminator: "*/");
            AddClassification(classifications: classifications, requestedSpan: span, start: position, end: commentEnd, classificationType: _comment);
            return commentEnd;
        }

        if (text[index: position] == '"')
        {
            var stringEnd = ReadStringLiteral(text: text, position: position);
            AddClassification(classifications: classifications, requestedSpan: span, start: position, end: stringEnd, classificationType: _string);
            return stringEnd;
        }

        if (text[index: position] == '#')
        {
            var directiveEnd = ReadPreprocessorDirective(text: text, position: position);
            AddClassification(classifications: classifications, requestedSpan: span, start: position, end: directiveEnd, classificationType: _keyword);
            return directiveEnd;
        }

        if (IsIdentifierStart(value: text[index: position]))
        {
            var nameEnd = ReadIdentifier(text: text, position: position);
            if (CSharpKeywords.Contains(item: text.Substring(startIndex: position, length: nameEnd - position)))
            {
                AddClassification(classifications: classifications, requestedSpan: span, start: position, end: nameEnd, classificationType: _keyword);
            }

            return nameEnd;
        }

        return position + 1;
    }

    private static IClassificationType GetClassificationType(
        IClassificationTypeRegistryService registry,
        string classificationName,
        string fallbackName) =>
        registry.GetClassificationType(type: classificationName)
        ?? registry.GetClassificationType(type: fallbackName)
        ?? throw new InvalidOperationException(message: "Visual Studio classification registry did not provide a text classification.");

    private static bool TryGetDirectiveLineEnd(
        string text,
        int lineStart,
        out int lineEnd)
    {
        lineEnd = FindLineEnd(text: text, position: lineStart);
        var position = lineStart;
        while (position < lineEnd && (text[index: position] == ' ' || text[index: position] == '\t'))
        {
            position++;
        }

        if (!StartsWith(text: text, position: position, value: "//"))
        {
            return false;
        }

        position += 2;
        while (position < lineEnd && (text[index: position] == ' ' || text[index: position] == '\t'))
        {
            position++;
        }

        if (!TryReadDirectiveName(text: text, position: position, lineEnd: lineEnd, end: out var directiveEnd))
        {
            return false;
        }

        while (directiveEnd < lineEnd && (text[index: directiveEnd] == ' ' || text[index: directiveEnd] == '\t'))
        {
            directiveEnd++;
        }

        return directiveEnd < lineEnd && text[index: directiveEnd] == ':';
    }

    private static bool TryReadDirectiveName(
        string text,
        int position,
        int lineEnd,
        out int end)
    {
        string[] names = ["output", "typewriter-output", "typewriter-template"];
        foreach (var name in names)
        {
            end = position + name.Length;
            if (end <= lineEnd && string.Compare(strA: text, indexA: position, strB: name, indexB: 0, length: name.Length, comparisonType: StringComparison.OrdinalIgnoreCase) == 0)
            {
                return true;
            }
        }

        end = position;
        return false;
    }

    private static bool IsCSharpBlockStart(
        string text,
        int position)
    {
        while (position < text.Length && char.IsWhiteSpace(c: text[index: position]))
        {
            position++;
        }

        foreach (var prefix in CSharpBlockPrefixes)
        {
            if (StartsWith(text: text, position: position, value: prefix))
            {
                return true;
            }
        }

        return false;
    }

    private static int ReadPreprocessorDirective(
        string text,
        int position)
    {
        var end = position + 1;
        while (end < text.Length && IsIdentifierPart(value: text[index: end]))
        {
            end++;
        }

        return end;
    }

    private static int ReadStringLiteral(
        string text,
        int position)
    {
        var end = position + 1;
        var escaped = false;
        while (end < text.Length)
        {
            var current = text[index: end++];
            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (current == '\\')
            {
                escaped = true;
                continue;
            }

            if (current == '"')
            {
                break;
            }
        }

        return end;
    }

    private static int ReadIdentifier(
        string text,
        int position)
    {
        var end = position + 1;
        while (end < text.Length && IsIdentifierPart(value: text[index: end]))
        {
            end++;
        }

        return end;
    }

    private static int FindBlockEnd(
        string text,
        int position,
        string terminator)
    {
        var terminatorPosition = text.IndexOf(value: terminator, startIndex: position, comparisonType: StringComparison.Ordinal);
        return terminatorPosition < 0
            ? text.Length
            : terminatorPosition + terminator.Length;
    }

    private static int FindLineEnd(
        string text,
        int position)
    {
        var lineFeedPosition = text.IndexOf(value: '\n', startIndex: position);
        return lineFeedPosition < 0
            ? text.Length
            : lineFeedPosition;
    }

    private static bool IsLineStart(
        string text,
        int position) =>
        position == 0 || text[index: position - 1] == '\n';

    private static bool StartsWith(
        string text,
        int position,
        string value) =>
        position + value.Length <= text.Length
        && string.Compare(strA: text, indexA: position, strB: value, indexB: 0, length: value.Length, comparisonType: StringComparison.Ordinal) == 0;

    private static bool IsIdentifierStart(char value) =>
        char.IsLetter(c: value) || value == '_';

    private static bool IsIdentifierPart(char value) =>
        char.IsLetterOrDigit(c: value) || value == '_';

    private static void AddClassification(
        ICollection<ClassificationSpan> classifications,
        SnapshotSpan requestedSpan,
        int start,
        int end,
        IClassificationType classificationType)
    {
        var classificationStart = Math.Max(val1: start, val2: requestedSpan.Start.Position);
        var classificationEnd = Math.Min(val1: end, val2: requestedSpan.End.Position);
        if (classificationEnd <= classificationStart)
        {
            return;
        }

        classifications.Add(
            item: new ClassificationSpan(
                span: new SnapshotSpan(
                    snapshot: requestedSpan.Snapshot,
                    start: classificationStart,
                    length: classificationEnd - classificationStart),
                classification: classificationType));
    }
}
