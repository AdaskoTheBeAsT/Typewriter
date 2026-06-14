namespace Typewriter.Abstractions;

public sealed record TypeParameterMetadata(
    string Name)
{
    public string FullName { get; init; } = Name;

    public string? Documentation { get; init; }
}
