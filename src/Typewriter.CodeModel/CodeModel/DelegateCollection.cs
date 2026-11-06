namespace Typewriter.CodeModel;

public sealed class DelegateCollection : ItemCollection<Delegate>, IDelegateCollection
{
    public DelegateCollection()
    {
    }

    public DelegateCollection(IEnumerable<Delegate> items)
        : base(items: items)
    {
    }

    protected override IEnumerable<string> GetAttributeFilter(Delegate item) => GetAttributeNames(attributes: item.Attributes);

    protected override IEnumerable<string> GetItemFilter(Delegate item) => [item.Name, item.FullName];
}
