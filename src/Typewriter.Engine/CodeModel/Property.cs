namespace Typewriter.CodeModel;

public class Property : Item
{
    public virtual IAttributeCollection Attributes { get; init; } = new AttributeCollection();

    public virtual DocComment? DocComment { get; init; }

    public virtual bool HasGetter { get; init; }

    public virtual bool HasSetter { get; init; }

    public virtual bool IsAbstract { get; init; }

    public virtual bool IsIndexer { get; init; }

    public virtual bool IsRequired { get; init; }

    public virtual bool IsVirtual { get; init; }

    public virtual IParameterCollection Parameters { get; init; } = new ParameterCollection();

    public virtual Type Type { get; init; } = new();

    public static implicit operator string(Property? instance) => instance?.ToString() ?? string.Empty;
}
