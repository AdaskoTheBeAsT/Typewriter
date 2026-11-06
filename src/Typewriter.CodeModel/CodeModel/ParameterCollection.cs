namespace Typewriter.CodeModel;

public sealed class ParameterCollection : ItemCollection<Parameter>, IParameterCollection
{
    public ParameterCollection()
    {
    }

    public ParameterCollection(IEnumerable<Parameter> items)
        : base(items: items)
    {
    }

    protected override IEnumerable<string> GetAttributeFilter(Parameter item) => GetAttributeNames(attributes: item.Attributes);

    protected override IEnumerable<string> GetItemFilter(Parameter item) => [item.Name, item.FullName];
}
