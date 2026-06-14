namespace Typewriter.CodeModel;

public class DocComment : Item
{
    public virtual string Summary { get; init; } = string.Empty;

    public virtual string Returns { get; init; } = string.Empty;

    public virtual IParameterCommentCollection Parameters { get; init; } = new ParameterCommentCollection();

    public virtual string Text => Summary;

    public static implicit operator string(DocComment? instance) => instance?.ToString() ?? string.Empty;

    public override string ToString() => Summary;
}
