namespace Typewriter.Abstractions;

public sealed record AttributeMetadata(
    string Name,
    string FullName,
    IReadOnlyList<AttributeArgumentMetadata> Arguments)
{
    public string AssemblyName { get; init; } = string.Empty;

    public TypeMetadataReference? Type { get; init; }
}
