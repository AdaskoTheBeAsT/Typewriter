namespace Typewriter.CodeModel;

/// <summary>
/// A <c>const</c> member declared on a type, enumerated in templates with <c>$Constants[...]</c>.
/// </summary>
public class Constant : Item
{
    /// <summary>
    /// Gets the attributes applied to the constant.
    /// </summary>
    public virtual IAttributeCollection Attributes { get; init; } = new AttributeCollection();

    /// <summary>
    /// Gets the XML documentation comment of the constant, or <c>null</c> when it has none.
    /// </summary>
    public virtual DocComment? DocComment { get; init; }

    /// <summary>
    /// Gets the type of the constant.
    /// </summary>
    public virtual Type? Type { get; init; }

    /// <summary>
    /// Gets the compile-time value of the constant, for example <c>Admin</c> for
    /// <c>public const string Role = "Admin";</c>. Template shorthand: <c>$Value</c>.
    /// </summary>
    public virtual string Value { get; init; } = string.Empty;

    /// <summary>
    /// Converts the constant to its name so it can be used directly in string contexts.
    /// </summary>
    public static implicit operator string(Constant? instance) => instance?.ToString() ?? string.Empty;
}
