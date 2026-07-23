using System.Diagnostics.CodeAnalysis;

namespace AdaskoTheBeAsT.Typewriter.Annotations;

/// <summary>
/// Overrides the frontend runtime type that Typewriter generates for a single
/// property, field, or parameter. Use this when the C# type is ambiguous,
/// for example <see cref="global::System.DateTime"/> which could represent an
/// instant, a local date-time, or a zoned date-time depending on context.
/// </summary>
/// <param name="type">The desired frontend runtime type.</param>
[AttributeUsage(
    AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
[ExcludeFromCodeCoverage]
public sealed class FrontendRuntimeTypeAttribute(FrontendRuntimeType type) : Attribute
{
    /// <summary>
    /// Gets the frontend runtime type specified in the constructor.
    /// </summary>
    public FrontendRuntimeType Type { get; } = type;

    /// <summary>
    /// Gets or sets the expected wire format, for example <c>"string"</c> for
    /// decimal values serialised as JSON strings. When <see langword="null"/>
    /// the template infers the wire format from the C# type.
    /// </summary>
    public string? WireFormat { get; set; }

    /// <summary>
    /// Gets or sets the name of a sibling property that carries the IANA time-zone
    /// identifier, used when <see cref="Type"/> is
    /// <see cref="FrontendRuntimeType.TemporalZonedDateTime"/> and the time zone
    /// is not embedded in the wire value itself.
    /// </summary>
    public string? TimeZoneProperty { get; set; }
}
