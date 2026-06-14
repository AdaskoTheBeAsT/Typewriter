namespace Typewriter.Abstractions;

public sealed record SourceFileMetadata(
    string Path,
    IReadOnlyList<TypeMetadata> Types)
{
    public IReadOnlyList<DelegateMetadata> Delegates { get; init; } = [];
}
