using System.Collections;

namespace Typewriter.Engine.Tests;

internal sealed class AdapterCountingList<T>(IReadOnlyList<T> items, int? maximumItemReads = null) : IReadOnlyList<T>
{
    private int _enumerations;
    private int _itemReads;

    public int Enumerations
    {
        get => Volatile.Read(location: ref _enumerations);
        set => Volatile.Write(location: ref _enumerations, value: value);
    }

    public int ItemReads => Volatile.Read(location: ref _itemReads);

    public int Count => items.Count;

    public T this[int index]
    {
        get
        {
            var reads = Interlocked.Increment(location: ref _itemReads);
            if (maximumItemReads is { } maximum && reads > maximum)
            {
                throw new InvalidOperationException(message: "Metadata traversal exceeded its deterministic item-read budget.");
            }

            return items[index];
        }
    }

    public IEnumerator<T> GetEnumerator()
    {
        Interlocked.Increment(location: ref _enumerations);
        return items.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
