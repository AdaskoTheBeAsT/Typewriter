namespace Typewriter.Engine;

internal enum FrontendRuntimeTypeKind
{
    Auto = 0,
    Decimal = 1,
    Uuid = 2,
    Instant = 3,
    PlainDate = 4,
    PlainTime = 5,
    PlainDateTime = 6,
    ZonedDateTime = 7,
    Duration = 8,
    String = 9,
    Period = 10,
    PlainYearMonth = 11,
    PlainMonthDay = 12,
}
