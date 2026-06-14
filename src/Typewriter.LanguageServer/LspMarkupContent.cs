namespace Typewriter.LanguageServer;

internal sealed record LspMarkupContent(
    string Kind,
    string Value);
