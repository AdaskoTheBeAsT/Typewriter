namespace Typewriter.CodeModel;

/// <summary>
/// A single argument of an <see cref="Attribute"/>, enumerated in templates with
/// <c>$Arguments[...]</c>. Named arguments expose the argument name through <c>Name</c>.
/// </summary>
public class AttributeArgument : Item
{
    /// <summary>
    /// Gets the declared type of the argument value.
    /// </summary>
    public virtual Type? Type { get; init; }

    /// <summary>
    /// Gets the referenced type when the argument is a <c>typeof(...)</c> expression,
    /// otherwise <c>null</c>.
    /// </summary>
    public virtual Type? TypeValue { get; init; }

    /// <summary>
    /// Gets the constant value of the argument. Template shorthand: <c>$Value</c>.
    /// </summary>
    public virtual object? Value { get; init; }
}
