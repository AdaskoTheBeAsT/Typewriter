namespace Typewriter.CodeModel;

public class Delegate : Item
{
    public virtual IAttributeCollection Attributes { get; init; } = new AttributeCollection();

    public virtual DocComment? DocComment { get; init; }

    public virtual bool IsGeneric { get; init; }

    public virtual IParameterCollection Parameters { get; init; } = new ParameterCollection();

    public virtual Type? Type { get; init; }

    public virtual ITypeParameterCollection TypeParameters { get; init; } = new TypeParameterCollection();

    public static implicit operator string(Delegate? instance) => instance?.ToString() ?? string.Empty;
}
