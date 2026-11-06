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
        Scan(content: content, start: 0, end: content.Length, parentIdentifier: null, predicates: predicates, methodNames: methodNames);
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
        string content,
        int start,
        int end,
        string? parentIdentifier,
        ICollection<TemplateInlinePredicate> predicates,
        IDictionary<(string CollectionName, string Filter), string> methodNames)
    {
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
                var codeEnd = TemplateCodeBlockScanner.FindBalancedEnd(content: content, openIndex: index);
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
                var filterEnd = TemplateCodeBlockScanner.FindBalancedEnd(content: content, openIndex: index);
                if (filterEnd < 0)
                {
                    return;
                }

                AddPredicate(
                    content: content,
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
                var blockEnd = TemplateBlockReader.FindEnd(content: content, openIndex: index, open: '[', close: ']');
                if (blockEnd < 0)
                {
                    return;
                }

                Scan(content: content, start: index + 1, end: blockEnd, parentIdentifier: identifier, predicates: predicates, methodNames: methodNames);
                index = blockEnd + 1;
            }
        }
    }
#pragma warning restore MA0051 // Method is too long

    private static void AddPredicate(
        string content,
        int start,
        int end,
        string identifier,
        string? parentIdentifier,
        ICollection<TemplateInlinePredicate> predicates,
        IDictionary<(string CollectionName, string Filter), string> methodNames)
    {
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

        var line = 1;
        var lineStart = 0;
        for (var index = 0; index < start; index++)
        {
            if (content[index: index] == '\n')
            {
                line++;
                lineStart = index + 1;
            }
        }

        predicates.Add(
            item: new TemplateInlinePredicate(
                CollectionName: identifier,
                Filter: filter,
                MethodName: methodName,
                ItemTypeName: GetItemTypeName(identifier: identifier, parentIdentifier: parentIdentifier, lambda: lambda),
                Line: line,
                Column: start - lineStart + 1));
    }
}
