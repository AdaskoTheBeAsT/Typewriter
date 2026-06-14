namespace Typewriter.Abstractions;

public interface ITypewriterGenerator
{
    Task<GenerationResult> GenerateAsync(
        GenerationRequest request,
        CancellationToken cancellationToken);
}
