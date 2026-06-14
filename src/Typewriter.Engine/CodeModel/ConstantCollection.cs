namespace Typewriter.CodeModel;

public sealed class ConstantCollection : ItemCollection<Constant>, IConstantCollection
{
    public ConstantCollection()
    {
    }

    public ConstantCollection(IEnumerable<Constant> items)
        : base(items: items)
    {
    }

    protected override IEnumerable<string> GetAttributeFilter(Constant item) => GetAttributeNames(attributes: item.Attributes);

    protected override IEnumerable<string> GetItemFilter(Constant item) => [item.Name, item.FullName];
}
