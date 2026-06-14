namespace Typewriter.CodeModel;

public class TypeParameter : Item
{
    public virtual string Constraints { get; init; } = string.Empty;

    public static implicit operator string(TypeParameter? instance) => instance?.ToString() ?? string.Empty;
}
