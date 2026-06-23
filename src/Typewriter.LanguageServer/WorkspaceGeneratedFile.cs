namespace Typewriter.LanguageServer;

internal sealed record WorkspaceGeneratedFile(
    string Path,
    bool Changed);
