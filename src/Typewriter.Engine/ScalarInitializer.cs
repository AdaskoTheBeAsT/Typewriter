using System.Globalization;

namespace Typewriter.Engine;

internal static class ScalarInitializer
{
    public static string Normalize(string? initializer, string defaultInitializer)
    {
        return string.IsNullOrWhiteSpace(value: initializer)
            ? defaultInitializer
            : initializer.Trim();
    }

    public static string ResolveGuid(
        string guidType,
        string guidInitializer,
        char stringLiteralCharacter)
    {
        if (!IsAutomatic(initializer: guidInitializer))
        {
            return guidInitializer;
        }

        if (guidType.Equals(value: "Uint8Array", comparisonType: StringComparison.Ordinal)
            || guidType.StartsWith(value: "Uint8Array<", comparisonType: StringComparison.Ordinal))
        {
            return "new Uint8Array(16)";
        }

        return $"{stringLiteralCharacter}{Guid.Empty.ToString(format: "D", provider: CultureInfo.InvariantCulture)}{stringLiteralCharacter}";
    }

    public static string ResolveDecimal(
        string decimalType,
        string decimalInitializer)
    {
        if (!IsAutomatic(initializer: decimalInitializer))
        {
            return decimalInitializer;
        }

        return decimalType.Equals(value: TypeScriptTypeMapper.DefaultDecimalType, comparisonType: StringComparison.Ordinal)
            ? "0"
            : $"new {decimalType}(0)";
    }

    private static bool IsAutomatic(string initializer)
    {
        return string.IsNullOrWhiteSpace(value: initializer)
            || initializer.Equals(value: "auto", comparisonType: StringComparison.OrdinalIgnoreCase);
    }
}
