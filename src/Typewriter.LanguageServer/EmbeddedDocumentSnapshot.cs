namespace Typewriter.LanguageServer;

internal sealed record EmbeddedDocumentSnapshot(
    string Kind,
    string LanguageId,
    string Content,
    int? Version);
