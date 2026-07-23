namespace Typewriter.Engine;

internal sealed record DateLibraryProfile(
    string InstantType,
    string InstantInitializer,
    string PlainDateType,
    string PlainDateInitializer,
    string PlainTimeType,
    string PlainTimeInitializer,
    string PlainDateTimeType,
    string PlainDateTimeInitializer,
    string ZonedDateTimeType,
    string ZonedDateTimeInitializer,
    string DurationType,
    string DurationInitializer,
    string PeriodType,
    string PeriodInitializer,
    string PlainYearMonthType,
    string PlainYearMonthInitializer,
    string PlainMonthDayType,
    string PlainMonthDayInitializer,
    string Imports);
