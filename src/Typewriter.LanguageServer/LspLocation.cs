namespace Typewriter.LanguageServer;

internal sealed record LspLocation(
    string Uri,
    LspRange Range);
