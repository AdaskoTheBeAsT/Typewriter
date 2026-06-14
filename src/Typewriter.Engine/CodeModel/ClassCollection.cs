namespace Typewriter.CodeModel;

public sealed class ClassCollection : ItemCollection<Class>, IClassCollection
{
    public ClassCollection()
    {
    }

    public ClassCollection(IEnumerable<Class> items)
        : base(items: items)
    {
    }

    protected override IEnumerable<string> GetAttributeFilter(Class item) => GetAttributeNames(attributes: item.Attributes);

    protected override IEnumerable<string> GetInheritanceFilter(Class item)
    {
        if (item.BaseClass is not null)
        {
            yield return item.BaseClass.Name;
            yield return item.BaseClass.FullName;
        }

        foreach (var implementedInterface in item.Interfaces)
        {
            yield return implementedInterface.Name;
            yield return implementedInterface.FullName;
        }
    }

    protected override IEnumerable<string> GetItemFilter(Class item) => [item.Name, item.FullName];
}
