using Typewriter.Abstractions;

namespace Typewriter.Buildalyzer;

public interface IProjectWorkspaceLoader
{
    /// <summary>
    /// Creates a loader scoped to one metadata request, including its referenced projects.
    /// Custom loaders retain their existing behavior unless they opt into session reuse.
    /// </summary>
    /// <returns>The request-scoped loader.</returns>
    IProjectWorkspaceLoader CreateSession() => this;

    Task<ProjectLoadResult> LoadAsync(
        ProjectContext project,
        CancellationToken cancellationToken);
}
