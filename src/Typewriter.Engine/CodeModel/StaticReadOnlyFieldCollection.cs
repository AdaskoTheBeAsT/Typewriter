namespace Typewriter.CodeModel;

public sealed class StaticReadOnlyFieldCollection : ItemCollection<StaticReadOnlyField>, IStaticReadOnlyFieldCollection
{
    public StaticReadOnlyFieldCollection()
    {
    }

    public StaticReadOnlyFieldCollection(IEnumerable<StaticReadOnlyField> items)
        : base(items: items)
    {
    }

    protected override IEnumerable<string> GetAttributeFilter(StaticReadOnlyField item) => GetAttributeNames(attributes: item.Attributes);

    protected override IEnumerable<string> GetItemFilter(StaticReadOnlyField item) => [item.Name, item.FullName];
}
