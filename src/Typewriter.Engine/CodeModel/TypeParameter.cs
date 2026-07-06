namespace Typewriter.CodeModel;

/// <summary>
/// A generic type parameter declared by a type or method, for example <c>T</c> in
/// <c>Result&lt;T&gt;</c>, enumerated in templates with <c>$TypeParameters[...]</c>.
/// </summary>
public class TypeParameter : Item
{
    /// <summary>
    /// Gets the generic constraints of the type parameter, for example <c>where T : class</c>.
    /// </summary>
    public virtual string Constraints { get; init; } = string.Empty;

    /// <summary>
    /// Converts the type parameter to its name so it can be used directly in string contexts.
    /// </summary>
    public static implicit operator string(TypeParameter? instance) => instance?.ToString() ?? string.Empty;
}
