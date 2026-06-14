namespace Typewriter.Abstractions;

public sealed record SourceLocation(
    string Path,
    int Line,
    int Column);
