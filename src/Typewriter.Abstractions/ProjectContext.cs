namespace Typewriter.Abstractions;

public sealed record ProjectContext(
    string ProjectPath,
    string WorkspacePath,
    string? TargetFramework = null,
    bool RunFullDiagnostics = false)
{
    /// <summary>Optional request-local performance observer. Not part of metadata cache identity.</summary>
    public Action<MetadataStageTiming>? PerformanceSink { get; init; }
}
