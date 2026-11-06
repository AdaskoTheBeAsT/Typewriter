namespace Typewriter.CodeModel;

public sealed class MethodCollection : ItemCollection<Method>, IMethodCollection
{
    public MethodCollection()
    {
    }

    public MethodCollection(IEnumerable<Method> items)
        : base(items: items)
    {
    }

    protected override IEnumerable<string> GetAttributeFilter(Method item) => GetAttributeNames(attributes: item.Attributes);

    protected override IEnumerable<string> GetItemFilter(Method item) => [item.Name, item.FullName];
}
