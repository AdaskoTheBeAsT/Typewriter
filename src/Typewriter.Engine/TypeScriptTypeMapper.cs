using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Typewriter.Abstractions;

namespace Typewriter.Engine;

public sealed class TypeScriptTypeMapper
{
    public const string DefaultDateType = OutputConfiguration.DefaultDateType;
    public const string DefaultDateInitializer = OutputConfiguration.DefaultDateInitializer;
    public const string DefaultDateOnlyType = OutputConfiguration.DefaultDateOnlyType;
    public const string DefaultDateOnlyInitializer = OutputConfiguration.DefaultDateOnlyInitializer;
    public const string DefaultTimeOnlyType = OutputConfiguration.DefaultTimeOnlyType;
    public const string DefaultTimeOnlyInitializer = OutputConfiguration.DefaultTimeOnlyInitializer;
    public const string DefaultDecimalType = OutputConfiguration.DefaultDecimalType;
    public const string DefaultDecimalInitializer = OutputConfiguration.DefaultDecimalInitializer;
    public const string DefaultGuidType = OutputConfiguration.DefaultGuidType;
    public const string DefaultGuidInitializer = OutputConfiguration.DefaultGuidInitializer;

    private readonly ConcurrentDictionary<MapCacheKey, string> _cache = new();
    private readonly ConcurrentDictionary<SemanticMapCacheKey, string> _semanticCache = new();

    public string Map(TypeMetadataReference type) =>
        Map(type: type, strictNull: true);

    public string Map(
        TypeMetadataReference type,
        bool strictNull) =>
        Map(
            type: type,
            strictNull: strictNull,
            dateType: DefaultDateType,
            decimalType: DefaultDecimalType,
            guidType: DefaultGuidType,
            dateOnlyType: DefaultDateOnlyType,
            timeOnlyType: DefaultTimeOnlyType);

    public string Map(
        TypeMetadataReference type,
        bool strictNull,
        string? dateType,
        string? decimalType = null,
        string? guidType = null,
        string? dateOnlyType = null,
        string? timeOnlyType = null)
    {
        ArgumentNullException.ThrowIfNull(argument: type);

        var normalizedDateType = NormalizeDateType(dateType: dateType);
        var normalizedDecimalType = NormalizeDecimalType(decimalType: decimalType);
        var normalizedGuidType = NormalizeGuidType(guidType: guidType);
        var normalizedDateOnlyType = NormalizeDateOnlyType(dateOnlyType: dateOnlyType);
        var normalizedTimeOnlyType = NormalizeTimeOnlyType(timeOnlyType: timeOnlyType);
        return _cache.GetOrAdd(
            key: new MapCacheKey(
                Type: type,
                StrictNull: strictNull,
                DateType: normalizedDateType,
                DecimalType: normalizedDecimalType,
                GuidType: normalizedGuidType,
                DateOnlyType: normalizedDateOnlyType,
                TimeOnlyType: normalizedTimeOnlyType),
            valueFactory: static (key, mapper) => mapper.MapUncached(
                type: key.Type,
                strictNull: key.StrictNull,
                dateType: key.DateType,
                decimalType: key.DecimalType,
                guidType: key.GuidType,
                dateOnlyType: key.DateOnlyType,
                timeOnlyType: key.TimeOnlyType),
            factoryArgument: this);
    }

    internal string Map(
        TypeMetadataReference type,
        bool strictNull,
        TypeScriptDateMapping dateMapping,
        string? decimalType,
        string? guidType,
        FrontendRuntimeTypeKind runtimeType = FrontendRuntimeTypeKind.Auto)
    {
        ArgumentNullException.ThrowIfNull(argument: type);
        ArgumentNullException.ThrowIfNull(argument: dateMapping);

        return _semanticCache.GetOrAdd(
            key: new SemanticMapCacheKey(
                Type: type,
                StrictNull: strictNull,
                DateMapping: dateMapping,
                DecimalType: NormalizeDecimalType(decimalType: decimalType),
                GuidType: NormalizeGuidType(guidType: guidType),
                RuntimeType: runtimeType),
            valueFactory: static (key, mapper) => mapper.MapSemanticUncached(
                type: key.Type,
                strictNull: key.StrictNull,
                dateMapping: key.DateMapping,
                decimalType: key.DecimalType,
                guidType: key.GuidType,
                runtimeType: key.RuntimeType),
            factoryArgument: this);
    }

    private static bool IsBoolean(string fullName) =>
        fullName.Equals(value: "System.Boolean", comparisonType: StringComparison.Ordinal);

    private static bool IsTaskLike(string fullName) =>
        fullName.Equals(value: "System.Threading.Tasks.Task", comparisonType: StringComparison.Ordinal)
        || fullName.Equals(value: "System.Threading.Tasks.ValueTask", comparisonType: StringComparison.Ordinal);

    private static bool IsString(string fullName) =>
        fullName.Equals(value: "System.String", comparisonType: StringComparison.Ordinal)
        || fullName.Equals(value: "System.Char", comparisonType: StringComparison.Ordinal);

    private static bool IsGuid(string fullName) =>
        fullName.Equals(value: "System.Guid", comparisonType: StringComparison.Ordinal);

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
        || fullName.Equals(value: "System.Double", comparisonType: StringComparison.Ordinal);

    private static bool IsDecimal(string fullName) =>
        fullName.Equals(value: "System.Decimal", comparisonType: StringComparison.Ordinal);

    private static string NormalizeDateType(string? dateType) =>
        string.IsNullOrWhiteSpace(value: dateType)
            ? DefaultDateType
            : dateType.Trim();

    private static string NormalizeDecimalType(string? decimalType) =>
        string.IsNullOrWhiteSpace(value: decimalType)
            ? DefaultDecimalType
            : decimalType.Trim();

    private static string NormalizeGuidType(string? guidType) =>
        string.IsNullOrWhiteSpace(value: guidType)
            ? DefaultGuidType
            : guidType.Trim();

    private static string NormalizeDateOnlyType(string? dateOnlyType) =>
        string.IsNullOrWhiteSpace(value: dateOnlyType)
            ? DefaultDateOnlyType
            : dateOnlyType.Trim();

    private static string NormalizeTimeOnlyType(string? timeOnlyType) =>
        string.IsNullOrWhiteSpace(value: timeOnlyType)
            ? DefaultTimeOnlyType
            : timeOnlyType.Trim();

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

    private static bool TryMapKnownType(
        TypeMetadataReference type,
        string dateType,
        string decimalType,
        string guidType,
        string dateOnlyType,
        string timeOnlyType,
        [NotNullWhen(returnValue: true)] out string? mapped)
    {
        if (type.IsEnum)
        {
            mapped = type.Name;
            return true;
        }

        var fullName = type.FullName;
        return TryMapKnownTemporalType(type: type, dateType: dateType, dateOnlyType: dateOnlyType, timeOnlyType: timeOnlyType, mapped: out mapped)
               || TryMapKnownScalarType(fullName: fullName, decimalType: decimalType, guidType: guidType, mapped: out mapped);
    }

    private static bool TryMapKnownTemporalType(
        TypeMetadataReference type,
        string dateType,
        string dateOnlyType,
        string timeOnlyType,
        [NotNullWhen(returnValue: true)] out string? mapped)
    {
        var fullName = type.FullName;
        if (TypeScriptTemporalTypes.IsDateTime(fullName: fullName))
        {
            mapped = dateType;
            return true;
        }

        if (TypeScriptTemporalTypes.IsDateOnly(fullName: fullName))
        {
            mapped = dateOnlyType;
            return true;
        }

        if (TypeScriptTemporalTypes.IsTimeOnly(fullName: fullName))
        {
            mapped = timeOnlyType;
            return true;
        }

        if (fullName is "NodaTime.Duration" or "NodaTime.Period")
        {
            mapped = type.Name;
            return true;
        }

        mapped = type.IsDateLike ? "string" : null;
        return type.IsDateLike;
    }

    private static bool TryMapKnownScalarType(
        string fullName,
        string decimalType,
        string guidType,
        [NotNullWhen(returnValue: true)] out string? mapped)
    {
        mapped = null;
        if (IsGuid(fullName: fullName))
        {
            mapped = guidType;
        }
        else if (IsBoolean(fullName: fullName))
        {
            mapped = "boolean";
        }
        else if (IsDecimal(fullName: fullName))
        {
            mapped = decimalType;
        }
        else if (IsNumber(fullName: fullName))
        {
            mapped = "number";
        }
        else if (IsString(fullName: fullName))
        {
            mapped = "string";
        }
        else if (fullName.Equals(value: "System.Void", comparisonType: StringComparison.Ordinal))
        {
            mapped = "void";
        }
        else if (fullName.Equals(value: "dynamic", comparisonType: StringComparison.OrdinalIgnoreCase)
                 || fullName.Equals(value: "System.Object", comparisonType: StringComparison.Ordinal))
        {
            mapped = "unknown";
        }

        return mapped is not null;
    }

    private string MapCore(
        TypeMetadataReference type,
        bool strictNull,
        string dateType,
        string decimalType,
        string guidType,
        string dateOnlyType,
        string timeOnlyType)
    {
        if (IsTaskLike(fullName: type.FullName))
        {
            return type.TypeArguments.Count > 0
                ? Map(type: type.TypeArguments[index: 0], strictNull: strictNull, dateType: dateType, decimalType: decimalType, guidType: guidType, dateOnlyType: dateOnlyType, timeOnlyType: timeOnlyType)
                : "void";
        }

        if (type.IsDictionary)
        {
            return MapDictionaryType(type: type, strictNull: strictNull, dateType: dateType, decimalType: decimalType, guidType: guidType, dateOnlyType: dateOnlyType, timeOnlyType: timeOnlyType);
        }

        if (type.IsCollection && type.ElementType is not null)
        {
            return $"{FormatArrayElementType(mapped: Map(type: type.ElementType, strictNull: strictNull, dateType: dateType, decimalType: decimalType, guidType: guidType, dateOnlyType: dateOnlyType, timeOnlyType: timeOnlyType))}[]";
        }

        if (TryMapKnownType(type: type, dateType: dateType, decimalType: decimalType, guidType: guidType, dateOnlyType: dateOnlyType, timeOnlyType: timeOnlyType, mapped: out var mapped))
        {
            return mapped;
        }

        return type.TypeArguments.Count > 0
            ? MapGenericType(type: type, strictNull: strictNull, dateType: dateType, decimalType: decimalType, guidType: guidType, dateOnlyType: dateOnlyType, timeOnlyType: timeOnlyType)
            : type.Name;
    }

    private string MapDictionaryType(
        TypeMetadataReference type,
        bool strictNull,
        string dateType,
        string decimalType,
        string guidType,
        string dateOnlyType,
        string timeOnlyType)
    {
        var keyType = type.TypeArguments.Count > 0
            ? type.TypeArguments[index: 0]
            : null;
        var valueType = type.TypeArguments.Count > 1
            ? Map(type: type.TypeArguments[index: 1], strictNull: strictNull, dateType: dateType, decimalType: decimalType, guidType: guidType, dateOnlyType: dateOnlyType, timeOnlyType: timeOnlyType)
            : "unknown";
        var keyTypeName = keyType is null
            ? "string"
            : MapDictionaryKeyType(type: keyType, dateType: dateType, decimalType: decimalType, guidType: guidType, dateOnlyType: dateOnlyType, timeOnlyType: timeOnlyType);

        return keyTypeName is "string" or "number"
            || (keyType is not null && IsGuid(fullName: keyType.FullName))
            || keyType?.IsEnum == true
            ? $"Record<{keyTypeName}, {valueType}>"
            : $"Map<{keyTypeName}, {valueType}>";
    }

    private string MapDictionaryKeyType(
        TypeMetadataReference type,
        string dateType,
        string decimalType,
        string guidType,
        string dateOnlyType,
        string timeOnlyType)
    {
        if (type.IsEnum)
        {
            return type.Name;
        }

        if (IsNumber(fullName: type.FullName)
            || IsDecimal(fullName: type.FullName))
        {
            return "number";
        }

        if (IsString(fullName: type.FullName))
        {
            return "string";
        }

        return Map(type: type, strictNull: false, dateType: dateType, decimalType: decimalType, guidType: guidType, dateOnlyType: dateOnlyType, timeOnlyType: timeOnlyType);
    }

    private string MapGenericType(
        TypeMetadataReference type,
        bool strictNull,
        string dateType,
        string decimalType,
        string guidType,
        string dateOnlyType,
        string timeOnlyType)
    {
        var arguments = string.Join(
            separator: ", ",
            values: type.TypeArguments.Select(selector: argument => Map(type: argument, strictNull: strictNull, dateType: dateType, decimalType: decimalType, guidType: guidType, dateOnlyType: dateOnlyType, timeOnlyType: timeOnlyType)));
        return type.Name + "<" + arguments + ">";
    }

    private string MapUncached(
        TypeMetadataReference type,
        bool strictNull,
        string dateType,
        string decimalType,
        string guidType,
        string dateOnlyType,
        string timeOnlyType)
    {
        var mapped = MapCore(type: type, strictNull: strictNull, dateType: dateType, decimalType: decimalType, guidType: guidType, dateOnlyType: dateOnlyType, timeOnlyType: timeOnlyType);
        if (strictNull && type.IsNullable && !HasTopLevelNullUnion(mapped: mapped))
        {
            return $"{mapped} | null";
        }

        return mapped;
    }

    private string MapSemanticUncached(
        TypeMetadataReference type,
        bool strictNull,
        TypeScriptDateMapping dateMapping,
        string decimalType,
        string guidType,
        FrontendRuntimeTypeKind runtimeType)
    {
        var mapped = MapSemanticCore(
            type: type,
            strictNull: strictNull,
            dateMapping: dateMapping,
            decimalType: decimalType,
            guidType: guidType,
            runtimeType: runtimeType);
        if (strictNull && type.IsNullable && !HasTopLevelNullUnion(mapped: mapped))
        {
            return $"{mapped} | null";
        }

        return mapped;
    }

    private string MapSemanticCore(
        TypeMetadataReference type,
        bool strictNull,
        TypeScriptDateMapping dateMapping,
        string decimalType,
        string guidType,
        FrontendRuntimeTypeKind runtimeType)
    {
        if (TryMapSemanticContainer(
            type: type,
            strictNull: strictNull,
            dateMapping: dateMapping,
            decimalType: decimalType,
            guidType: guidType,
            runtimeType: runtimeType,
            mapped: out var containerType))
        {
            return containerType;
        }

        var overridden = runtimeType switch
        {
            FrontendRuntimeTypeKind.Decimal => decimalType,
            FrontendRuntimeTypeKind.Uuid => guidType,
            FrontendRuntimeTypeKind.String => "string",
            _ => null,
        };
        if (overridden is not null)
        {
            return overridden;
        }

        if ((dateMapping.Library != Configuration.DateLibrary.Legacy || runtimeType != FrontendRuntimeTypeKind.Auto)
            && DateSemanticTypeResolver.Resolve(type: type, runtimeType: runtimeType) is { } semanticKind)
        {
            return dateMapping.GetType(kind: semanticKind);
        }

        if (type.TypeArguments.Count > 0)
        {
            var arguments = string.Join(
                separator: ", ",
                values: type.TypeArguments.Select(
                    selector: argument => Map(
                        type: argument,
                        strictNull: strictNull,
                        dateMapping: dateMapping,
                        decimalType: decimalType,
                        guidType: guidType,
                        runtimeType: runtimeType)));
            return type.Name + "<" + arguments + ">";
        }

        return Map(
            type: type,
            strictNull: false,
            dateType: dateMapping.DateType,
            decimalType: decimalType,
            guidType: guidType,
            dateOnlyType: dateMapping.DateOnlyType,
            timeOnlyType: dateMapping.TimeOnlyType);
    }

    private bool TryMapSemanticContainer(
        TypeMetadataReference type,
        bool strictNull,
        TypeScriptDateMapping dateMapping,
        string decimalType,
        string guidType,
        FrontendRuntimeTypeKind runtimeType,
        [NotNullWhen(returnValue: true)] out string? mapped)
    {
        if (IsTaskLike(fullName: type.FullName))
        {
            mapped = type.TypeArguments.Count > 0
                ? Map(
                    type: type.TypeArguments[index: 0],
                    strictNull: strictNull,
                    dateMapping: dateMapping,
                    decimalType: decimalType,
                    guidType: guidType,
                    runtimeType: runtimeType)
                : "void";
            return true;
        }

        if (type.IsDictionary)
        {
            mapped = MapSemanticDictionaryType(
                type: type,
                strictNull: strictNull,
                dateMapping: dateMapping,
                decimalType: decimalType,
                guidType: guidType,
                runtimeType: runtimeType);
            return true;
        }

        if (type.IsCollection && type.ElementType is not null)
        {
            var elementType = Map(
                type: type.ElementType,
                strictNull: strictNull,
                dateMapping: dateMapping,
                decimalType: decimalType,
                guidType: guidType,
                runtimeType: runtimeType);
            mapped = $"{FormatArrayElementType(mapped: elementType)}[]";
            return true;
        }

        mapped = null;
        return false;
    }

    private string MapSemanticDictionaryType(
        TypeMetadataReference type,
        bool strictNull,
        TypeScriptDateMapping dateMapping,
        string decimalType,
        string guidType,
        FrontendRuntimeTypeKind runtimeType)
    {
        var keyType = type.TypeArguments.Count > 0
            ? type.TypeArguments[index: 0]
            : null;
        var valueType = type.TypeArguments.Count > 1
            ? Map(
                type: type.TypeArguments[index: 1],
                strictNull: strictNull,
                dateMapping: dateMapping,
                decimalType: decimalType,
                guidType: guidType,
                runtimeType: runtimeType)
            : "unknown";
        var keyTypeName = keyType is null
            ? "string"
            : MapDictionaryKeyType(
                type: keyType,
                dateType: dateMapping.DateType,
                decimalType: decimalType,
                guidType: guidType,
                dateOnlyType: dateMapping.DateOnlyType,
                timeOnlyType: dateMapping.TimeOnlyType);

        return keyTypeName is "string" or "number"
            || (keyType is not null && IsGuid(fullName: keyType.FullName))
            || keyType?.IsEnum == true
            ? $"Record<{keyTypeName}, {valueType}>"
            : $"Map<{keyTypeName}, {valueType}>";
    }

    private sealed record MapCacheKey(
        TypeMetadataReference Type,
        bool StrictNull,
        string DateType,
        string DecimalType,
        string GuidType,
        string DateOnlyType,
        string TimeOnlyType);

    private sealed record SemanticMapCacheKey(
        TypeMetadataReference Type,
        bool StrictNull,
        TypeScriptDateMapping DateMapping,
        string DecimalType,
        string GuidType,
        FrontendRuntimeTypeKind RuntimeType);
}
