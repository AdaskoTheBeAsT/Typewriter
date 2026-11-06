namespace Typewriter.CodeModel;

public sealed class InterfaceCollection : ItemCollection<Interface>, IInterfaceCollection
{
    public InterfaceCollection()
    {
    }

    public InterfaceCollection(IEnumerable<Interface> items)
        : base(items: items)
    {
    }

    protected override IEnumerable<string> GetAttributeFilter(Interface item) => GetAttributeNames(attributes: item.Attributes);

    protected override IEnumerable<string> GetInheritanceFilter(Interface item)
    {
        foreach (var implementedInterface in item.Interfaces)
        {
            yield return implementedInterface.Name;
            yield return implementedInterface.FullName;
        }
    }

    protected override IEnumerable<string> GetItemFilter(Interface item) => [item.Name, item.FullName];
}
