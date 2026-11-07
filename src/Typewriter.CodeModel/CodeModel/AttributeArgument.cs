namespace Typewriter.CodeModel;

/// <summary>
/// A single argument of an <see cref="Attribute"/>, enumerated in templates with
/// <c>$Arguments[...]</c>. Named arguments expose the argument name through <c>Name</c>.
/// </summary>
public class AttributeArgument : Item
{
    private Type? _type;
    private Type? _typeValue;
    private Lazy<Type?>? _typeFactory;
    private Lazy<Type?>? _typeValueFactory;

    /// <summary>
    /// Gets the declared type of the argument value.
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
    /// Gets the referenced type when the argument is a <c>typeof(...)</c> expression,
    /// otherwise <c>null</c>.
    /// </summary>
    public virtual Type? TypeValue
    {
        get
        {
            return _typeValueFactory is null ? _typeValue : _typeValueFactory.Value;
        }

        init
        {
            _typeValue = value;
            _typeValueFactory = null;
        }
    }

    /// <summary>
    /// Gets the constant value of the argument. Template shorthand: <c>$Value</c>.
    /// </summary>
    public virtual object? Value { get; init; }

    internal void SetTypeFactory(Func<Type?> typeFactory) =>
        _typeFactory = new Lazy<Type?>(valueFactory: typeFactory);

    internal void SetTypeValueFactory(Func<Type?> typeValueFactory) =>
        _typeValueFactory = new Lazy<Type?>(valueFactory: typeValueFactory);
}
