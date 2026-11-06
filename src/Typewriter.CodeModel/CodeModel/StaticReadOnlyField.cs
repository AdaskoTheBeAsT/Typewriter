namespace Typewriter.CodeModel;

/// <summary>
/// A <c>static readonly</c> field declared on a type, enumerated in templates with
/// <c>$StaticReadOnlyFields[...]</c>.
/// </summary>
public class StaticReadOnlyField : Item
{
    /// <summary>
    /// Gets the attributes applied to the field.
    /// </summary>
    public virtual IAttributeCollection Attributes { get; init; } = new AttributeCollection();

    /// <summary>
    /// Gets the XML documentation comment of the field, or <c>null</c> when it has none.
    /// </summary>
    public virtual DocComment? DocComment { get; init; }

    /// <summary>
    /// Gets the type of the field.
    /// </summary>
    public virtual Type? Type { get; init; }

    /// <summary>
    /// Gets the field initializer value. Template shorthand: <c>$Value</c>.
    /// </summary>
    public virtual string Value { get; init; } = string.Empty;

    /// <summary>
    /// Converts the field to its name so it can be used directly in string contexts.
    /// </summary>
    public static implicit operator string(StaticReadOnlyField? instance) => instance?.ToString() ?? string.Empty;
}
