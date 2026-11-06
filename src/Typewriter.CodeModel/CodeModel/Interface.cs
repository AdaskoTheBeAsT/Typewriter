namespace Typewriter.CodeModel;

/// <summary>
/// A C# interface made available to templates through <c>$Interfaces[...]</c>.
/// Exposes the members and base interfaces of the interface.
/// </summary>
public class Interface : Item
{
    /// <summary>
    /// Gets the attributes applied to the interface.
    /// Template shorthand: <c>$Attributes[...]</c>.
    /// </summary>
    public virtual IAttributeCollection Attributes { get; init; } = new AttributeCollection();

    /// <summary>
    /// Gets the class this interface is nested inside, or <c>null</c> for top-level interfaces.
    /// </summary>
    public virtual Class? ContainingClass { get; init; }

    /// <summary>
    /// Gets the XML documentation comment of the interface, or <c>null</c> when the interface
    /// has no doc comment. Use <c>$DocComment.Summary</c> in templates.
    /// </summary>
    public virtual DocComment? DocComment { get; init; }

    /// <summary>
    /// Gets the events declared on the interface.
    /// </summary>
    public virtual IEventCollection Events { get; init; } = new EventCollection();

    /// <summary>
    /// Gets the interfaces this interface extends.
    /// </summary>
    public virtual IInterfaceCollection Interfaces { get; init; } = new InterfaceCollection();

    /// <summary>
    /// Gets a value indicating whether the interface declares generic type parameters.
    /// </summary>
    public virtual bool IsGeneric { get; init; }

    /// <summary>
    /// Gets the methods declared on the interface.
    /// Template shorthand: <c>$Methods[...]</c>.
    /// </summary>
    public virtual IMethodCollection Methods { get; init; } = new MethodCollection();

    /// <summary>
    /// Gets the namespace that contains the interface.
    /// Template shorthand: <c>$Namespace</c>.
    /// </summary>
    public virtual string Namespace { get; init; } = string.Empty;

    /// <summary>
    /// Gets the structs nested inside the interface.
    /// </summary>
    public virtual IStructCollection NestedStructs { get; init; } = new StructCollection();

    /// <summary>
    /// Gets the properties declared on the interface.
    /// Template shorthand: <c>$Properties[...]</c>.
    /// </summary>
    public virtual IPropertyCollection Properties { get; init; } = new PropertyCollection();

    /// <summary>
    /// Gets the type reference for the interface.
    /// </summary>
    public virtual Type Type { get; init; } = new();

    /// <summary>
    /// Gets the generic type arguments of the interface when it is a constructed generic type.
    /// </summary>
    public virtual ITypeCollection TypeArguments { get; init; } = new TypeCollection();

    /// <summary>
    /// Gets the generic type parameters declared by the interface.
    /// </summary>
    public virtual ITypeParameterCollection TypeParameters { get; init; } = new TypeParameterCollection();

    /// <summary>
    /// Converts the interface to its name so it can be used directly in string contexts.
    /// </summary>
    public static implicit operator string(Interface? instance) => instance?.ToString() ?? string.Empty;

    /// <summary>
    /// Converts the interface to its <see cref="Type"/> reference.
    /// </summary>
    public static implicit operator Type?(Interface? instance) => instance?.Type;
}
