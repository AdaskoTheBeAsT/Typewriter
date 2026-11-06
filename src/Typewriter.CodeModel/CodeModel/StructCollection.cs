namespace Typewriter.CodeModel;

public sealed class StructCollection : ItemCollection<Struct>, IStructCollection
{
    public StructCollection()
    {
    }

    public StructCollection(IEnumerable<Struct> items)
        : base(items: items)
    {
    }

    protected override IEnumerable<string> GetAttributeFilter(Struct item) => GetAttributeNames(attributes: item.Attributes);

    protected override IEnumerable<string> GetInheritanceFilter(Struct item)
    {
        foreach (var implementedInterface in item.Interfaces)
        {
            yield return implementedInterface.Name;
            yield return implementedInterface.FullName;
        }
    }

    protected override IEnumerable<string> GetItemFilter(Struct item) => [item.Name, item.FullName, item.Namespace];
}
