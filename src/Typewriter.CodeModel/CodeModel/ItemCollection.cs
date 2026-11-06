using System.Collections;

namespace Typewriter.CodeModel;

public class ItemCollection<T> : IItemCollection<T>
    where T : Item
{
    private readonly IReadOnlyList<T> _items;

    public ItemCollection()
        : this(items: [])
    {
    }

    public ItemCollection(IEnumerable<T> items)
    {
        _items = items.ToArray();
    }

    public int Count => _items.Count;

    public virtual Func<Item, IEnumerable<string>> AttributeFilterSelector => item => item is T typed ? GetAttributeFilter(item: typed) : [];

    public virtual Func<Item, IEnumerable<string>> InheritanceFilterSelector => item => item is T typed ? GetInheritanceFilter(item: typed) : [];

    public virtual Func<Item, IEnumerable<string>> ItemFilterSelector => item => item is T typed ? GetItemFilter(item: typed) : [];

    public T this[int index] => _items[index: index];

    public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    protected static IEnumerable<string> GetAttributeNames(IAttributeCollection attributes)
    {
        foreach (var attribute in attributes)
        {
            yield return attribute.Name;
            yield return attribute.FullName;
        }
    }

    protected virtual IEnumerable<string> GetAttributeFilter(T item) => [];

    protected virtual IEnumerable<string> GetInheritanceFilter(T item) => [];

    protected virtual IEnumerable<string> GetItemFilter(T item) => [item.Name, item.FullName];
}
