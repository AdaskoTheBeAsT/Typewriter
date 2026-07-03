namespace Typewriter.CodeModel;

/// <summary>
/// The XML documentation comment of a code model item. Access it in templates through
/// <c>$DocComment.Summary</c>, <c>$DocComment.Returns</c>, and <c>$DocComment.Parameters</c>.
/// </summary>
public class DocComment : Item
{
    /// <summary>
    /// Gets the text of the <c>&lt;summary&gt;</c> element.
    /// </summary>
    public virtual string Summary { get; init; } = string.Empty;

    /// <summary>
    /// Gets the text of the <c>&lt;returns&gt;</c> element.
    /// </summary>
    public virtual string Returns { get; init; } = string.Empty;

    /// <summary>
    /// Gets the <c>&lt;param&gt;</c> element descriptions.
    /// </summary>
    public virtual IParameterCommentCollection Parameters { get; init; } = new ParameterCommentCollection();

    /// <summary>
    /// Gets the summary text. Alias for <see cref="Summary"/> kept for legacy templates.
    /// </summary>
    public virtual string Text => Summary;

    /// <summary>
    /// Converts the doc comment to its summary text.
    /// </summary>
    public static implicit operator string(DocComment? instance) => instance?.ToString() ?? string.Empty;

    /// <inheritdoc/>
    public override string ToString() => Summary;
}
