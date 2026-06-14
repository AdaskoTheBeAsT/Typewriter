namespace Typewriter.CodeModel;

public class File : Item
{
    public virtual IClassCollection Classes { get; init; } = new ClassCollection();

    public virtual IDelegateCollection Delegates { get; init; } = new DelegateCollection();

    public virtual IEnumCollection Enums { get; init; } = new EnumCollection();

    public virtual IInterfaceCollection Interfaces { get; init; } = new InterfaceCollection();

    public virtual string Path { get; init; } = string.Empty;

    public virtual IRecordCollection Records { get; init; } = new RecordCollection();

    public virtual ITypeCollection Types { get; init; } = new TypeCollection();
}
