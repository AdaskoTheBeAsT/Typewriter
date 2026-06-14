namespace Typewriter.LanguageServer;

internal sealed record EmbeddedCSharpCompletions(
    IReadOnlyList<LspCompletionItem> Items,
    bool IsIncomplete);
