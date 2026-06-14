namespace Typewriter.CodeModel;

public class Attribute : Item
{
    public virtual IAttributeArgumentCollection Arguments { get; init; } = new AttributeArgumentCollection();

    public virtual Type? Type { get; init; }

    public virtual string Value { get; init; } = string.Empty;

    public static implicit operator string(Attribute? instance) => instance?.ToString() ?? string.Empty;
}
