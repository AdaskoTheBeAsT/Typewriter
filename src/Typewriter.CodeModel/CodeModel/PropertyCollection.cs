namespace Typewriter.CodeModel;

public sealed class PropertyCollection : ItemCollection<Property>, IPropertyCollection
{
    public PropertyCollection()
    {
    }

    public PropertyCollection(IEnumerable<Property> items)
        : base(items: items)
    {
    }

    protected override IEnumerable<string> GetAttributeFilter(Property item) => GetAttributeNames(attributes: item.Attributes);

    protected override IEnumerable<string> GetItemFilter(Property item) => [item.Name, item.FullName];
}
