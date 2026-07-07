namespace Typewriter.Abstractions;

public sealed record GenerationRequest(
    string? WorkspacePath,
    string? ProjectPath,
    string? TemplatePath,
    GenerationMode Mode,
    TypewriterConfiguration Configuration,
    bool AllProjects = false,
    string? TemplateSearchPath = null)
{
    public bool IncludeDiff { get; init; }

    /// <summary>
    /// The inputs known to have changed since the previous successful generation.
    /// <c>null</c> means the provenance is unknown and a full generation is performed.
    /// A non-null value allows the generator to scope per-source-file rendering to the
    /// affected files when <see cref="GenerationConfiguration.IsIncrementalEnabled"/> allows it.
    /// </summary>
    public IReadOnlyCollection<ChangedInput>? ChangedInputs { get; init; }
}
