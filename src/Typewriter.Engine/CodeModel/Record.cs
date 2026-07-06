namespace Typewriter.CodeModel;

/// <summary>
/// A C# record made available to templates through <c>$Records[...]</c>.
/// Exposes the members, base record, interfaces, and nested types of the record.
/// </summary>
public class Record : Item
{
    /// <summary>
    /// Gets the attributes applied to the record.
    /// Template shorthand: <c>$Attributes[...]</c>.
    /// </summary>
    public virtual IAttributeCollection Attributes { get; init; } = new AttributeCollection();

    /// <summary>
    /// Gets the direct base record this record inherits from, or <c>null</c> when there is none.
    /// The base record exposes the same members, so <c>$BaseRecord[...]</c> can walk
    /// inheritance chains.
    /// </summary>
    public virtual Record? BaseRecord { get; init; }

    /// <summary>
    /// Gets the <c>const</c> members declared on the record.
    /// Template shorthand: <c>$Constants[...]</c>.
    /// </summary>
    public virtual IConstantCollection Constants { get; init; } = new ConstantCollection();

    /// <summary>
    /// Gets the record this record is nested inside, or <c>null</c> for top-level records.
    /// </summary>
    public virtual Record? ContainingRecord { get; init; }

    /// <summary>
    /// Gets the delegates declared inside the record.
    /// </summary>
    public virtual IDelegateCollection Delegates { get; init; } = new DelegateCollection();

    /// <summary>
    /// Gets the XML documentation comment of the record, or <c>null</c> when the record
    /// has no doc comment. Use <c>$DocComment.Summary</c> in templates.
    /// </summary>
    public virtual DocComment? DocComment { get; init; }

    /// <summary>
    /// Gets the events declared on the record.
    /// </summary>
    public virtual IEventCollection Events { get; init; } = new EventCollection();

    /// <summary>
    /// Gets the instance fields declared on the record.
    /// </summary>
    public virtual IFieldCollection Fields { get; init; } = new FieldCollection();

    /// <summary>
    /// Gets the interfaces implemented by the record.
    /// </summary>
    public virtual IInterfaceCollection Interfaces { get; init; } = new InterfaceCollection();

    /// <summary>
    /// Gets a value indicating whether the record is declared <c>abstract</c>.
    /// </summary>
    public virtual bool IsAbstract { get; init; }

    /// <summary>
    /// Gets a value indicating whether the record declares generic type parameters.
    /// </summary>
    public virtual bool IsGeneric { get; init; }

    /// <summary>
    /// Gets the methods declared on the record.
    /// Template shorthand: <c>$Methods[...]</c>.
    /// </summary>
    public virtual IMethodCollection Methods { get; init; } = new MethodCollection();

    /// <summary>
    /// Gets the namespace that contains the record.
    /// Template shorthand: <c>$Namespace</c>.
    /// </summary>
    public virtual string Namespace { get; init; } = string.Empty;

    /// <summary>
    /// Gets the classes nested inside the record.
    /// </summary>
    public virtual IClassCollection NestedClasses { get; init; } = new ClassCollection();

    /// <summary>
    /// Gets the enums nested inside the record.
    /// </summary>
    public virtual IEnumCollection NestedEnums { get; init; } = new EnumCollection();

    /// <summary>
    /// Gets the interfaces nested inside the record.
    /// </summary>
    public virtual IInterfaceCollection NestedInterfaces { get; init; } = new InterfaceCollection();

    /// <summary>
    /// Gets the records nested inside the record.
    /// </summary>
    public virtual IRecordCollection NestedRecords { get; init; } = new RecordCollection();

    /// <summary>
    /// Gets the structs nested inside the record.
    /// </summary>
    public virtual IStructCollection NestedStructs { get; init; } = new StructCollection();

    /// <summary>
    /// Gets the properties declared on the record, including positional parameters.
    /// Template shorthand: <c>$Properties[...]</c>.
    /// </summary>
    public virtual IPropertyCollection Properties { get; init; } = new PropertyCollection();

    /// <summary>
    /// Gets the <c>static readonly</c> fields declared on the record.
    /// </summary>
    public virtual IStaticReadOnlyFieldCollection StaticReadOnlyFields { get; init; } = new StaticReadOnlyFieldCollection();

    /// <summary>
    /// Gets the type reference for the record, exposing type-level information such as
    /// <c>IsNullable</c>, <c>IsEnumerable</c>, and the TypeScript rendering of the type.
    /// </summary>
    public virtual Type Type { get; init; } = new();

    /// <summary>
    /// Gets the generic type arguments of the record when it is a constructed generic type.
    /// </summary>
    public virtual ITypeCollection TypeArguments { get; init; } = new TypeCollection();

    /// <summary>
    /// Gets the generic type parameters declared by the record.
    /// </summary>
    public virtual ITypeParameterCollection TypeParameters { get; init; } = new TypeParameterCollection();

    /// <summary>
    /// Converts the record to its name so it can be used directly in string contexts.
    /// </summary>
    public static implicit operator string(Record? instance) => instance?.ToString() ?? string.Empty;

    /// <summary>
    /// Converts the record to its <see cref="Type"/> reference.
    /// </summary>
    public static implicit operator Type?(Record? instance) => instance?.Type;
}
