using Typewriter.Configuration;

namespace Typewriter.Engine;

internal static class DateLibraryProfiles
{
    public static TypeScriptDateMapping GetMapping(
        DateLibrary library,
        string dateType,
        string dateOnlyType,
        string timeOnlyType)
    {
        if (library == DateLibrary.Legacy)
        {
            return TypeScriptDateMapping.Legacy(dateType: dateType, dateOnlyType: dateOnlyType, timeOnlyType: timeOnlyType);
        }

        var profile = Get(library: library);
        return new TypeScriptDateMapping(
            Library: library,
            DateType: profile.PlainDateTimeType,
            DateOnlyType: profile.PlainDateType,
            TimeOnlyType: profile.PlainTimeType,
            InstantType: profile.InstantType,
            PlainDateType: profile.PlainDateType,
            PlainTimeType: profile.PlainTimeType,
            PlainDateTimeType: profile.PlainDateTimeType,
            ZonedDateTimeType: profile.ZonedDateTimeType,
            DurationType: profile.DurationType,
            PeriodType: profile.PeriodType,
            PlainYearMonthType: profile.PlainYearMonthType,
            PlainMonthDayType: profile.PlainMonthDayType);
    }

    public static DateLibraryProfile Get(DateLibrary library) =>
        library switch
        {
            DateLibrary.NativeDate => NativeDate(),
            DateLibrary.Temporal => Temporal(),
            DateLibrary.Moment => Moment(),
            DateLibrary.Luxon => Luxon(),
            DateLibrary.DateFns => DateFns(),
            DateLibrary.DayJs => DayJs(),
            DateLibrary.JsJoda => JsJoda(),
            _ => throw new ArgumentOutOfRangeException(paramName: nameof(library), actualValue: library, message: "Legacy settings do not use a date library profile."),
        };

    private static DateLibraryProfile NativeDate() =>
        new("Date", "new Date()", "Date", "new Date()", "string", "\"00:00:00\"", "Date", "new Date()", "string", "\"\"", "string", "\"PT0S\"", "string", "\"P0D\"", "string", "\"\"", "string", "\"\"", string.Empty);

    private static DateLibraryProfile Temporal() =>
        new("Temporal.Instant", "Temporal.Now.instant()", "Temporal.PlainDate", "Temporal.Now.plainDateISO()", "Temporal.PlainTime", "Temporal.Now.plainTimeISO()", "Temporal.PlainDateTime", "Temporal.Now.plainDateTimeISO()", "Temporal.ZonedDateTime", "Temporal.Now.zonedDateTimeISO()", "Temporal.Duration", "Temporal.Duration.from(\"PT0S\")", "Temporal.Duration", "Temporal.Duration.from(\"P0D\")", "Temporal.PlainYearMonth", "Temporal.PlainYearMonth.from(\"1970-01\")", "Temporal.PlainMonthDay", "Temporal.PlainMonthDay.from(\"--01-01\")", "import { Temporal } from '@js-temporal/polyfill';");

    private static DateLibraryProfile Moment() =>
        new("moment.Moment", "moment()", "moment.Moment", "moment()", "string", "\"00:00:00\"", "moment.Moment", "moment()", "string", "\"\"", "moment.Duration", "moment.duration(0)", "moment.Duration", "moment.duration(0)", "string", "\"\"", "string", "\"\"", "import moment from 'moment';");

    private static DateLibraryProfile Luxon() =>
        new("DateTime", "DateTime.now().toUTC()", "DateTime", "DateTime.now().startOf('day')", "DateTime", "DateTime.now()", "DateTime", "DateTime.now()", "DateTime", "DateTime.now()", "Duration", "Duration.fromMillis(0)", "Duration", "Duration.fromObject({})", "DateTime", "DateTime.now().startOf('month')", "DateTime", "DateTime.now()", "import { DateTime, Duration } from 'luxon';");

    private static DateLibraryProfile DateFns() =>
        new("Date", "new Date()", "Date", "new Date()", "string", "\"00:00:00\"", "Date", "new Date()", "string", "\"\"", "Duration", "{}", "Duration", "{}", "string", "\"\"", "string", "\"\"", "import type { Duration } from 'date-fns';");

    private static DateLibraryProfile DayJs() =>
        new("Dayjs", "dayjs()", "Dayjs", "dayjs()", "string", "\"00:00:00\"", "Dayjs", "dayjs()", "string", "\"\"", "ReturnType<typeof dayjs.duration>", "dayjs.duration(0)", "ReturnType<typeof dayjs.duration>", "dayjs.duration(0)", "string", "\"\"", "string", "\"\"", "import dayjs, { type Dayjs } from 'dayjs';\nimport duration from 'dayjs/plugin/duration';\ndayjs.extend(duration);");

    private static DateLibraryProfile JsJoda() =>
        new("Instant", "Instant.now()", "LocalDate", "LocalDate.now()", "LocalTime", "LocalTime.now()", "LocalDateTime", "LocalDateTime.now()", "ZonedDateTime", "ZonedDateTime.now()", "Duration", "Duration.ZERO", "Period", "Period.ZERO", "YearMonth", "YearMonth.now()", "MonthDay", "MonthDay.now()", "import { Duration, Instant, LocalDate, LocalDateTime, LocalTime, MonthDay, Period, YearMonth, ZonedDateTime } from '@js-joda/core';\nimport '@js-joda/timezone';");
}
