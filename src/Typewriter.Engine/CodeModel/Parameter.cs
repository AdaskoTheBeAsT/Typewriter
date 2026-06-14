namespace Typewriter.CodeModel;

public class Parameter : Item
{
    public virtual IAttributeCollection Attributes { get; init; } = new AttributeCollection();

    public virtual string DefaultValue { get; init; } = string.Empty;

    public virtual bool HasDefaultValue { get; init; }

    public virtual Type Type { get; init; } = new();

    public static implicit operator string(Parameter? instance) => instance?.ToString() ?? string.Empty;
}
