namespace Typewriter.LanguageServer;

internal sealed record LspHover(
    LspMarkupContent Contents,
    LspRange? Range = null);
