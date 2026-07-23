using Typewriter.Configuration;

namespace Typewriter.Engine;

internal sealed record TypeScriptDateMapping(
    DateLibrary Library,
    string DateType,
    string DateOnlyType,
    string TimeOnlyType,
    string InstantType,
    string PlainDateType,
    string PlainTimeType,
    string PlainDateTimeType,
    string ZonedDateTimeType,
    string DurationType,
    string PeriodType,
    string PlainYearMonthType,
    string PlainMonthDayType)
{
    public string GetType(DateSemanticKind kind) =>
        kind switch
        {
            DateSemanticKind.Instant => InstantType,
            DateSemanticKind.PlainDate => PlainDateType,
            DateSemanticKind.PlainTime => PlainTimeType,
            DateSemanticKind.PlainDateTime => PlainDateTimeType,
            DateSemanticKind.ZonedDateTime => ZonedDateTimeType,
            DateSemanticKind.Duration => DurationType,
            DateSemanticKind.Period => PeriodType,
            DateSemanticKind.PlainYearMonth => PlainYearMonthType,
            DateSemanticKind.PlainMonthDay => PlainMonthDayType,
            _ => throw new ArgumentOutOfRangeException(paramName: nameof(kind), actualValue: kind, message: null),
        };

    public static TypeScriptDateMapping Legacy(
        string dateType,
        string dateOnlyType,
        string timeOnlyType) =>
        new(
            Library: DateLibrary.Legacy,
            DateType: dateType,
            DateOnlyType: dateOnlyType,
            TimeOnlyType: timeOnlyType,
            InstantType: dateType,
            PlainDateType: dateOnlyType,
            PlainTimeType: timeOnlyType,
            PlainDateTimeType: dateType,
            ZonedDateTimeType: dateType,
            DurationType: "string",
            PeriodType: "string",
            PlainYearMonthType: "string",
            PlainMonthDayType: "string");
}
