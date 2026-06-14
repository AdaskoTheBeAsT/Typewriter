namespace Typewriter.LanguageServer;

internal sealed record EmbeddedLanguageRegion(
    EmbeddedLanguageKind Kind,
    int Start,
    int End,
    int ContentStart,
    int ContentEnd);
