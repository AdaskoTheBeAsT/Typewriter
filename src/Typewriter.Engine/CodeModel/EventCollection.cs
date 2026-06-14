namespace Typewriter.CodeModel;

public sealed class EventCollection : ItemCollection<Event>, IEventCollection
{
    public EventCollection()
    {
    }

    public EventCollection(IEnumerable<Event> items)
        : base(items: items)
    {
    }

    protected override IEnumerable<string> GetAttributeFilter(Event item) => GetAttributeNames(attributes: item.Attributes);

    protected override IEnumerable<string> GetItemFilter(Event item) => [item.Name, item.FullName];
}
