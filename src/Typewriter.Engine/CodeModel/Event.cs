namespace Typewriter.CodeModel;

public class Event : Item
{
    public virtual IAttributeCollection Attributes { get; init; } = new AttributeCollection();

    public virtual DocComment? DocComment { get; init; }

    public virtual Type? Type { get; init; }

    public static implicit operator string(Event? instance) => instance?.ToString() ?? string.Empty;
}
