namespace Typewriter.CodeModel;

public class Enum : Item
{
    public virtual IAttributeCollection Attributes { get; init; } = new AttributeCollection();

    public virtual Class? ContainingClass { get; init; }

    public virtual DocComment? DocComment { get; init; }

    public virtual bool IsFlags { get; init; }

    public virtual string Namespace { get; init; } = string.Empty;

    public virtual Type Type { get; init; } = new();

    public virtual IEnumValueCollection Values { get; init; } = new EnumValueCollection();

    public static implicit operator string(Enum? instance) => instance?.ToString() ?? string.Empty;

    public static implicit operator Type?(Enum? instance) => instance?.Type;
}
