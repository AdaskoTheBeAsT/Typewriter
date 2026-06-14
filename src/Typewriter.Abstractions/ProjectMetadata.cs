namespace Typewriter.Abstractions;

public sealed record ProjectMetadata(
    string ProjectPath,
    IReadOnlyList<SourceFileMetadata> SourceFiles,
    IReadOnlyList<TypeMetadata> Types,
    IReadOnlyList<GenerationDiagnostic> Diagnostics)
{
    public IReadOnlyList<DelegateMetadata> Delegates { get; init; } = [];
}
