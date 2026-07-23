using System.Diagnostics.CodeAnalysis;

namespace AdaskoTheBeAsT.Typewriter.Annotations;

/// <summary>
/// Attaches a human-readable label to an enum field. Typewriter recipe
/// templates can read this label through the field's attribute collection
/// and emit it alongside the enum value in the generated frontend code.
/// </summary>
/// <param name="label">The display label for the enum value.</param>
[AttributeUsage(AttributeTargets.Field)]
[ExcludeFromCodeCoverage]
public sealed class LabelForEnumAttribute(string label) : Attribute
{
    /// <summary>
    /// Gets the display label specified in the constructor.
    /// </summary>
    public string Label { get; } = label;
}
