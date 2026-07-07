namespace Typewriter.LanguageServer;

internal sealed record WorkspaceChangedInput(
    string? FullPath,
    string? Kind);
