namespace Typewriter.LanguageServer;

internal sealed record LspDiagnostic(
    LspRange Range,
    string Message,
    int Severity,
    string Source,
    string? Code);
