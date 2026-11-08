namespace Typewriter.CodeModel;

/// <summary>
/// An attribute applied to a code model item, enumerated in templates with
/// <c>$Attributes[...]</c>. Attribute filters use the short name, for example
/// <c>$Classes([Serializable])[...]</c>.
/// </summary>
public class Attribute : Item
{
    private string _value = string.Empty;
    private Lazy<string>? _valueFactory;
    private Type? _type;
    private Lazy<Type?>? _typeFactory;

    /// <summary>
    /// Gets the constructor and named arguments of the attribute.
    /// Template shorthand: <c>$Arguments[...]</c>.
    /// </summary>
    public virtual IAttributeArgumentCollection Arguments { get; init; } = new AttributeArgumentCollection();

    /// <summary>
    /// Gets the type of the attribute class.
    /// </summary>
    public virtual Type? Type
    {
        get
        {
            return _typeFactory is null ? _type : _typeFactory.Value;
        }

        init
        {
            _type = value;
            _typeFactory = null;
        }
    }

    /// <summary>
    /// Gets the raw argument list of the attribute as written in source, for example
    /// <c>"users", Order = 1</c> for <c>[Route("users", Order = 1)]</c>.
    /// Template shorthand: <c>$Value</c>.
    /// </summary>
    public virtual string Value
    {
        get => _valueFactory is null ? _value : _valueFactory.Value;
        init => _value = value;
    }

    /// <summary>
    /// Converts the attribute to its name so it can be used directly in string contexts.
    /// </summary>
    public static implicit operator string(Attribute? instance) => instance?.ToString() ?? string.Empty;

    internal void SetValueFactory(Func<string> valueFactory) =>
        _valueFactory = new Lazy<string>(valueFactory: valueFactory);

    internal void SetTypeFactory(Func<Type?> typeFactory) =>
        _typeFactory = new Lazy<Type?>(valueFactory: typeFactory);
}
