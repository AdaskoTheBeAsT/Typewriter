namespace Typewriter.CodeModel;

/// <summary>
/// A C# class made available to templates through <c>$Classes[...]</c>.
/// Exposes the members, base type, interfaces, and nested types of the class.
/// </summary>
public class Class : Item
{
    /// <summary>
    /// Gets the attributes applied to the class, for example <c>[Serializable]</c>.
    /// Template shorthand: <c>$Attributes[...]</c>.
    /// </summary>
    public virtual IAttributeCollection Attributes { get; init; } = new AttributeCollection();

    /// <summary>
    /// Gets the direct base class this class inherits from, or <c>null</c> when the class
    /// inherits only from <c>object</c>. The base class exposes the same members
    /// (<c>Name</c>, <c>Properties</c>, ...), so <c>$BaseClass[...]</c> can walk inheritance chains,
    /// for example <c>export class $Name$BaseClass[ extends $Name]</c>.
    /// </summary>
    public virtual Class? BaseClass { get; init; }

    /// <summary>
    /// Gets the <c>const</c> members declared on the class.
    /// Template shorthand: <c>$Constants[...]</c>.
    /// </summary>
    public virtual IConstantCollection Constants { get; init; } = new ConstantCollection();

    /// <summary>
    /// Gets the class this class is nested inside, or <c>null</c> for top-level classes.
    /// </summary>
    public virtual Class? ContainingClass { get; init; }

    /// <summary>
    /// Gets the delegates declared inside the class.
    /// </summary>
    public virtual IDelegateCollection Delegates { get; init; } = new DelegateCollection();

    /// <summary>
    /// Gets the XML documentation comment of the class, or <c>null</c> when the class
    /// has no doc comment. Use <c>$DocComment.Summary</c> in templates.
    /// </summary>
    public virtual DocComment? DocComment { get; init; }

    /// <summary>
    /// Gets the events declared on the class.
    /// </summary>
    public virtual IEventCollection Events { get; init; } = new EventCollection();

    /// <summary>
    /// Gets the instance fields declared on the class.
    /// Template shorthand: <c>$Fields[...]</c>.
    /// </summary>
    public virtual IFieldCollection Fields { get; init; } = new FieldCollection();

    /// <summary>
    /// Gets the interfaces implemented by the class.
    /// </summary>
    public virtual IInterfaceCollection Interfaces { get; init; } = new InterfaceCollection();

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
    public virtual IMethodCollection Methods { get; init; } = new MethodCollection();

    /// <summary>
    /// Gets the namespace that contains the class, for example <c>MyApp.Models</c>.
    /// Template shorthand: <c>$Namespace</c>.
    /// </summary>
    public virtual string Namespace { get; init; } = string.Empty;

    /// <summary>
    /// Gets the classes nested inside the class.
    /// </summary>
    public virtual IClassCollection NestedClasses { get; init; } = new ClassCollection();

    /// <summary>
    /// Gets the enums nested inside the class.
    /// </summary>
    public virtual IEnumCollection NestedEnums { get; init; } = new EnumCollection();

    /// <summary>
    /// Gets the interfaces nested inside the class.
    /// </summary>
    public virtual IInterfaceCollection NestedInterfaces { get; init; } = new InterfaceCollection();

    /// <summary>
    /// Gets the records nested inside the class.
    /// </summary>
    public virtual IRecordCollection NestedRecords { get; init; } = new RecordCollection();

    /// <summary>
    /// Gets the structs nested inside the class.
    /// </summary>
    public virtual IStructCollection NestedStructs { get; init; } = new StructCollection();

    /// <summary>
    /// Gets the properties declared on the class, including indexers.
    /// Template shorthand: <c>$Properties[...]</c>.
    /// </summary>
    public virtual IPropertyCollection Properties { get; init; } = new PropertyCollection();

    /// <summary>
    /// Gets the <c>static readonly</c> fields declared on the class.
    /// </summary>
    public virtual IStaticReadOnlyFieldCollection StaticReadOnlyFields { get; init; } = new StaticReadOnlyFieldCollection();

    /// <summary>
    /// Gets the type reference for the class, exposing type-level information such as
    /// <c>IsNullable</c>, <c>IsEnumerable</c>, and the TypeScript rendering of the type.
    /// </summary>
    public virtual Type Type { get; init; } = new();

    /// <summary>
    /// Gets the generic type arguments of the class when it is a constructed generic type,
    /// for example <c>string</c> in <c>Result&lt;string&gt;</c>.
    /// </summary>
    public virtual ITypeCollection TypeArguments { get; init; } = new TypeCollection();

    /// <summary>
    /// Gets the generic type parameters declared by the class,
    /// for example <c>T</c> in <c>Result&lt;T&gt;</c>.
    /// </summary>
    public virtual ITypeParameterCollection TypeParameters { get; init; } = new TypeParameterCollection();

    /// <summary>
    /// Converts the class to its name so it can be used directly in string contexts.
    /// </summary>
    public static implicit operator string(Class? instance) => instance?.ToString() ?? string.Empty;

    /// <summary>
    /// Converts the class to its <see cref="Type"/> reference.
    /// </summary>
    public static implicit operator Type?(Class? instance) => instance?.Type;
}
