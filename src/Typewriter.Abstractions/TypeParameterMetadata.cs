namespace Typewriter.Abstractions;

public sealed record TypeParameterMetadata(
    string Name)
{
    public string FullName { get; init; } = Name;

    /// <summary>Gets the declaration identity used to match generic parameter references.</summary>
    public string? Key { get; init; }

    public string? Documentation { get; init; }
}
