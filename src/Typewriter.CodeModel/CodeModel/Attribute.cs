namespace Typewriter.CodeModel;

/// <summary>
/// An attribute applied to a code model item, enumerated in templates with
/// <c>$Attributes[...]</c>. Attribute filters use the short name, for example
/// <c>$Classes([Serializable])[...]</c>.
/// </summary>
public class Attribute : Item
{
    /// <summary>
    /// Gets the constructor and named arguments of the attribute.
    /// Template shorthand: <c>$Arguments[...]</c>.
    /// </summary>
    public virtual IAttributeArgumentCollection Arguments { get; init; } = new AttributeArgumentCollection();

    /// <summary>
    /// Gets the type of the attribute class.
    /// </summary>
    public virtual Type? Type { get; init; }

    /// <summary>
    /// Gets the raw argument list of the attribute as written in source, for example
    /// <c>"users", Order = 1</c> for <c>[Route("users", Order = 1)]</c>.
    /// Template shorthand: <c>$Value</c>.
    /// </summary>
    public virtual string Value { get; init; } = string.Empty;

    /// <summary>
    /// Converts the attribute to its name so it can be used directly in string contexts.
    /// </summary>
    public static implicit operator string(Attribute? instance) => instance?.ToString() ?? string.Empty;
}
