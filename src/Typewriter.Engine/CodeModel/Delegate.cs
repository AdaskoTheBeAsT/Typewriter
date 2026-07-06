namespace Typewriter.CodeModel;

/// <summary>
/// A C# delegate made available to templates through <c>$Delegates[...]</c>.
/// </summary>
public class Delegate : Item
{
    /// <summary>
    /// Gets the attributes applied to the delegate.
    /// </summary>
    public virtual IAttributeCollection Attributes { get; init; } = new AttributeCollection();

    /// <summary>
    /// Gets the XML documentation comment of the delegate, or <c>null</c> when it has none.
    /// </summary>
    public virtual DocComment? DocComment { get; init; }

    /// <summary>
    /// Gets a value indicating whether the delegate declares generic type parameters.
    /// </summary>
    public virtual bool IsGeneric { get; init; }

    /// <summary>
    /// Gets the parameters of the delegate signature.
    /// Template shorthand: <c>$Parameters[...]</c>.
    /// </summary>
    public virtual IParameterCollection Parameters { get; init; } = new ParameterCollection();

    /// <summary>
    /// Gets the return type of the delegate.
    /// </summary>
    public virtual Type? Type { get; init; }

    /// <summary>
    /// Gets the generic type parameters declared by the delegate.
    /// </summary>
    public virtual ITypeParameterCollection TypeParameters { get; init; } = new TypeParameterCollection();

    /// <summary>
    /// Converts the delegate to its name so it can be used directly in string contexts.
    /// </summary>
    public static implicit operator string(Delegate? instance) => instance?.ToString() ?? string.Empty;
}
