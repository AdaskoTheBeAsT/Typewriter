namespace Typewriter.CodeModel;

public class ParameterComment : Item
{
    public virtual string Description { get; init; } = string.Empty;

    public static implicit operator string(ParameterComment? instance) => instance?.ToString() ?? string.Empty;

    public override string ToString() => Name;
}
