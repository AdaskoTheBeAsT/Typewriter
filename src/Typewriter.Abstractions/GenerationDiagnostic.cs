namespace Typewriter.Abstractions;

public sealed record GenerationDiagnostic(
    string? File,
    int? Line,
    int? Column,
    DiagnosticSeverity Severity,
    string Message,
    string? Code = null,
    string? HelpLink = null);
