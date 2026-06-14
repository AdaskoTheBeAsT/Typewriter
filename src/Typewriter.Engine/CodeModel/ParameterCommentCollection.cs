namespace Typewriter.CodeModel;

public sealed class ParameterCommentCollection : ItemCollection<ParameterComment>, IParameterCommentCollection
{
    public ParameterCommentCollection()
    {
    }

    public ParameterCommentCollection(IEnumerable<ParameterComment> items)
        : base(items: items)
    {
    }

    protected override IEnumerable<string> GetItemFilter(ParameterComment item) => [item.Name];
}
