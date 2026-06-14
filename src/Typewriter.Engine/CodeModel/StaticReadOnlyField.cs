namespace Typewriter.CodeModel;

public class StaticReadOnlyField : Item
{
    public virtual IAttributeCollection Attributes { get; init; } = new AttributeCollection();

    public virtual DocComment? DocComment { get; init; }

    public virtual Type? Type { get; init; }

    public virtual string Value { get; init; } = string.Empty;

    public static implicit operator string(StaticReadOnlyField? instance) => instance?.ToString() ?? string.Empty;
}
