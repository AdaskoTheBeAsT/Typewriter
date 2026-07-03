namespace Typewriter.CodeModel;

/// <summary>
/// An event declared on a type, enumerated in templates with <c>$Events[...]</c>.
/// </summary>
public class Event : Item
{
    /// <summary>
    /// Gets the attributes applied to the event.
    /// </summary>
    public virtual IAttributeCollection Attributes { get; init; } = new AttributeCollection();

    /// <summary>
    /// Gets the XML documentation comment of the event, or <c>null</c> when it has none.
    /// </summary>
    public virtual DocComment? DocComment { get; init; }

    /// <summary>
    /// Gets the delegate type of the event, for example <c>EventHandler&lt;TArgs&gt;</c>.
    /// </summary>
    public virtual Type? Type { get; init; }

    /// <summary>
    /// Converts the event to its name so it can be used directly in string contexts.
    /// </summary>
    public static implicit operator string(Event? instance) => instance?.ToString() ?? string.Empty;
}
