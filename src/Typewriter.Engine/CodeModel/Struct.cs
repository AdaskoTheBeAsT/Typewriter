namespace Typewriter.CodeModel;

/// <summary>
/// A C# struct made available to templates through <c>$Structs[...]</c>.
/// Exposes the members, interfaces, and nested types of the struct.
/// </summary>
public class Struct : Item
{
    /// <summary>
    /// Gets the attributes applied to the struct.
    /// Template shorthand: <c>$Attributes[...]</c>.
    /// </summary>
    public virtual IAttributeCollection Attributes { get; init; } = new AttributeCollection();

    /// <summary>
    /// Gets the <c>const</c> members declared on the struct.
    /// Template shorthand: <c>$Constants[...]</c>.
    /// </summary>
    public virtual IConstantCollection Constants { get; init; } = new ConstantCollection();

    /// <summary>
    /// Gets the class this struct is nested inside, or <c>null</c> when not nested in a class.
    /// </summary>
    public virtual Class? ContainingClass { get; init; }

    /// <summary>
    /// Gets the struct this struct is nested inside, or <c>null</c> when not nested in a struct.
    /// </summary>
    public virtual Struct? ContainingStruct { get; init; }

    /// <summary>
    /// Gets the delegates declared inside the struct.
    /// </summary>
    public virtual IDelegateCollection Delegates { get; init; } = new DelegateCollection();

    /// <summary>
    /// Gets the XML documentation comment of the struct, or <c>null</c> when the struct
    /// has no doc comment. Use <c>$DocComment.Summary</c> in templates.
    /// </summary>
    public virtual DocComment? DocComment { get; init; }

    /// <summary>
    /// Gets the events declared on the struct.
    /// </summary>
    public virtual IEventCollection Events { get; init; } = new EventCollection();

    /// <summary>
    /// Gets the instance fields declared on the struct.
    /// </summary>
    public virtual IFieldCollection Fields { get; init; } = new FieldCollection();

    /// <summary>
    /// Gets the interfaces implemented by the struct.
    /// </summary>
    public virtual IInterfaceCollection Interfaces { get; init; } = new InterfaceCollection();

    /// <summary>
    /// Gets a value indicating whether the struct declares generic type parameters.
    /// </summary>
    public virtual bool IsGeneric { get; init; }

    /// <summary>
    /// Gets a value indicating whether the struct is declared <c>static</c>.
    /// </summary>
    public virtual bool IsStatic { get; init; }

    /// <summary>
    /// Gets the methods declared on the struct.
    /// Template shorthand: <c>$Methods[...]</c>.
    /// </summary>
    public virtual IMethodCollection Methods { get; init; } = new MethodCollection();

    /// <summary>
    /// Gets the namespace that contains the struct.
    /// Template shorthand: <c>$Namespace</c>.
    /// </summary>
    public virtual string Namespace { get; init; } = string.Empty;

    /// <summary>
    /// Gets the classes nested inside the struct.
    /// </summary>
    public virtual IClassCollection NestedClasses { get; init; } = new ClassCollection();

    /// <summary>
    /// Gets the enums nested inside the struct.
    /// </summary>
    public virtual IEnumCollection NestedEnums { get; init; } = new EnumCollection();

    /// <summary>
    /// Gets the interfaces nested inside the struct.
    /// </summary>
    public virtual IInterfaceCollection NestedInterfaces { get; init; } = new InterfaceCollection();

    /// <summary>
    /// Gets the records nested inside the struct.
    /// </summary>
    public virtual IRecordCollection NestedRecords { get; init; } = new RecordCollection();

    /// <summary>
    /// Gets the structs nested inside the struct.
    /// </summary>
    public virtual IStructCollection NestedStructs { get; init; } = new StructCollection();

    /// <summary>
    /// Gets the properties declared on the struct, including indexers.
    /// Template shorthand: <c>$Properties[...]</c>.
    /// </summary>
    public virtual IPropertyCollection Properties { get; init; } = new PropertyCollection();

    /// <summary>
    /// Gets the <c>static readonly</c> fields declared on the struct.
    /// </summary>
    public virtual IStaticReadOnlyFieldCollection StaticReadOnlyFields { get; init; } = new StaticReadOnlyFieldCollection();

    /// <summary>
    /// Gets the type reference for the struct, exposing type-level information such as
    /// <c>IsStruct</c>, <c>IsNullable</c>, and the TypeScript rendering of the type.
    /// </summary>
    public virtual Type Type { get; init; } = new();

    /// <summary>
    /// Gets the generic type arguments of the struct when it is a constructed generic type.
    /// </summary>
    public virtual ITypeCollection TypeArguments { get; init; } = new TypeCollection();

    /// <summary>
    /// Gets the generic type parameters declared by the struct.
    /// </summary>
    public virtual ITypeParameterCollection TypeParameters { get; init; } = new TypeParameterCollection();

    /// <summary>
    /// Converts the struct to its name so it can be used directly in string contexts.
    /// </summary>
    public static implicit operator string(Struct? instance) => instance?.ToString() ?? string.Empty;

    /// <summary>
    /// Converts the struct to its <see cref="Type"/> reference.
    /// </summary>
    public static implicit operator Type?(Struct? instance) => instance?.Type;
}
