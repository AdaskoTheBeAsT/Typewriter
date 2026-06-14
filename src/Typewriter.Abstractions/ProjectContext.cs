namespace Typewriter.Abstractions;

public sealed record ProjectContext(
    string ProjectPath,
    string WorkspacePath,
    string? TargetFramework = null);
