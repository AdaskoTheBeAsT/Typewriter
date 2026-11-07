namespace Typewriter.CodeModel;

/// <summary>
/// A C# class made available to templates through <c>$Classes[...]</c>.
/// Exposes the members, base type, interfaces, and nested types of the class.
/// </summary>
public class Class : Item
{
    private IAttributeCollection? _attributes;
    private IConstantCollection? _constants;
    private IDelegateCollection? _delegates;
    private IEventCollection? _events;
    private IFieldCollection? _fields;
    private IInterfaceCollection? _interfaces;
    private IMethodCollection? _methods;
    private IClassCollection? _nestedClasses;
    private IEnumCollection? _nestedEnums;
    private IInterfaceCollection? _nestedInterfaces;
    private IRecordCollection? _nestedRecords;
    private IStructCollection? _nestedStructs;
    private IPropertyCollection? _properties;
    private IStaticReadOnlyFieldCollection? _staticReadOnlyFields;
    private Type? _type;
    private ITypeCollection? _typeArguments;
    private ITypeParameterCollection? _typeParameters;
    private int _initializedDefaults;

    private Class? _baseType;
    private Class? _containingType;
    private Lazy<Class?>? _baseTypeFactory;
    private Lazy<Class?>? _containingTypeFactory;

    /// <summary>
    /// Gets the attributes applied to the class, for example <c>[Serializable]</c>.
    /// Template shorthand: <c>$Attributes[...]</c>.
    /// </summary>
    public virtual IAttributeCollection Attributes
    {
        get => Volatile.Read(location: ref _attributes) ?? GetDefaultValue(target: ref _attributes, index: 0, factory: static () => new AttributeCollection());
        init => InitializeValue(target: ref _attributes, value: value, index: 0);
    }

    /// <summary>
    /// Gets the direct base class this class inherits from, or <c>null</c> when the class
    /// inherits only from <c>object</c>. The base class exposes the same members
    /// (<c>Name</c>, <c>Properties</c>, ...), so <c>$BaseClass[...]</c> can walk inheritance chains,
    /// for example <c>export class $Name$BaseClass[ extends $Name]</c>.
    /// </summary>
    public virtual Class? BaseClass
    {
        get => _baseTypeFactory is null ? _baseType : _baseTypeFactory.Value;
        init => _baseType = value;
    }

    /// <summary>
    /// Gets the <c>const</c> members declared on the class.
    /// Template shorthand: <c>$Constants[...]</c>.
    /// </summary>
    public virtual IConstantCollection Constants
    {
        get => Volatile.Read(location: ref _constants) ?? GetDefaultValue(target: ref _constants, index: 1, factory: static () => new ConstantCollection());
        init => InitializeValue(target: ref _constants, value: value, index: 1);
    }

    /// <summary>
    /// Gets the class this class is nested inside, or <c>null</c> for top-level classes.
    /// </summary>
    public virtual Class? ContainingClass
    {
        get => _containingTypeFactory is null ? _containingType : _containingTypeFactory.Value;
        init => _containingType = value;
    }

    /// <summary>
    /// Gets the delegates declared inside the class.
    /// </summary>
    public virtual IDelegateCollection Delegates
    {
        get => Volatile.Read(location: ref _delegates) ?? GetDefaultValue(target: ref _delegates, index: 2, factory: static () => new DelegateCollection());
        init => InitializeValue(target: ref _delegates, value: value, index: 2);
    }

    /// <summary>
    /// Gets the XML documentation comment of the class, or <c>null</c> when the class
    /// has no doc comment. Use <c>$DocComment.Summary</c> in templates.
    /// </summary>
    public virtual DocComment? DocComment { get; init; }

    /// <summary>
    /// Gets the events declared on the class.
    /// </summary>
    public virtual IEventCollection Events
    {
        get => Volatile.Read(location: ref _events) ?? GetDefaultValue(target: ref _events, index: 3, factory: static () => new EventCollection());
        init => InitializeValue(target: ref _events, value: value, index: 3);
    }

    /// <summary>
    /// Gets the instance fields declared on the class.
    /// Template shorthand: <c>$Fields[...]</c>.
    /// </summary>
    public virtual IFieldCollection Fields
    {
        get => Volatile.Read(location: ref _fields) ?? GetDefaultValue(target: ref _fields, index: 4, factory: static () => new FieldCollection());
        init => InitializeValue(target: ref _fields, value: value, index: 4);
    }

    /// <summary>
    /// Gets the interfaces implemented by the class.
    /// </summary>
    public virtual IInterfaceCollection Interfaces
    {
        get => Volatile.Read(location: ref _interfaces) ?? GetDefaultValue(target: ref _interfaces, index: 5, factory: static () => new InterfaceCollection());
        init => InitializeValue(target: ref _interfaces, value: value, index: 5);
    }

    /// <summary>
    /// Gets a value indicating whether the class is declared <c>abstract</c>.
    /// </summary>
    public virtual bool IsAbstract { get; init; }

    /// <summary>
    /// Gets a value indicating whether the class declares generic type parameters,
    /// for example <c>Result&lt;T&gt;</c>.
    /// </summary>
    public virtual bool IsGeneric { get; init; }

    /// <summary>
    /// Gets a value indicating whether the class is declared <c>static</c>.
    /// </summary>
    public virtual bool IsStatic { get; init; }

    /// <summary>
    /// Gets the methods declared on the class.
    /// Template shorthand: <c>$Methods[...]</c>.
    /// </summary>
    public virtual IMethodCollection Methods
    {
        get => Volatile.Read(location: ref _methods) ?? GetDefaultValue(target: ref _methods, index: 6, factory: static () => new MethodCollection());
        init => InitializeValue(target: ref _methods, value: value, index: 6);
    }

    /// <summary>
    /// Gets the namespace that contains the class, for example <c>MyApp.Models</c>.
    /// Template shorthand: <c>$Namespace</c>.
    /// </summary>
    public virtual string Namespace { get; init; } = string.Empty;

    /// <summary>
    /// Gets the classes nested inside the class.
    /// </summary>
    public virtual IClassCollection NestedClasses
    {
        get => Volatile.Read(location: ref _nestedClasses) ?? GetDefaultValue(target: ref _nestedClasses, index: 7, factory: static () => new ClassCollection());
        init => InitializeValue(target: ref _nestedClasses, value: value, index: 7);
    }

    /// <summary>
    /// Gets the enums nested inside the class.
    /// </summary>
    public virtual IEnumCollection NestedEnums
    {
        get => Volatile.Read(location: ref _nestedEnums) ?? GetDefaultValue(target: ref _nestedEnums, index: 8, factory: static () => new EnumCollection());
        init => InitializeValue(target: ref _nestedEnums, value: value, index: 8);
    }

    /// <summary>
    /// Gets the interfaces nested inside the class.
    /// </summary>
    public virtual IInterfaceCollection NestedInterfaces
    {
        get => Volatile.Read(location: ref _nestedInterfaces) ?? GetDefaultValue(target: ref _nestedInterfaces, index: 9, factory: static () => new InterfaceCollection());
        init => InitializeValue(target: ref _nestedInterfaces, value: value, index: 9);
    }

    /// <summary>
    /// Gets the records nested inside the class.
    /// </summary>
    public virtual IRecordCollection NestedRecords
    {
        get => Volatile.Read(location: ref _nestedRecords) ?? GetDefaultValue(target: ref _nestedRecords, index: 10, factory: static () => new RecordCollection());
        init => InitializeValue(target: ref _nestedRecords, value: value, index: 10);
    }

    /// <summary>
    /// Gets the structs nested inside the class.
    /// </summary>
    public virtual IStructCollection NestedStructs
    {
        get => Volatile.Read(location: ref _nestedStructs) ?? GetDefaultValue(target: ref _nestedStructs, index: 11, factory: static () => new StructCollection());
        init => InitializeValue(target: ref _nestedStructs, value: value, index: 11);
    }

    /// <summary>
    /// Gets the properties declared on the class, including indexers.
    /// Template shorthand: <c>$Properties[...]</c>.
    /// </summary>
    public virtual IPropertyCollection Properties
    {
        get => Volatile.Read(location: ref _properties) ?? GetDefaultValue(target: ref _properties, index: 12, factory: static () => new PropertyCollection());
        init => InitializeValue(target: ref _properties, value: value, index: 12);
    }

    /// <summary>
    /// Gets the <c>static readonly</c> fields declared on the class.
    /// </summary>
    public virtual IStaticReadOnlyFieldCollection StaticReadOnlyFields
    {
        get => Volatile.Read(location: ref _staticReadOnlyFields) ?? GetDefaultValue(target: ref _staticReadOnlyFields, index: 13, factory: static () => new StaticReadOnlyFieldCollection());
        init => InitializeValue(target: ref _staticReadOnlyFields, value: value, index: 13);
    }

    /// <summary>
    /// Gets the type reference for the class, exposing type-level information such as
    /// <c>IsNullable</c>, <c>IsEnumerable</c>, and the TypeScript rendering of the type.
    /// </summary>
    public virtual Type Type
    {
        get => Volatile.Read(location: ref _type) ?? GetDefaultValue(target: ref _type, index: 14, factory: static () => new());
        init => InitializeValue(target: ref _type, value: value, index: 14);
    }

    /// <summary>
    /// Gets the generic type arguments of the class when it is a constructed generic type,
    /// for example <c>string</c> in <c>Result&lt;string&gt;</c>.
    /// </summary>
    public virtual ITypeCollection TypeArguments
    {
        get => Volatile.Read(location: ref _typeArguments) ?? GetDefaultValue(target: ref _typeArguments, index: 15, factory: static () => new TypeCollection());
        init => InitializeValue(target: ref _typeArguments, value: value, index: 15);
    }

    /// <summary>
    /// Gets the generic type parameters declared by the class,
    /// for example <c>T</c> in <c>Result&lt;T&gt;</c>.
    /// </summary>
    public virtual ITypeParameterCollection TypeParameters
    {
        get => Volatile.Read(location: ref _typeParameters) ?? GetDefaultValue(target: ref _typeParameters, index: 16, factory: static () => new TypeParameterCollection());
        init => InitializeValue(target: ref _typeParameters, value: value, index: 16);
    }

    /// <summary>
    /// Converts the class to its name so it can be used directly in string contexts.
    /// </summary>
    public static implicit operator string(Class? instance) => instance?.ToString() ?? string.Empty;

    /// <summary>
    /// Converts the class to its <see cref="Type"/> reference.
    /// </summary>
    public static implicit operator Type?(Class? instance) => instance?.Type;

    internal void SetNavigationFactories(
        Func<Class?> baseTypeFactory,
        Func<Class?> containingTypeFactory)
    {
        _baseTypeFactory = new Lazy<Class?>(valueFactory: baseTypeFactory);
        _containingTypeFactory = new Lazy<Class?>(valueFactory: containingTypeFactory);
    }

    private TValue GetDefaultValue<TValue>(ref TValue? target, int index, Func<TValue> factory)
        where TValue : class
    {
        // Explicit init values, including null, must bypass default construction.
        return (_initializedDefaults & (1 << index)) != 0
            ? target!
            : LazyInitializer.EnsureInitialized(target: ref target, valueFactory: factory)!;
    }

    private void InitializeValue<TValue>(ref TValue? target, TValue value, int index)
        where TValue : class
    {
        target = value;
        _initializedDefaults |= 1 << index;
    }
}
