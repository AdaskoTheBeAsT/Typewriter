namespace Typewriter.CodeModel;

/// <summary>
/// A property declared on a class, record, struct, or interface, enumerated in
/// templates with <c>$Properties[...]</c>. Indexers are included and flagged
/// with <see cref="IsIndexer"/>.
/// </summary>
public class Property : Item
{
    /// <summary>
    /// Gets the attributes applied to the property, for example <c>[JsonPropertyName("...")]</c>.
    /// Template shorthand: <c>$Attributes[...]</c>.
    /// </summary>
    public virtual IAttributeCollection Attributes { get; init; } = new AttributeCollection();

    /// <summary>
    /// Gets the XML documentation comment of the property, or <c>null</c> when it has none.
    /// Use <c>$DocComment.Summary</c> in templates.
    /// </summary>
    public virtual DocComment? DocComment { get; init; }

    /// <summary>
    /// Gets a value indicating whether the property has a getter.
    /// Template shorthand: <c>$HasGetter[...][...]</c>.
    /// </summary>
    public virtual bool HasGetter { get; init; }

    /// <summary>
    /// Gets a value indicating whether the property has a setter (including <c>init</c>).
    /// Template shorthand: <c>$HasSetter[...][...]</c>.
    /// </summary>
    public virtual bool HasSetter { get; init; }

    /// <summary>
    /// Gets a value indicating whether the property is declared <c>abstract</c>.
    /// </summary>
    public virtual bool IsAbstract { get; init; }

    /// <summary>
    /// Gets a value indicating whether the property is an indexer, for example
    /// <c>this[string key]</c>. Template shorthand: <c>$IsIndexer[...][...]</c>.
    /// </summary>
    public virtual bool IsIndexer { get; init; }

    /// <summary>
    /// Gets a value indicating whether the property is declared <c>required</c>.
    /// </summary>
    public virtual bool IsRequired { get; init; }

    /// <summary>
    /// Gets a value indicating whether the property is declared <c>virtual</c>.
    /// </summary>
    public virtual bool IsVirtual { get; init; }

    /// <summary>
    /// Gets the indexer parameters. Empty for normal properties.
    /// Template shorthand: <c>$Parameters[...]</c>.
    /// </summary>
    public virtual IParameterCollection Parameters { get; init; } = new ParameterCollection();

    /// <summary>
    /// Gets the type of the property. Rendering it (<c>$Type</c>) produces the mapped
    /// TypeScript type, for example <c>number</c> for C# <c>int</c>.
    /// </summary>
    public virtual Type Type { get; init; } = new();

    /// <summary>
    /// Gets the property initializer value, for example <c>"text"</c> for
    /// <c>public string Name { get; set; } = "text";</c>. Template shorthand: <c>$Value</c>.
    /// </summary>
    public virtual string Value { get; init; } = string.Empty;

    /// <summary>
    /// Converts the property to its name so it can be used directly in string contexts.
    /// </summary>
    public static implicit operator string(Property? instance) => instance?.ToString() ?? string.Empty;
}
