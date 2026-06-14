namespace Typewriter.CodeModel;

public sealed class AttributeCollection : ItemCollection<Attribute>, IAttributeCollection
{
    public AttributeCollection()
    {
    }

    public AttributeCollection(IEnumerable<Attribute> items)
        : base(items: items)
    {
    }

    protected override IEnumerable<string> GetItemFilter(Attribute item) => [item.Name, item.FullName];
}
