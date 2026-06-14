namespace Typewriter.Abstractions;

public interface ITemplateDiscovery
{
    Task<IReadOnlyList<TemplateFile>> FindTemplatesAsync(
        WorkspaceContext workspace,
        GenerationRequest request,
        CancellationToken cancellationToken);
}
