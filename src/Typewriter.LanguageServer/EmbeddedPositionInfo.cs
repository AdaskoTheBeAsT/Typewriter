namespace Typewriter.LanguageServer;

internal sealed record EmbeddedPositionInfo(
    string Kind,
    LspPosition? VirtualPosition);
