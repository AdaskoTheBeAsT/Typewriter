namespace Typewriter.CodeModel;

/// <summary>
/// A method declared on a class, record, struct, or interface, enumerated in
/// templates with <c>$Methods[...]</c>.
/// </summary>
public class Method : Item
{
    /// <summary>
    /// Gets the attributes applied to the method, for example <c>[HttpGet]</c> or <c>[Route("...")]</c>.
    /// Template shorthand: <c>$Attributes[...]</c>.
    /// </summary>
    public virtual IAttributeCollection Attributes { get; init; } = new AttributeCollection();

    /// <summary>
    /// Gets the XML documentation comment of the method, or <c>null</c> when it has none.
    /// Use <c>$DocComment.Summary</c> in templates.
    /// </summary>
    public virtual DocComment? DocComment { get; init; }

    /// <summary>
    /// Gets a value indicating whether the method is declared <c>abstract</c>.
    /// </summary>
    public virtual bool IsAbstract { get; init; }

    /// <summary>
    /// Gets a value indicating whether the method declares generic type parameters.
    /// </summary>
    public virtual bool IsGeneric { get; init; }

    /// <summary>
    /// Gets the parameters of the method.
    /// Template shorthand: <c>$Parameters[...]</c>.
    /// </summary>
    public virtual IParameterCollection Parameters { get; init; } = new ParameterCollection();

    /// <summary>
    /// Gets the return type of the method. <c>Task&lt;T&gt;</c> is exposed through
    /// <see cref="CodeModel.Type.IsTask"/>. Template shorthand: <c>$Type</c> or <c>$ReturnType</c>.
    /// </summary>
    public virtual Type Type { get; init; } = new();

    /// <summary>
    /// Gets the generic type parameters declared by the method.
    /// </summary>
    public virtual ITypeParameterCollection TypeParameters { get; init; } = new TypeParameterCollection();

    /// <summary>
    /// Converts the method to its name so it can be used directly in string contexts.
    /// </summary>
    public static implicit operator string(Method? instance) => instance?.ToString() ?? string.Empty;
}
