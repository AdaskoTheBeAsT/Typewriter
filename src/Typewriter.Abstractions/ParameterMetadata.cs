namespace Typewriter.Abstractions;

public sealed record ParameterMetadata(
    string Name,
    string FullName,
    TypeMetadataReference Type,
    bool HasDefaultValue,
    string? DefaultValue,
    IReadOnlyList<AttributeMetadata> Attributes,
    string ParentMethodFullName)
{
    public string AssemblyName { get; init; } = string.Empty;

    public SourceLocation? Location { get; init; }

    public string? Documentation { get; init; }

    public DocCommentMetadata? DocComment { get; init; }
}
