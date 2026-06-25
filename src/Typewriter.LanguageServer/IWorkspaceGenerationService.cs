namespace Typewriter.LanguageServer;

internal interface IWorkspaceGenerationService
{
    Task<WorkspaceGenerationResult> GenerateAsync(
        WorkspaceGenerationRequest request,
        LanguageServerSettings settings,
        CancellationToken cancellationToken);
}
