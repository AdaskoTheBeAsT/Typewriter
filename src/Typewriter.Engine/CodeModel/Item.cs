namespace Typewriter.CodeModel;

public class Item
{
    public virtual string AssemblyName { get; init; } = string.Empty;

    public virtual string FullName { get; init; } = string.Empty;

    public virtual string Name { get; init; } = string.Empty;

#pragma warning disable SA1300 // Element should begin with upper-case letter
    public virtual string name => GetName(nameCase: NameCase.LegacyCamelCase);
#pragma warning restore SA1300 // Element should begin with upper-case letter

    public virtual Item? Parent { get; init; }

    public virtual string GetName(NameCase nameCase) => NameCaseFormatter.Format(value: Name, nameCase: nameCase);

    public override string ToString() => Name;
}
