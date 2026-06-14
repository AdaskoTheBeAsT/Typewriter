namespace Typewriter.CodeModel;

public sealed class TypeParameterCollection : ItemCollection<TypeParameter>, ITypeParameterCollection
{
    public TypeParameterCollection()
    {
    }

    public TypeParameterCollection(IEnumerable<TypeParameter> items)
        : base(items: items)
    {
    }

    public override string ToString() => Count == 0
        ? string.Empty
        : string.Concat(str0: "<", str1: string.Join(separator: ", ", values: this.Select(selector: item => item.Name)), str2: ">");

    protected override IEnumerable<string> GetItemFilter(TypeParameter item) => [item.Name];
}
