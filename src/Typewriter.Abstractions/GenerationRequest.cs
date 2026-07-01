namespace Typewriter.Abstractions;

public sealed record GenerationRequest(
    string? WorkspacePath,
    string? ProjectPath,
    string? TemplatePath,
    GenerationMode Mode,
    TypewriterConfiguration Configuration,
    bool AllProjects = false,
    string? TemplateSearchPath = null,
    bool IncludeDiff = false);
