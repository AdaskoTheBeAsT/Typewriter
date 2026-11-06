namespace Typewriter.CodeModel;

/// <summary>
/// A C# enum made available to templates through <c>$Enums[...]</c>.
/// Enumerate its members with <c>$Values[...]</c>.
/// </summary>
public class Enum : Item
{
    /// <summary>
    /// Gets the attributes applied to the enum, for example <c>[Flags]</c>.
    /// Template shorthand: <c>$Attributes[...]</c>.
    /// </summary>
    public virtual IAttributeCollection Attributes { get; init; } = new AttributeCollection();

    /// <summary>
    /// Gets the class this enum is nested inside, or <c>null</c> for top-level enums.
    /// </summary>
    public virtual Class? ContainingClass { get; init; }

    /// <summary>
    /// Gets the XML documentation comment of the enum, or <c>null</c> when the enum
    /// has no doc comment. Use <c>$DocComment.Summary</c> in templates.
    /// </summary>
    public virtual DocComment? DocComment { get; init; }

    /// <summary>
    /// Gets a value indicating whether the enum is decorated with <c>[Flags]</c>.
    /// </summary>
    public virtual bool IsFlags { get; init; }

    /// <summary>
    /// Gets the namespace that contains the enum.
    /// Template shorthand: <c>$Namespace</c>.
    /// </summary>
    public virtual string Namespace { get; init; } = string.Empty;

    /// <summary>
    /// Gets the type reference for the enum.
    /// </summary>
    public virtual Type Type { get; init; } = new();

    /// <summary>
    /// Gets the values declared on the enum.
    /// Template shorthand: <c>$Values[...]</c> or <c>$EnumValues[...]</c>.
    /// </summary>
    public virtual IEnumValueCollection Values { get; init; } = new EnumValueCollection();

    /// <summary>
    /// Converts the enum to its name so it can be used directly in string contexts.
    /// </summary>
    public static implicit operator string(Enum? instance) => instance?.ToString() ?? string.Empty;

    /// <summary>
    /// Converts the enum to its <see cref="Type"/> reference.
    /// </summary>
    public static implicit operator Type?(Enum? instance) => instance?.Type;
}
