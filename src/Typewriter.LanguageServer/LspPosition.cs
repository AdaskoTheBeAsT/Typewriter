namespace Typewriter.LanguageServer;

internal sealed record LspPosition(
    int Line,
    int Character);
