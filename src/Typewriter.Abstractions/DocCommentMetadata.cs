namespace Typewriter.Abstractions;

public sealed record DocCommentMetadata(
    string Summary,
    string Returns,
    IReadOnlyList<ParameterCommentMetadata> Parameters);
