using System.Globalization;
using Typewriter.Abstractions;

namespace Typewriter.Engine;

internal static class FrontendRuntimeTypeResolver
{
    private const string AttributeFullName = "AdaskoTheBeAsT.Typewriter.Annotations.FrontendRuntimeTypeAttribute";

    public static FrontendRuntimeTypeKind Resolve(IReadOnlyList<AttributeMetadata> attributes)
    {
        var attribute = attributes.FirstOrDefault(
            predicate: item => item.FullName.Equals(value: AttributeFullName, comparisonType: StringComparison.Ordinal));
        var value = attribute?.Arguments.FirstOrDefault()?.Value;
        if (string.IsNullOrWhiteSpace(value: value))
        {
            return FrontendRuntimeTypeKind.Auto;
        }

        if (int.TryParse(s: value, style: NumberStyles.Integer, provider: CultureInfo.InvariantCulture, result: out var numeric)
            && Enum.IsDefined(enumType: typeof(FrontendRuntimeTypeKind), value: numeric))
        {
            return (FrontendRuntimeTypeKind)numeric;
        }

        var name = value[(value.LastIndexOf(value: '.') + 1)..];
        if (name.StartsWith(value: "Temporal", comparisonType: StringComparison.Ordinal))
        {
            name = name["Temporal".Length..];
        }

        return Enum.TryParse<FrontendRuntimeTypeKind>(value: name, ignoreCase: false, result: out var runtimeType)
            ? runtimeType
            : FrontendRuntimeTypeKind.Auto;
    }
}
