namespace Typewriter.Abstractions;

public sealed record GeneratedFile(
    string Path,
    string Content,
    bool Changed,
    bool? Utf8Bom = null)
{
    public string? Diff { get; init; }
}
