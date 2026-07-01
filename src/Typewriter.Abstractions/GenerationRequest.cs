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
}
