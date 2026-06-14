namespace Typewriter.Abstractions;

public sealed record EnumValueMetadata(
    string Name,
    long? Value)
{
    public string AssemblyName { get; init; } = string.Empty;

    public IReadOnlyList<AttributeMetadata> Attributes { get; init; } = [];

    public string ParentTypeFullName { get; init; } = string.Empty;

    public SourceLocation? Location { get; init; }

    public string? Documentation { get; init; }

    public DocCommentMetadata? DocComment { get; init; }
}
