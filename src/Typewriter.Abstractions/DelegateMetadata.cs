namespace Typewriter.Abstractions;

public sealed record DelegateMetadata(
    string Name,
    string FullName,
    MetadataAccessibility Accessibility,
    TypeMetadataReference ReturnType,
    bool IsGeneric,
    IReadOnlyList<ParameterMetadata> Parameters,
    IReadOnlyList<AttributeMetadata> Attributes,
    string ParentTypeFullName)
{
    public string AssemblyName { get; init; } = string.Empty;

    public SourceLocation? Location { get; init; }

    public string? Documentation { get; init; }

    public DocCommentMetadata? DocComment { get; init; }

    public IReadOnlyList<TypeParameterMetadata> TypeParameters { get; init; } = [];
}
