namespace Typewriter.LanguageServer;

internal sealed record TemplateToken(
    string Text,
    int Start,
    int End,
    bool HasTemplatePrefix);
