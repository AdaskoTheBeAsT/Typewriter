using System.Collections.Concurrent;
using Typewriter.Abstractions;

namespace Typewriter.Engine;

public sealed class TypeScriptTypeMapper
{
    private readonly ConcurrentDictionary<MapCacheKey, string> _cache = new();

    public string Map(TypeMetadataReference type) =>
        Map(type: type, strictNull: true);

    public string Map(
        TypeMetadataReference type,
        bool strictNull)
    {
        ArgumentNullException.ThrowIfNull(argument: type);

        return _cache.GetOrAdd(
            key: new MapCacheKey(Type: type, StrictNull: strictNull),
            valueFactory: static (key, mapper) => mapper.MapUncached(type: key.Type, strictNull: key.StrictNull),
            factoryArgument: this);
    }

    private static bool IsBoolean(string fullName) =>
        fullName.Equals(value: "System.Boolean", comparisonType: StringComparison.Ordinal);

    private static bool IsTaskLike(string fullName) =>
        fullName.Equals(value: "System.Threading.Tasks.Task", comparisonType: StringComparison.Ordinal)
        || fullName.Equals(value: "System.Threading.Tasks.ValueTask", comparisonType: StringComparison.Ordinal);

    private static bool IsString(string fullName) =>
        fullName.Equals(value: "System.String", comparisonType: StringComparison.Ordinal)
        || fullName.Equals(value: "System.Char", comparisonType: StringComparison.Ordinal)
        || fullName.Equals(value: "System.Guid", comparisonType: StringComparison.Ordinal);

    private static bool IsNumber(string fullName) =>
        fullName.Equals(value: "System.Byte", comparisonType: StringComparison.Ordinal)
        || fullName.Equals(value: "System.SByte", comparisonType: StringComparison.Ordinal)
        || fullName.Equals(value: "System.Int16", comparisonType: StringComparison.Ordinal)
        || fullName.Equals(value: "System.UInt16", comparisonType: StringComparison.Ordinal)
        || fullName.Equals(value: "System.Int32", comparisonType: StringComparison.Ordinal)
        || fullName.Equals(value: "System.UInt32", comparisonType: StringComparison.Ordinal)
        || fullName.Equals(value: "System.Int64", comparisonType: StringComparison.Ordinal)
        || fullName.Equals(value: "System.UInt64", comparisonType: StringComparison.Ordinal)
        || fullName.Equals(value: "System.Single", comparisonType: StringComparison.Ordinal)
        || fullName.Equals(value: "System.Double", comparisonType: StringComparison.Ordinal)
        || fullName.Equals(value: "System.Decimal", comparisonType: StringComparison.Ordinal);

    private static string FormatArrayElementType(string mapped)
    {
        return HasTopLevelUnion(mapped: mapped)
            ? $"({mapped})"
            : mapped;
    }

    private static bool HasTopLevelNullUnion(string mapped)
    {
        return TryVisitTopLevelUnionSegments(mapped: mapped, segmentPredicate: IsNullSegment);
    }

    private static bool HasTopLevelUnion(string mapped)
    {
        return TryVisitTopLevelUnionSegments(mapped: mapped, segmentPredicate: static (_, _, _) => true);
    }

    private static bool TryVisitTopLevelUnionSegments(
        string mapped,
        Func<string, int, int, bool> segmentPredicate)
    {
        ArgumentNullException.ThrowIfNull(argument: segmentPredicate);

        var angleDepth = 0;
        var parenthesisDepth = 0;
        var bracketDepth = 0;
        var segmentStart = 0;
        var hasTopLevelUnion = false;

        for (var i = 0; i < mapped.Length; i++)
        {
            switch (mapped[index: i])
            {
                case '<':
                    angleDepth++;
                    break;
                case '>':
                    angleDepth = Math.Max(val1: 0, val2: angleDepth - 1);
                    break;
                case '(':
                    parenthesisDepth++;
                    break;
                case ')':
                    parenthesisDepth = Math.Max(val1: 0, val2: parenthesisDepth - 1);
                    break;
                case '[':
                    bracketDepth++;
                    break;
                case ']':
                    bracketDepth = Math.Max(val1: 0, val2: bracketDepth - 1);
                    break;
                case '|':
                    if (angleDepth == 0
                        && parenthesisDepth == 0
                        && bracketDepth == 0)
                    {
                        hasTopLevelUnion = true;
                        if (segmentPredicate(arg1: mapped, arg2: segmentStart, arg3: i))
                        {
                            return true;
                        }

                        segmentStart = i + 1;
                    }

                    break;
                default:
                    break;
            }
        }

        return hasTopLevelUnion
            && segmentPredicate(arg1: mapped, arg2: segmentStart, arg3: mapped.Length);
    }

    private static bool IsNullSegment(
        string mapped,
        int start,
        int end)
    {
        while (start < end && char.IsWhiteSpace(c: mapped[index: start]))
        {
            start++;
        }

        while (end > start && char.IsWhiteSpace(c: mapped[index: end - 1]))
        {
            end--;
        }

        return end - start == 4
            && mapped[index: start] == 'n'
            && mapped[index: start + 1] == 'u'
            && mapped[index: start + 2] == 'l'
            && mapped[index: start + 3] == 'l';
    }

    private string MapCore(
        TypeMetadataReference type,
        bool strictNull)
    {
        if (IsTaskLike(fullName: type.FullName))
        {
            return type.TypeArguments.Count > 0
                ? Map(type: type.TypeArguments[index: 0], strictNull: strictNull)
                : "void";
        }

        if (type.IsDictionary)
        {
            return MapDictionaryType(type: type, strictNull: strictNull);
        }

        if (type.IsCollection && type.ElementType is not null)
        {
            return $"{FormatArrayElementType(mapped: Map(type: type.ElementType, strictNull: strictNull))}[]";
        }

        if (type.IsEnum)
        {
            return type.Name;
        }

        if (type.IsDateLike)
        {
            return "string";
        }

        var fullName = type.FullName;
        if (IsBoolean(fullName: fullName))
        {
            return "boolean";
        }

        if (IsNumber(fullName: fullName))
        {
            return "number";
        }

        if (IsString(fullName: fullName))
        {
            return "string";
        }

        if (fullName.Equals(value: "System.Void", comparisonType: StringComparison.Ordinal))
        {
            return "void";
        }

        if (fullName.Equals(value: "dynamic", comparisonType: StringComparison.OrdinalIgnoreCase)
            || fullName.Equals(value: "System.Object", comparisonType: StringComparison.Ordinal))
        {
            return "unknown";
        }

        return type.TypeArguments.Count > 0
            ? MapGenericType(type: type, strictNull: strictNull)
            : type.Name;
    }

    private string MapDictionaryType(
        TypeMetadataReference type,
        bool strictNull)
    {
        var valueType = type.TypeArguments.Count > 1
            ? Map(type: type.TypeArguments[index: 1], strictNull: strictNull)
            : "unknown";

        return $"Record<string, {valueType}>";
    }

    private string MapGenericType(
        TypeMetadataReference type,
        bool strictNull)
    {
        var arguments = string.Join(
            separator: ", ",
            values: type.TypeArguments.Select(selector: argument => Map(type: argument, strictNull: strictNull)));
        return type.Name + "<" + arguments + ">";
    }

    private string MapUncached(
        TypeMetadataReference type,
        bool strictNull)
    {
        var mapped = MapCore(type: type, strictNull: strictNull);
        if (strictNull && type.IsNullable && !HasTopLevelNullUnion(mapped: mapped))
        {
            return $"{mapped} | null";
        }

        return mapped;
    }

    private sealed record MapCacheKey(
        TypeMetadataReference Type,
        bool StrictNull);
}
