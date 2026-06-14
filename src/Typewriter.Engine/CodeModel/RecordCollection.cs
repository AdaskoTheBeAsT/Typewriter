namespace Typewriter.CodeModel;

public sealed class RecordCollection : ItemCollection<Record>, IRecordCollection
{
    public RecordCollection()
    {
    }

    public RecordCollection(IEnumerable<Record> items)
        : base(items: items)
    {
    }

    protected override IEnumerable<string> GetAttributeFilter(Record item) => GetAttributeNames(attributes: item.Attributes);

    protected override IEnumerable<string> GetInheritanceFilter(Record item)
    {
        if (item.BaseRecord is not null)
        {
            yield return item.BaseRecord.Name;
            yield return item.BaseRecord.FullName;
        }

        foreach (var implementedInterface in item.Interfaces)
        {
            yield return implementedInterface.Name;
            yield return implementedInterface.FullName;
        }
    }

    protected override IEnumerable<string> GetItemFilter(Record item) => [item.Name, item.FullName];
}
