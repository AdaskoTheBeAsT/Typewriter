using Typewriter.Abstractions;

namespace Typewriter.LanguageServer;

internal sealed class InMemoryTemplateDiscovery : ITemplateDiscovery
{
    private readonly TextDocumentState _document;

    public InMemoryTemplateDiscovery(TextDocumentState document)
    {
        _document = document;
    }

    public Task<IReadOnlyList<TemplateFile>> FindTemplatesAsync(
        WorkspaceContext workspace,
        GenerationRequest request,
        CancellationToken cancellationToken)
    {
        _ = workspace;
        _ = request;
        _ = cancellationToken;

#pragma warning disable CC0001 // You should use 'var' whenever possible.
        IReadOnlyList<TemplateFile> templates =
        [
            new TemplateFile(Path: _document.Path, Content: _document.Text),
        ];
#pragma warning restore CC0001 // You should use 'var' whenever possible.
        return Task.FromResult(result: templates);
    }
}
