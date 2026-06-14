namespace Typewriter.CodeModel;

public class Record : Item
{
    public virtual IAttributeCollection Attributes { get; init; } = new AttributeCollection();

    public virtual Record? BaseRecord { get; init; }

    public virtual IConstantCollection Constants { get; init; } = new ConstantCollection();

    public virtual Record? ContainingRecord { get; init; }

    public virtual IDelegateCollection Delegates { get; init; } = new DelegateCollection();

    public virtual DocComment? DocComment { get; init; }

    public virtual IEventCollection Events { get; init; } = new EventCollection();

    public virtual IFieldCollection Fields { get; init; } = new FieldCollection();

    public virtual IInterfaceCollection Interfaces { get; init; } = new InterfaceCollection();

    public virtual bool IsAbstract { get; init; }

    public virtual bool IsGeneric { get; init; }

    public virtual IMethodCollection Methods { get; init; } = new MethodCollection();

    public virtual string Namespace { get; init; } = string.Empty;

    public virtual IClassCollection NestedClasses { get; init; } = new ClassCollection();

    public virtual IEnumCollection NestedEnums { get; init; } = new EnumCollection();

    public virtual IInterfaceCollection NestedInterfaces { get; init; } = new InterfaceCollection();

    public virtual IRecordCollection NestedRecords { get; init; } = new RecordCollection();

    public virtual IPropertyCollection Properties { get; init; } = new PropertyCollection();

    public virtual IStaticReadOnlyFieldCollection StaticReadOnlyFields { get; init; } = new StaticReadOnlyFieldCollection();

    public virtual Type Type { get; init; } = new();

    public virtual ITypeCollection TypeArguments { get; init; } = new TypeCollection();

    public virtual ITypeParameterCollection TypeParameters { get; init; } = new TypeParameterCollection();

    public static implicit operator string(Record? instance) => instance?.ToString() ?? string.Empty;

    public static implicit operator Type?(Record? instance) => instance?.Type;
}
