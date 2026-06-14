namespace Typewriter.Abstractions;

public interface IProjectMetadataProvider
{
    Task<ProjectMetadata> GetMetadataAsync(
        ProjectContext project,
        CancellationToken cancellationToken);
}
