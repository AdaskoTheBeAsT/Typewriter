namespace Typewriter.LanguageServer;

internal sealed record EmbeddedSpanMapping(
    int TemplateStart,
    int VirtualStart,
    int Length);
