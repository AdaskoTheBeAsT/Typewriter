namespace Typewriter.CodeModel;

internal interface IDeferredItemCollection
{
    bool TrySetParent(Item parent);
}
