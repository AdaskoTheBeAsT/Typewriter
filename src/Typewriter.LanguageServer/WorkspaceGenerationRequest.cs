namespace Typewriter.LanguageServer;

internal sealed record WorkspaceGenerationRequest(
    string? Command,
    string? WorkspacePath,
    string? ProjectPath,
    string? TemplatePath,
    string? Framework,
    bool? AllProjects,
    string? TemplateSearchPath = null);
