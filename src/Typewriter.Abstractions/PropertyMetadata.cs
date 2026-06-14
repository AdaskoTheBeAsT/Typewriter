namespace Typewriter.Abstractions;

public sealed record PropertyMetadata(
    string Name,
    string FullName,
    TypeMetadataReference Type,
    MetadataAccessibility Accessibility,
    bool HasGetter,
    bool HasSetter,
    bool IsRequired,
    IReadOnlyList<AttributeMetadata> Attributes)
{
    public string ParentTypeFullName { get; init; } = string.Empty;

    public string AssemblyName { get; init; } = string.Empty;

    public SourceLocation? Location { get; init; }

    public string? Documentation { get; init; }

    public DocCommentMetadata? DocComment { get; init; }

    public bool IsAbstract { get; init; }

    public bool IsVirtual { get; init; }
}
