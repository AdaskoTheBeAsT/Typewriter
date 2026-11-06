namespace Typewriter.CodeModel;

/// <summary>
/// A single member of an <see cref="Enum"/>, enumerated in templates with
/// <c>$Values[...]</c> or <c>$EnumValues[...]</c>.
/// </summary>
public class EnumValue : Item
{
    /// <summary>
    /// Gets the attributes applied to the enum member, for example <c>[EnumMember(Value = "...")]</c>.
    /// </summary>
    public virtual IAttributeCollection Attributes { get; init; } = new AttributeCollection();

    /// <summary>
    /// Gets the XML documentation comment of the enum member, or <c>null</c> when it has none.
    /// </summary>
    public virtual DocComment? DocComment { get; init; }

    /// <summary>
    /// Gets the numeric value of the enum member. Template shorthand: <c>$Value</c>.
    /// </summary>
    public virtual long Value { get; init; }

    /// <summary>
    /// Converts the enum value to its name so it can be used directly in string contexts.
    /// </summary>
    public static implicit operator string(EnumValue? instance) => instance?.ToString() ?? string.Empty;
}
