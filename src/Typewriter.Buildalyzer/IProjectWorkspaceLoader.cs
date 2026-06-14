using Typewriter.Abstractions;

namespace Typewriter.Buildalyzer;

public interface IProjectWorkspaceLoader
{
    Task<ProjectLoadResult> LoadAsync(
        ProjectContext project,
        CancellationToken cancellationToken);
}
