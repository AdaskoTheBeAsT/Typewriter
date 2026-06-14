namespace Typewriter.LanguageServer;

internal sealed record LspCompletionList(
    bool IsIncomplete,
    IReadOnlyList<LspCompletionItem> Items);
