using System.Globalization;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Typewriter.Engine;

internal static class TemplateInlinePredicateScanner
{
    public static IReadOnlyList<TemplateInlinePredicate> Find(string content)
    {
        var predicates = new List<TemplateInlinePredicate>();
        var methodNames = new Dictionary<(string CollectionName, string Filter), string>();
        var source = new TemplateSourceIndex(content: content);
        var pending = new Stack<(int Start, int End, string? ParentIdentifier)>();
        pending.Push(item: (0, content.Length, null));
        while (pending.TryPop(result: out var range))
        {
            Scan(source: source, start: range.Start, end: range.End, parentIdentifier: range.ParentIdentifier, predicates: predicates, methodNames: methodNames, pending: pending);
        }

        return predicates;
    }

    private static string? GetItemTypeName(
        string identifier,
        string? parentIdentifier,
        LambdaExpressionSyntax lambda)
    {
        if (lambda is ParenthesizedLambdaExpressionSyntax { ParameterList.Parameters: [var parameter] }
            && parameter.Type is not null)
        {
            return parameter.Type.ToString();
        }

        var typeName = identifier switch
        {
            "Classes" or "NestedClasses" => "Class",
            "Records" or "NestedRecords" => "Record",
            "Structs" or "NestedStructs" => "Struct",
            "Interfaces" or "NestedInterfaces" => "Interface",
            "Enums" or "NestedEnums" => "Enum",
            "Types" or "BaseTypes" or "TypeArguments" => "Type",
            "Methods" => "Method",
            "Properties" => "Property",
            "Parameters" when string.Equals(a: parentIdentifier, b: "DocComment", comparisonType: StringComparison.Ordinal) => "ParameterComment",
            "Parameters" => "Parameter",
            "TupleElements" or "Fields" => "Field",
            "Constants" => "Constant",
            "StaticReadOnlyFields" => "StaticReadOnlyField",
            "Events" => "Event",
            "Delegates" => "Delegate",
            "TypeParameters" => "TypeParameter",
            "Attributes" => "Attribute",
            "Arguments" => "AttributeArgument",
            "Values" => "EnumValue",
            "FileLocations" => "string",
            _ => null,
        };
        return typeName is null or "string" ? typeName : "global::Typewriter.CodeModel." + typeName;
    }

#pragma warning disable MA0051 // Method is too long
    private static void Scan(
        TemplateSourceIndex source,
        int start,
        int end,
        string? parentIdentifier,
        ICollection<TemplateInlinePredicate> predicates,
        IDictionary<(string CollectionName, string Filter), string> methodNames,
        Stack<(int Start, int End, string? ParentIdentifier)> pending)
    {
        var content = source.Content;
        var index = start;
        while (index < end)
        {
            if (content[index: index++] != '$' || index >= end)
            {
                continue;
            }

            if (content[index: index] == '$')
            {
                index++;
                continue;
            }

            if (content[index: index] == '{')
            {
                var codeEnd = source.FindBlockEnd(openIndex: index);
                index = codeEnd < 0 ? end : codeEnd + 1;
                continue;
            }

            var identifierStart = index;
            while (index < end && (char.IsLetterOrDigit(c: content[index: index]) || content[index: index] == '_'))
            {
                index++;
            }

            var identifier = content[identifierStart..index];
            if (index < end && content[index: index] == '(')
            {
                var filterEnd = source.FindBlockEnd(openIndex: index);
                if (filterEnd < 0)
                {
                    return;
                }

                AddPredicate(
                    source: source,
                    start: index + 1,
                    end: filterEnd,
                    identifier: identifier,
                    parentIdentifier: parentIdentifier,
                    predicates: predicates,
                    methodNames: methodNames);
                index = filterEnd + 1;
            }

            if (index < end && content[index: index] == '[')
            {
                var blockEnd = source.FindBlockEnd(openIndex: index);
                if (blockEnd < 0)
                {
                    return;
                }

                if (blockEnd + 1 < end)
                {
                    pending.Push(item: (blockEnd + 1, end, parentIdentifier));
                }

                pending.Push(item: (index + 1, blockEnd, identifier));
                return;
            }
        }
    }
#pragma warning restore MA0051 // Method is too long

    private static void AddPredicate(
        TemplateSourceIndex source,
        int start,
        int end,
        string identifier,
        string? parentIdentifier,
        ICollection<TemplateInlinePredicate> predicates,
        IDictionary<(string CollectionName, string Filter), string> methodNames)
    {
        var content = source.Content;
        var filter = content[start..end].Trim();
        if (!filter.Contains(value: "=>", comparisonType: StringComparison.Ordinal))
        {
            return;
        }

        var expression = SyntaxFactory.ParseExpression(text: filter);
        while (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized.Expression;
        }

        if (expression is not LambdaExpressionSyntax lambda)
        {
            return;
        }

        var key = (identifier, filter);
        if (!methodNames.TryGetValue(key: key, value: out var methodName))
        {
            methodName = "__TypewriterInlinePredicate_" + methodNames.Count.ToString(provider: CultureInfo.InvariantCulture);
            methodNames.Add(key: key, value: methodName);
        }

        while (start < end && char.IsWhiteSpace(c: content[index: start]))
        {
            start++;
        }

        var (line, column) = source.GetPosition(index: start);

        predicates.Add(
            item: new TemplateInlinePredicate(
                CollectionName: identifier,
                Filter: filter,
                MethodName: methodName,
                ItemTypeName: GetItemTypeName(identifier: identifier, parentIdentifier: parentIdentifier, lambda: lambda),
                Line: line,
                Column: column));
    }
}
