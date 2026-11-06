namespace Typewriter.CodeModel;

public interface IFilterable
{
    Func<Item, IEnumerable<string>> AttributeFilterSelector { get; }

    Func<Item, IEnumerable<string>> InheritanceFilterSelector { get; }

    Func<Item, IEnumerable<string>> ItemFilterSelector { get; }
}
