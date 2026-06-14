namespace Typewriter.CodeModel;

public class AttributeArgument : Item
{
    public virtual Type? Type { get; init; }

    public virtual Type? TypeValue { get; init; }

    public virtual object? Value { get; init; }
}
