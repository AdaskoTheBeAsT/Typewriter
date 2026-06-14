using System.Globalization;
using Type = Typewriter.CodeModel.Type;

namespace Typewriter.Extensions.Types;

public static class TypeExtensions
{
    public static string ClassName(this Type type)
    {
        ArgumentNullException.ThrowIfNull(argument: type);

        return type.Name
            .Replace(oldValue: " | null", newValue: string.Empty, comparisonType: StringComparison.Ordinal)
            .Replace(oldValue: "(", newValue: string.Empty, comparisonType: StringComparison.Ordinal)
            .Replace(oldValue: ")", newValue: string.Empty, comparisonType: StringComparison.Ordinal)
            .TrimEnd('[', ']');
    }

#pragma warning disable MA0051,S3776
    public static string Default(this Type type)
#pragma warning restore MA0051,S3776
    {
        ArgumentNullException.ThrowIfNull(argument: type);

        if (type.IsNullable)
        {
            return "null";
        }

        if (type.IsDictionary)
        {
            return "{}";
        }

        if (type.IsDynamic)
        {
            return "null";
        }

        if (type.IsEnumerable)
        {
            return "[]";
        }

        if (type.Name.Equals(value: "boolean", comparisonType: StringComparison.OrdinalIgnoreCase)
            || type.Name.Equals(value: "bool", comparisonType: StringComparison.OrdinalIgnoreCase)
            || type.FullName.Equals(value: "System.Boolean", comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            return "false";
        }

        if (type.Name.Equals(value: "number", comparisonType: StringComparison.OrdinalIgnoreCase)
            || (type.IsPrimitive
                && !type.Name.Equals(value: "string", comparisonType: StringComparison.OrdinalIgnoreCase)
                && !type.FullName.Equals(value: "System.String", comparisonType: StringComparison.OrdinalIgnoreCase)))
        {
            return "0";
        }

        if (type.Name.Equals(value: "void", comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            return "void(0)";
        }

        var stringLiteralCharacter = type.Settings?.StringLiteralCharacter ?? '"';
        if (type.IsGuid)
        {
            return $"{stringLiteralCharacter}{Guid.Empty.ToString(format: "D", provider: CultureInfo.InvariantCulture)}{stringLiteralCharacter}";
        }

        if (type.IsTimeSpan)
        {
            return $"{stringLiteralCharacter}00:00:00{stringLiteralCharacter}";
        }

        if (type.IsDate)
        {
            return "new Date()";
        }

        if (type.IsEnum)
        {
            return string.IsNullOrEmpty(value: type.DefaultValue) ? "0" : type.DefaultValue;
        }

        if (type.Name.Equals(value: "string", comparisonType: StringComparison.OrdinalIgnoreCase)
            || type.FullName.Equals(value: "System.String", comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            return $"{stringLiteralCharacter}{stringLiteralCharacter}";
        }

        return $"new {type.ClassName()}()";
    }

    public static Type Unwrap(this Type type)
    {
        ArgumentNullException.ThrowIfNull(argument: type);

        return type.IsGeneric && type.TypeArguments.Count > 0
            ? type.TypeArguments[index: 0]
            : type.ElementType ?? type;
    }
}
