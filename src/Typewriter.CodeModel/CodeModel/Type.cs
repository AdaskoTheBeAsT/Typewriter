using Typewriter.Configuration;

namespace Typewriter.CodeModel;

/// <summary>
/// A type reference in the code model, for example the type of a property, parameter,
/// or method return value. Rendering it (template shorthand <c>$Type</c>) produces the
/// mapped TypeScript type, for example <c>number</c> for C# <c>int</c>.
/// </summary>
public class Type : Item
{
    private IAttributeCollection? _attributes;
    private IConstantCollection? _constants;
    private IDelegateCollection? _delegates;
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
    private ITypeCollection? _typeArguments;
    private ITypeParameterCollection? _typeParameters;
    private IFieldCollection? _tupleElements;
    private int _initializedDefaults;

    private Class? _baseType;
    private Class? _containingType;
    private Lazy<Class?>? _baseTypeFactory;
    private Lazy<Class?>? _containingTypeFactory;
    private Type? _elementType;
    private Lazy<Type?>? _elementTypeFactory;

    /// <summary>
    /// Gets the attributes applied to the type declaration.
    /// </summary>
    public virtual IAttributeCollection Attributes
    {
        get => Volatile.Read(location: ref _attributes) ?? GetDefaultValue(target: ref _attributes, index: 0, factory: static () => new AttributeCollection());
        init => InitializeValue(target: ref _attributes, value: value, index: 0);
    }

    /// <summary>
    /// Gets the direct base class of the type, or <c>null</c> when the type inherits
    /// only from <c>object</c>. Useful for walking inheritance chains, for example
    /// <c>$BaseClass[ extends $Name]</c>.
    /// </summary>
    public virtual Class? BaseClass
    {
        get => _baseTypeFactory is null ? _baseType : _baseTypeFactory.Value;
        init => _baseType = value;
    }

    /// <summary>
    /// Gets the <c>const</c> members declared on the type.
    /// </summary>
    public virtual IConstantCollection Constants
    {
        get => Volatile.Read(location: ref _constants) ?? GetDefaultValue(target: ref _constants, index: 1, factory: static () => new ConstantCollection());
        init => InitializeValue(target: ref _constants, value: value, index: 1);
    }

    /// <summary>
    /// Gets the class this type is nested inside, or <c>null</c> for top-level types.
    /// </summary>
    public virtual Class? ContainingClass
    {
        get => _containingTypeFactory is null ? _containingType : _containingTypeFactory.Value;
        init => _containingType = value;
    }

    /// <summary>
    /// Gets the delegates declared inside the type.
    /// </summary>
    public virtual IDelegateCollection Delegates
    {
        get => Volatile.Read(location: ref _delegates) ?? GetDefaultValue(target: ref _delegates, index: 2, factory: static () => new DelegateCollection());
        init => InitializeValue(target: ref _delegates, value: value, index: 2);
    }

    /// <summary>
    /// Gets the XML documentation comment of the type, or <c>null</c> when the type
    /// has no doc comment. Use <c>$DocComment.Summary</c> in templates.
    /// </summary>
    public virtual DocComment? DocComment { get; init; }

    /// <summary>
    /// Gets the element type of arrays and enumerable types, for example <c>string</c>
    /// for <c>string[]</c> or <c>List&lt;string&gt;</c>; <c>null</c> for non-enumerable types.
    /// </summary>
    public virtual Type? ElementType
    {
        get => _elementTypeFactory is null ? _elementType : _elementTypeFactory.Value;
        init => _elementType = value;
    }

    /// <summary>
    /// Gets the instance fields declared on the type.
    /// </summary>
    public virtual IFieldCollection Fields
    {
        get => Volatile.Read(location: ref _fields) ?? GetDefaultValue(target: ref _fields, index: 3, factory: static () => new FieldCollection());
        init => InitializeValue(target: ref _fields, value: value, index: 3);
    }

    /// <summary>
    /// Gets the source file paths where the type is declared (multiple for partial types).
    /// </summary>
    public virtual IEnumerable<string> FileLocations { get; init; } = [];

    /// <summary>
    /// Gets the interfaces implemented by the type.
    /// </summary>
    public virtual IInterfaceCollection Interfaces
    {
        get => Volatile.Read(location: ref _interfaces) ?? GetDefaultValue(target: ref _interfaces, index: 4, factory: static () => new InterfaceCollection());
        init => InitializeValue(target: ref _interfaces, value: value, index: 4);
    }

    /// <summary>
    /// Gets a value indicating whether the type represents date or time data,
    /// for example <c>DateTime</c>, <c>DateTimeOffset</c>, <c>DateOnly</c>, or NodaTime types.
    /// </summary>
    public virtual bool IsDate { get; init; }

    /// <summary>
    /// Gets a value indicating whether the type is defined in the loaded workspace source
    /// (as opposed to a referenced assembly or framework type).
    /// </summary>
    public virtual bool IsDefined { get; init; }

    /// <summary>
    /// Gets a value indicating whether the type is a dictionary,
    /// for example <c>Dictionary&lt;string, int&gt;</c>.
    /// </summary>
    public virtual bool IsDictionary { get; init; }

    /// <summary>
    /// Gets a value indicating whether the type is <c>dynamic</c>.
    /// </summary>
    public virtual bool IsDynamic { get; init; }

    /// <summary>
    /// Gets a value indicating whether the type is an enum.
    /// </summary>
    public virtual bool IsEnum { get; init; }

    /// <summary>
    /// Gets a value indicating whether the type is enumerable (arrays, <c>List&lt;T&gt;</c>,
    /// <c>IEnumerable&lt;T&gt;</c>, ...). <c>string</c> is not treated as enumerable.
    /// </summary>
    public virtual bool IsEnumerable { get; init; }

    /// <summary>
    /// Gets a value indicating whether the type is generic.
    /// </summary>
    public virtual bool IsGeneric { get; init; }

    /// <summary>
    /// Gets a value indicating whether the type is <c>System.Guid</c>.
    /// </summary>
    public virtual bool IsGuid { get; init; }

    /// <summary>
    /// Gets a value indicating whether the type is nullable (<c>T?</c> or
    /// <c>Nullable&lt;T&gt;</c>). Template shorthand: <c>$IsNullable[...][...]</c>.
    /// </summary>
    public virtual bool IsNullable { get; init; }

    /// <summary>
    /// Gets a value indicating whether the type maps to a primitive TypeScript type
    /// (<c>string</c>, <c>number</c>, <c>boolean</c>, ...).
    /// </summary>
    public virtual bool IsPrimitive { get; init; }

    /// <summary>
    /// Gets a value indicating whether the type is a struct (value type).
    /// </summary>
    public virtual bool IsStruct { get; init; }

    /// <summary>
    /// Gets a value indicating whether the type is <c>Task</c> or <c>Task&lt;T&gt;</c>.
    /// Await-unwrapped rendering uses the inner type.
    /// </summary>
    public virtual bool IsTask { get; init; }

    /// <summary>
    /// Gets a value indicating whether the type is <c>System.TimeSpan</c>.
    /// </summary>
    public virtual bool IsTimeSpan { get; init; }

    /// <summary>
    /// Gets a value indicating whether the type is a value tuple, for example <c>(int, string)</c>.
    /// </summary>
    public virtual bool IsValueTuple { get; init; }

    /// <summary>
    /// Gets the methods declared on the type.
    /// </summary>
    public virtual IMethodCollection Methods
    {
        get => Volatile.Read(location: ref _methods) ?? GetDefaultValue(target: ref _methods, index: 5, factory: static () => new MethodCollection());
        init => InitializeValue(target: ref _methods, value: value, index: 5);
    }

    /// <summary>
    /// Gets the namespace that contains the type.
    /// </summary>
    public virtual string Namespace { get; init; } = string.Empty;

    /// <summary>
    /// Gets the classes nested inside the type.
    /// </summary>
    public virtual IClassCollection NestedClasses
    {
        get => Volatile.Read(location: ref _nestedClasses) ?? GetDefaultValue(target: ref _nestedClasses, index: 6, factory: static () => new ClassCollection());
        init => InitializeValue(target: ref _nestedClasses, value: value, index: 6);
    }

    /// <summary>
    /// Gets the enums nested inside the type.
    /// </summary>
    public virtual IEnumCollection NestedEnums
    {
        get => Volatile.Read(location: ref _nestedEnums) ?? GetDefaultValue(target: ref _nestedEnums, index: 7, factory: static () => new EnumCollection());
        init => InitializeValue(target: ref _nestedEnums, value: value, index: 7);
    }

    /// <summary>
    /// Gets the interfaces nested inside the type.
    /// </summary>
    public virtual IInterfaceCollection NestedInterfaces
    {
        get => Volatile.Read(location: ref _nestedInterfaces) ?? GetDefaultValue(target: ref _nestedInterfaces, index: 8, factory: static () => new InterfaceCollection());
        init => InitializeValue(target: ref _nestedInterfaces, value: value, index: 8);
    }

    /// <summary>
    /// Gets the records nested inside the type.
    /// </summary>
    public virtual IRecordCollection NestedRecords
    {
        get => Volatile.Read(location: ref _nestedRecords) ?? GetDefaultValue(target: ref _nestedRecords, index: 9, factory: static () => new RecordCollection());
        init => InitializeValue(target: ref _nestedRecords, value: value, index: 9);
    }

    /// <summary>
    /// Gets the structs nested inside the type.
    /// </summary>
    public virtual IStructCollection NestedStructs
    {
        get => Volatile.Read(location: ref _nestedStructs) ?? GetDefaultValue(target: ref _nestedStructs, index: 10, factory: static () => new StructCollection());
        init => InitializeValue(target: ref _nestedStructs, value: value, index: 10);
    }

    /// <summary>
    /// Gets the original C# name of the type before TypeScript mapping,
    /// for example <c>Int32</c> when <see cref="Item.Name"/> renders <c>number</c>.
    /// Template shorthand: <c>$OriginalName</c>.
    /// </summary>
    public virtual string OriginalName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the properties declared on the type.
    /// </summary>
    public virtual IPropertyCollection Properties
    {
        get => Volatile.Read(location: ref _properties) ?? GetDefaultValue(target: ref _properties, index: 11, factory: static () => new PropertyCollection());
        init => InitializeValue(target: ref _properties, value: value, index: 11);
    }

    /// <summary>
    /// Gets the <c>static readonly</c> fields declared on the type.
    /// </summary>
    public virtual IStaticReadOnlyFieldCollection StaticReadOnlyFields
    {
        get => Volatile.Read(location: ref _staticReadOnlyFields) ?? GetDefaultValue(target: ref _staticReadOnlyFields, index: 12, factory: static () => new StaticReadOnlyFieldCollection());
        init => InitializeValue(target: ref _staticReadOnlyFields, value: value, index: 12);
    }

    /// <summary>
    /// Gets the generic type arguments of the type, for example <c>string</c> and <c>int</c>
    /// in <c>Dictionary&lt;string, int&gt;</c>. Template shorthand: <c>$TypeArguments[...]</c>.
    /// </summary>
    public virtual ITypeCollection TypeArguments
    {
        get => Volatile.Read(location: ref _typeArguments) ?? GetDefaultValue(target: ref _typeArguments, index: 13, factory: static () => new TypeCollection());
        init => InitializeValue(target: ref _typeArguments, value: value, index: 13);
    }

    /// <summary>
    /// Gets the generic type parameters declared by the type.
    /// </summary>
    public virtual ITypeParameterCollection TypeParameters
    {
        get => Volatile.Read(location: ref _typeParameters) ?? GetDefaultValue(target: ref _typeParameters, index: 14, factory: static () => new TypeParameterCollection());
        init => InitializeValue(target: ref _typeParameters, value: value, index: 14);
    }

    /// <summary>
    /// Gets the elements of a value tuple type, for example <c>Item1</c> and <c>Item2</c>.
    /// </summary>
    public virtual IFieldCollection TupleElements
    {
        get => Volatile.Read(location: ref _tupleElements) ?? GetDefaultValue(target: ref _tupleElements, index: 15, factory: static () => new FieldCollection());
        init => InitializeValue(target: ref _tupleElements, value: value, index: 15);
    }

    /// <summary>
    /// Gets the default TypeScript value for the type, for example <c>0</c> for <c>number</c>
    /// and <c>null</c> for nullable types. Template shorthand: <c>$Type[$Default]</c>.
    /// </summary>
    public virtual string DefaultValue { get; init; } = "null";

    /// <summary>
    /// Gets the template settings in effect for the current render, or <c>null</c> outside rendering.
    /// </summary>
    public virtual Settings? Settings { get; init; }

    /// <summary>
    /// Converts the type to its rendered TypeScript name, with strict-null suffixes,
    /// parentheses, and array brackets removed.
    /// </summary>
    public static implicit operator string(Type? type) => NormalizeName(name: type?.ToString());

    internal void SetNavigationFactories(
        Func<Class?> baseTypeFactory,
        Func<Class?> containingTypeFactory)
    {
        _baseTypeFactory = new Lazy<Class?>(valueFactory: baseTypeFactory);
        _containingTypeFactory = new Lazy<Class?>(valueFactory: containingTypeFactory);
    }

    internal void SetElementTypeFactory(Func<Type?> elementTypeFactory) =>
        _elementTypeFactory = new Lazy<Type?>(valueFactory: elementTypeFactory);

    private static string NormalizeName(string? name)
    {
        return (name ?? string.Empty)
            .Replace(oldValue: " | null", newValue: string.Empty)
            .Replace(oldValue: "(", newValue: string.Empty)
            .Replace(oldValue: ")", newValue: string.Empty)
            .TrimEnd('[', ']');
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
