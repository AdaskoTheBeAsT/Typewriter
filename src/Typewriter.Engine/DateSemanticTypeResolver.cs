using Typewriter.Abstractions;

namespace Typewriter.Engine;

internal static class DateSemanticTypeResolver
{
    public static DateSemanticKind? Resolve(
        TypeMetadataReference type,
        FrontendRuntimeTypeKind runtimeType = FrontendRuntimeTypeKind.Auto)
    {
        var overridden = ResolveOverride(runtimeType: runtimeType);
        if (overridden is not null)
        {
            return overridden;
        }

        return type.FullName switch
        {
            "System.DateTime" => DateSemanticKind.PlainDateTime,
            "System.DateTimeOffset" => DateSemanticKind.Instant,
            "System.DateOnly" => DateSemanticKind.PlainDate,
            "System.TimeOnly" => DateSemanticKind.PlainTime,
            "System.TimeSpan" => DateSemanticKind.Duration,
            "NodaTime.Instant" => DateSemanticKind.Instant,
            "NodaTime.LocalDate" or "NodaTime.OffsetDate" => DateSemanticKind.PlainDate,
            "NodaTime.LocalTime" or "NodaTime.OffsetTime" => DateSemanticKind.PlainTime,
            "NodaTime.LocalDateTime" => DateSemanticKind.PlainDateTime,
            "NodaTime.OffsetDateTime" => DateSemanticKind.Instant,
            "NodaTime.ZonedDateTime" => DateSemanticKind.ZonedDateTime,
            "NodaTime.Duration" => DateSemanticKind.Duration,
            "NodaTime.Period" => DateSemanticKind.Period,
            "NodaTime.YearMonth" => DateSemanticKind.PlainYearMonth,
            "NodaTime.AnnualDate" => DateSemanticKind.PlainMonthDay,
            _ => null,
        };
    }

    private static DateSemanticKind? ResolveOverride(FrontendRuntimeTypeKind runtimeType) =>
        runtimeType switch
        {
            FrontendRuntimeTypeKind.Instant => DateSemanticKind.Instant,
            FrontendRuntimeTypeKind.PlainDate => DateSemanticKind.PlainDate,
            FrontendRuntimeTypeKind.PlainTime => DateSemanticKind.PlainTime,
            FrontendRuntimeTypeKind.PlainDateTime => DateSemanticKind.PlainDateTime,
            FrontendRuntimeTypeKind.ZonedDateTime => DateSemanticKind.ZonedDateTime,
            FrontendRuntimeTypeKind.Duration => DateSemanticKind.Duration,
            FrontendRuntimeTypeKind.Period => DateSemanticKind.Period,
            FrontendRuntimeTypeKind.PlainYearMonth => DateSemanticKind.PlainYearMonth,
            FrontendRuntimeTypeKind.PlainMonthDay => DateSemanticKind.PlainMonthDay,
            _ => null,
        };
}
