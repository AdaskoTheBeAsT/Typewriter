using Typewriter.Abstractions;

namespace Typewriter.LanguageServer;

internal interface ITemplateDiagnosticService
{
    Task<IReadOnlyList<GenerationDiagnostic>> ValidateAsync(
        TextDocumentState document,
        LanguageServerSettings settings,
        CancellationToken cancellationToken);
}
