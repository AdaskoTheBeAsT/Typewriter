namespace Typewriter.Abstractions;

public sealed record ConstantMetadata(
    string Name,
    string FullName,
    MetadataAccessibility Accessibility,
    TypeMetadataReference Type,
    string? Value,
    IReadOnlyList<AttributeMetadata> Attributes,
    string ParentTypeFullName)
{
    public string AssemblyName { get; init; } = string.Empty;

    public SourceLocation? Location { get; init; }

    public string? Documentation { get; init; }

    public DocCommentMetadata? DocComment { get; init; }
}
