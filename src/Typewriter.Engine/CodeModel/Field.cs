namespace Typewriter.CodeModel;

/// <summary>
/// An instance field declared on a type, enumerated in templates with <c>$Fields[...]</c>.
/// </summary>
public class Field : Item
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
    /// Gets the type of the field. Rendering it (<c>$Type</c>) produces the mapped TypeScript type.
    /// </summary>
    public virtual Type? Type { get; init; }

    /// <summary>
    /// Gets the field initializer value, for example <c>42</c> for <c>private int count = 42;</c>.
    /// Template shorthand: <c>$Value</c>.
    /// </summary>
    public virtual string Value { get; init; } = string.Empty;

    /// <summary>
    /// Converts the field to its name so it can be used directly in string contexts.
    /// </summary>
    public static implicit operator string(Field? instance) => instance?.ToString() ?? string.Empty;
}
