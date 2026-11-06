namespace Typewriter.CodeModel;

/// <summary>
/// The root of the code model for one rendered source file. Templates receive it through
/// <c>Template(Settings settings, File file)</c> and <c>OnRenderComplete(File file)</c>,
/// and the top-level collections (<c>$Classes</c>, <c>$Enums</c>, ...) come from here.
/// </summary>
public class File : Item
{
    /// <summary>
    /// Gets the classes declared in the file. Template shorthand: <c>$Classes[...]</c>.
    /// </summary>
    public virtual IClassCollection Classes { get; init; } = new ClassCollection();

    /// <summary>
    /// Gets the delegates declared in the file. Template shorthand: <c>$Delegates[...]</c>.
    /// </summary>
    public virtual IDelegateCollection Delegates { get; init; } = new DelegateCollection();

    /// <summary>
    /// Gets the enums declared in the file. Template shorthand: <c>$Enums[...]</c>.
    /// </summary>
    public virtual IEnumCollection Enums { get; init; } = new EnumCollection();

    /// <summary>
    /// Gets the interfaces declared in the file. Template shorthand: <c>$Interfaces[...]</c>.
    /// </summary>
    public virtual IInterfaceCollection Interfaces { get; init; } = new InterfaceCollection();

    /// <summary>
    /// Gets the full path of the source file.
    /// </summary>
    public virtual string Path { get; init; } = string.Empty;

    /// <summary>
    /// Gets the records declared in the file. Template shorthand: <c>$Records[...]</c>.
    /// </summary>
    public virtual IRecordCollection Records { get; init; } = new RecordCollection();

    /// <summary>
    /// Gets the structs declared in the file. Template shorthand: <c>$Structs[...]</c>.
    /// </summary>
    public virtual IStructCollection Structs { get; init; } = new StructCollection();

    /// <summary>
    /// Gets all types declared in the file. Template shorthand: <c>$Types[...]</c>,
    /// optionally filtered like <c>$Types(Class)[...]</c>.
    /// </summary>
    public virtual ITypeCollection Types { get; init; } = new TypeCollection();
}
