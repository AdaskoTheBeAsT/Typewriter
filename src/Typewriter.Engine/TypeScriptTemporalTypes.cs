namespace Typewriter.Engine;

internal static class TypeScriptTemporalTypes
{
    public static bool IsDateTime(string fullName) =>
        fullName.Equals(value: "System.DateTime", comparisonType: StringComparison.Ordinal)
        || fullName.Equals(value: "System.DateTimeOffset", comparisonType: StringComparison.Ordinal)
        || fullName.Equals(value: "NodaTime.Instant", comparisonType: StringComparison.Ordinal)
        || fullName.Equals(value: "NodaTime.LocalDateTime", comparisonType: StringComparison.Ordinal)
        || fullName.Equals(value: "NodaTime.OffsetDateTime", comparisonType: StringComparison.Ordinal)
        || fullName.Equals(value: "NodaTime.ZonedDateTime", comparisonType: StringComparison.Ordinal);

    public static bool IsDateOnly(string fullName) =>
        fullName.Equals(value: "System.DateOnly", comparisonType: StringComparison.Ordinal)
        || fullName.Equals(value: "NodaTime.LocalDate", comparisonType: StringComparison.Ordinal)
        || fullName.Equals(value: "NodaTime.OffsetDate", comparisonType: StringComparison.Ordinal);

    public static bool IsTimeOnly(string fullName) =>
        fullName.Equals(value: "System.TimeOnly", comparisonType: StringComparison.Ordinal)
        || fullName.Equals(value: "NodaTime.LocalTime", comparisonType: StringComparison.Ordinal)
        || fullName.Equals(value: "NodaTime.OffsetTime", comparisonType: StringComparison.Ordinal);

    public static string FormatTimeOnlyInitializer(
        string initializer,
        char stringLiteralCharacter) =>
        initializer.Equals(value: TypeScriptTypeMapper.DefaultTimeOnlyInitializer, comparisonType: StringComparison.Ordinal)
            ? $"{stringLiteralCharacter}00:00:00{stringLiteralCharacter}"
            : initializer;
}
