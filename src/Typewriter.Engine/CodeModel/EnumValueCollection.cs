namespace Typewriter.CodeModel;

public sealed class EnumValueCollection : ItemCollection<EnumValue>, IEnumValueCollection
{
    public EnumValueCollection()
    {
    }

    public EnumValueCollection(IEnumerable<EnumValue> items)
        : base(items: items)
    {
    }

    protected override IEnumerable<string> GetAttributeFilter(EnumValue item) => GetAttributeNames(attributes: item.Attributes);

    protected override IEnumerable<string> GetItemFilter(EnumValue item) => [item.Name, item.FullName];
}
