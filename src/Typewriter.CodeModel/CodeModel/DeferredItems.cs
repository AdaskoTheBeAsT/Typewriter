using System.Collections;

namespace Typewriter.CodeModel;

internal sealed class DeferredItems<T> : IReadOnlyList<T>
    where T : Item
{
    private readonly object _sync = new();
    private readonly Lazy<IReadOnlyList<Lazy<T>>> _items;
    private Item? _parent;

    private DeferredItems(Func<IReadOnlyList<Lazy<T>>> itemsFactory)
    {
        _items = new Lazy<IReadOnlyList<Lazy<T>>>(valueFactory: itemsFactory);
    }

    public int Count => _items.Value.Count;

    public T this[int index]
    {
        get
        {
            lock (_sync)
            {
                var slot = _items.Value[index];
                var created = slot.IsValueCreated;
                var item = slot.Value;
                if (!created && _parent is not null)
                {
                    item.SetParent(parent: _parent);
                }

                return item;
            }
        }
    }

    public static DeferredItems<T> Create<TSource>(IEnumerable<TSource> source, Func<TSource, T> selector)
    {
        if (selector is null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        return new DeferredItems<T>(itemsFactory: () => source.Select(selector: item => new Lazy<T>(valueFactory: () => selector(arg: item))).ToArray());
    }

    public IEnumerator<T> GetEnumerator()
    {
        for (var index = 0; index < Count; index++)
        {
            yield return this[index];
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void SetParent(Item parent)
    {
        lock (_sync)
        {
            _parent = parent;
            if (!_items.IsValueCreated)
            {
                return;
            }

            foreach (var slot in _items.Value)
            {
                if (slot.IsValueCreated)
                {
                    slot.Value.SetParent(parent: parent);
                }
            }
        }
    }
}
