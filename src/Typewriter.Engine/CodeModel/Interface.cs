namespace Typewriter.CodeModel;

public class Interface : Item
{
    public virtual IAttributeCollection Attributes { get; init; } = new AttributeCollection();

    public virtual Class? ContainingClass { get; init; }

    public virtual DocComment? DocComment { get; init; }

    public virtual IEventCollection Events { get; init; } = new EventCollection();

    public virtual IInterfaceCollection Interfaces { get; init; } = new InterfaceCollection();

    public virtual bool IsGeneric { get; init; }

    public virtual IMethodCollection Methods { get; init; } = new MethodCollection();

    public virtual string Namespace { get; init; } = string.Empty;

    public virtual IStructCollection NestedStructs { get; init; } = new StructCollection();

    public virtual IPropertyCollection Properties { get; init; } = new PropertyCollection();

    public virtual Type Type { get; init; } = new();

    public virtual ITypeCollection TypeArguments { get; init; } = new TypeCollection();

    public virtual ITypeParameterCollection TypeParameters { get; init; } = new TypeParameterCollection();

    public static implicit operator string(Interface? instance) => instance?.ToString() ?? string.Empty;

    public static implicit operator Type?(Interface? instance) => instance?.Type;
}
