namespace Typewriter.LanguageServer;

internal sealed record LspRange(
    LspPosition Start,
    LspPosition End);
