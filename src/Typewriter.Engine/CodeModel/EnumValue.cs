namespace Typewriter.CodeModel;

public class EnumValue : Item
{
    public virtual IAttributeCollection Attributes { get; init; } = new AttributeCollection();

    public virtual DocComment? DocComment { get; init; }

    public virtual long Value { get; init; }

    public static implicit operator string(EnumValue? instance) => instance?.ToString() ?? string.Empty;
}
