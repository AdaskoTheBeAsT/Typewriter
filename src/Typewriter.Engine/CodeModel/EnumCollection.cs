namespace Typewriter.CodeModel;

public sealed class EnumCollection : ItemCollection<Enum>, IEnumCollection
{
    public EnumCollection()
    {
    }

    public EnumCollection(IEnumerable<Enum> items)
        : base(items: items)
    {
    }

    protected override IEnumerable<string> GetAttributeFilter(Enum item) => GetAttributeNames(attributes: item.Attributes);

    protected override IEnumerable<string> GetItemFilter(Enum item) => [item.Name, item.FullName];
}
