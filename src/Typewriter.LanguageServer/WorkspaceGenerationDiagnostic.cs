namespace Typewriter.LanguageServer;

internal sealed record WorkspaceGenerationDiagnostic(
    string? File,
    int? Line,
    int? Column,
    string Severity,
    string Message,
    string? Code,
    string? HelpLink);
