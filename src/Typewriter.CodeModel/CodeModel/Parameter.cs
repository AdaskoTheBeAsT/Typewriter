namespace Typewriter.CodeModel;

/// <summary>
/// A parameter of a method or indexer, enumerated in templates with <c>$Parameters[...]</c>.
/// </summary>
public class Parameter : Item
{
    /// <summary>
    /// Gets the attributes applied to the parameter, for example <c>[FromBody]</c> or <c>[FromQuery]</c>.
    /// </summary>
    public virtual IAttributeCollection Attributes { get; init; } = new AttributeCollection();

    /// <summary>
    /// Gets the default value of an optional parameter, or an empty string when there is none.
    /// Template shorthand: <c>$DefaultValue</c>.
    /// </summary>
    public virtual string DefaultValue { get; init; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether the parameter declares a default value.
    /// Template shorthand: <c>$HasDefaultValue[...][...]</c>.
    /// </summary>
    public virtual bool HasDefaultValue { get; init; }

    /// <summary>
    /// Gets the type of the parameter. Rendering it (<c>$Type</c>) produces the mapped
    /// TypeScript type.
    /// </summary>
    public virtual Type Type { get; init; } = new();

    /// <summary>
    /// Converts the parameter to its name so it can be used directly in string contexts.
    /// </summary>
    public static implicit operator string(Parameter? instance) => instance?.ToString() ?? string.Empty;
}
