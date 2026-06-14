namespace Typewriter.CodeModel;

public interface IItemCollection<out T> : IReadOnlyList<T>, IFilterable
    where T : Item
{
}
