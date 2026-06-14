namespace Typewriter.Abstractions;

public interface IGeneratedFileWriter
{
    Task<GeneratedFile> WriteAsync(
        GeneratedFile file,
        GenerationRequest request,
        CancellationToken cancellationToken);
}
