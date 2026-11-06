namespace Typewriter.CodeModel;

/// <summary>
/// The <c>&lt;param&gt;</c> documentation of a single parameter, exposed through
/// <see cref="DocComment.Parameters"/>.
/// </summary>
public class ParameterComment : Item
{
    /// <summary>
    /// Gets the description text of the <c>&lt;param&gt;</c> element.
    /// </summary>
    public virtual string Description { get; init; } = string.Empty;

    /// <summary>
    /// Converts the parameter comment to the parameter name.
    /// </summary>
    public static implicit operator string(ParameterComment? instance) => instance?.ToString() ?? string.Empty;

    /// <inheritdoc/>
    public override string ToString() => Name;
}
