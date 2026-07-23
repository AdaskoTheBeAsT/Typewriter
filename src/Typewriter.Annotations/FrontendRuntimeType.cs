namespace AdaskoTheBeAsT.Typewriter.Annotations;

/// <summary>
/// Specifies the runtime type that Typewriter templates should generate
/// for a property when the C# type alone is ambiguous.
/// </summary>
public enum FrontendRuntimeType
{
    /// <summary>
    /// Infer the runtime type from the C# type.
    /// </summary>
    Auto = 0,

    /// <summary>
    /// Map to a <c>decimal</c> / Decimal.js value.
    /// </summary>
    Decimal = 1,

    /// <summary>
    /// Map to a UUID value.
    /// </summary>
    Uuid = 2,

    /// <summary>
    /// Map to a <c>Temporal.Instant</c>.
    /// </summary>
    TemporalInstant = 3,

    /// <summary>
    /// Map to a <c>Temporal.PlainDate</c>.
    /// </summary>
    TemporalPlainDate = 4,

    /// <summary>
    /// Map to a <c>Temporal.PlainTime</c>.
    /// </summary>
    TemporalPlainTime = 5,

    /// <summary>
    /// Map to a <c>Temporal.PlainDateTime</c>.
    /// </summary>
    TemporalPlainDateTime = 6,

    /// <summary>
    /// Map to a <c>Temporal.ZonedDateTime</c>.
    /// </summary>
    TemporalZonedDateTime = 7,

    /// <summary>
    /// Map to a <c>Temporal.Duration</c>.
    /// </summary>
    TemporalDuration = 8,

    /// <summary>
    /// Emit the value as a plain string without transformation.
    /// </summary>
    String = 9,

    /// <summary>
    /// Map to an instant type provided by the selected date library.
    /// </summary>
    Instant = TemporalInstant,

    /// <summary>
    /// Map to a plain-date type provided by the selected date library.
    /// </summary>
    PlainDate = TemporalPlainDate,

    /// <summary>
    /// Map to a plain-time type provided by the selected date library.
    /// </summary>
    PlainTime = TemporalPlainTime,

    /// <summary>
    /// Map to a plain date-time type provided by the selected date library.
    /// </summary>
    PlainDateTime = TemporalPlainDateTime,

    /// <summary>
    /// Map to a zoned date-time type provided by the selected date library.
    /// </summary>
    ZonedDateTime = TemporalZonedDateTime,

    /// <summary>
    /// Map to an elapsed-duration type provided by the selected date library.
    /// </summary>
    Duration = TemporalDuration,

    /// <summary>
    /// Map to a calendar-period type provided by the selected date library.
    /// </summary>
    Period = 10,

    /// <summary>
    /// Map to a Temporal duration used with calendar-period semantics.
    /// </summary>
    TemporalPeriod = Period,

    /// <summary>
    /// Map to a year-month type provided by the selected date library.
    /// </summary>
    PlainYearMonth = 11,

    /// <summary>
    /// Map to a month-day type provided by the selected date library.
    /// </summary>
    PlainMonthDay = 12,

    /// <summary>
    /// Map to a <c>Temporal.PlainYearMonth</c>.
    /// </summary>
    TemporalPlainYearMonth = PlainYearMonth,

    /// <summary>
    /// Map to a <c>Temporal.PlainMonthDay</c>.
    /// </summary>
    TemporalPlainMonthDay = PlainMonthDay,
}
