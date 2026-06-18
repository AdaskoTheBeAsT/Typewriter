using Typewriter.Abstractions;

namespace Typewriter.Buildalyzer;

public sealed record ProjectLoadResult(
    string ProjectPath,
    string ProjectDirectory,
    string? TargetFramework,
    bool NullableEnabled,
    bool ImplicitUsingsEnabled,
    IReadOnlyList<string> SourceFiles,
    IReadOnlyList<string> PreprocessorSymbols,
    IReadOnlyList<string> GlobalUsings,
    IReadOnlyList<string> ProjectReferences,
    IReadOnlyList<string> ReferencePaths,
    IReadOnlyList<string> AnalyzerReferences,
    IReadOnlyList<string> AdditionalFiles,
    IReadOnlyList<string> AnalyzerConfigFiles,
    IReadOnlyList<GenerationDiagnostic> Diagnostics);
