namespace Typewriter.LanguageServer;

internal sealed record WorkspaceGenerationRequest(
    string? Command,
    string? WorkspacePath,
    string? ProjectPath,
    string? TemplatePath,
    string? Framework,
    bool? AllProjects,
    string? TemplateSearchPath = null)
{
    /// <summary>
    /// Gets the inputs changed since the previous generation, or <c>null</c> when the
    /// provenance is unknown and a full generation is performed.
    /// </summary>
    public IReadOnlyList<WorkspaceChangedInput>? ChangedInputs { get; init; }
}
