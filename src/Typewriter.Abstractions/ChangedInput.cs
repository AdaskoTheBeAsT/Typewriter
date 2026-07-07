namespace Typewriter.Abstractions;

public sealed record ChangedInput(
    string FullPath,
    ChangedInputKind Kind);
