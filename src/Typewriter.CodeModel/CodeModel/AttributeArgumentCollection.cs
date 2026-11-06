namespace Typewriter.CodeModel;

public sealed class AttributeArgumentCollection : ItemCollection<AttributeArgument>, IAttributeArgumentCollection
{
    public AttributeArgumentCollection()
    {
    }

    public AttributeArgumentCollection(IEnumerable<AttributeArgument> items)
        : base(items: items)
    {
    }
}
