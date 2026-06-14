namespace Typewriter.LanguageServer;

internal sealed record LspCompletionItem(
    string Label,
    int Kind,
    string? Detail = null,
    string? Documentation = null,
    string? InsertText = null,
    int? InsertTextFormat = null);
