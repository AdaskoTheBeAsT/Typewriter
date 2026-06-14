namespace Typewriter.Abstractions;

public sealed record AttributeArgumentMetadata(
    string? Name,
    string? Value)
{
    public string AssemblyName { get; init; } = string.Empty;

    public TypeMetadataReference? Type { get; init; }

    public TypeMetadataReference? TypeValue { get; init; }
}
