namespace Typewriter.CodeModel;

public sealed class FieldCollection : ItemCollection<Field>, IFieldCollection
{
    public FieldCollection()
    {
    }

    public FieldCollection(IEnumerable<Field> items)
        : base(items: items)
    {
    }

    protected override IEnumerable<string> GetAttributeFilter(Field item) => GetAttributeNames(attributes: item.Attributes);

    protected override IEnumerable<string> GetItemFilter(Field item) => [item.Name, item.FullName];
}
