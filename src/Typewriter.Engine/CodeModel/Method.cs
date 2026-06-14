namespace Typewriter.CodeModel;

public class Method : Item
{
    public virtual IAttributeCollection Attributes { get; init; } = new AttributeCollection();

    public virtual DocComment? DocComment { get; init; }

    public virtual bool IsAbstract { get; init; }

    public virtual bool IsGeneric { get; init; }

    public virtual IParameterCollection Parameters { get; init; } = new ParameterCollection();

    public virtual Type Type { get; init; } = new();

    public virtual ITypeParameterCollection TypeParameters { get; init; } = new TypeParameterCollection();

    public static implicit operator string(Method? instance) => instance?.ToString() ?? string.Empty;
}
