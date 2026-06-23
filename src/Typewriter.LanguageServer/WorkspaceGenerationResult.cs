namespace Typewriter.LanguageServer;

internal sealed record WorkspaceGenerationResult(
    bool Success,
    long DurationMs,
    IReadOnlyList<WorkspaceGeneratedFile> GeneratedFiles,
    IReadOnlyList<WorkspaceGenerationDiagnostic> Diagnostics);
