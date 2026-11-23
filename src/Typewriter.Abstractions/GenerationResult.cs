namespace Typewriter.Abstractions;

public sealed record GenerationResult(
    bool Success,
    IReadOnlyList<GeneratedFile> GeneratedFiles,
    IReadOnlyList<GenerationDiagnostic> Diagnostics,
    TimeSpan Duration)
{
    public IReadOnlyList<string> DeletedFiles { get; init; } = [];
}
