namespace Typewriter.Abstractions;

public interface IGenerationReporter
{
    void Info(string message);

    void Warning(GenerationDiagnostic diagnostic);

    void Error(GenerationDiagnostic diagnostic);
}
