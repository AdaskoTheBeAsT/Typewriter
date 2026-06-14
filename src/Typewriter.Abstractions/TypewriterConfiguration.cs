namespace Typewriter.Abstractions;

public sealed record TypewriterConfiguration(
    IReadOnlyList<string> Templates,
    IReadOnlyList<string> Exclude,
    string? DefaultTargetFramework,
    OutputConfiguration Output,
    DiagnosticsConfiguration Diagnostics)
{
    public static TypewriterConfiguration Default { get; } = new(
        Templates: ["**/*.tst"],
        Exclude: ["**/bin/**", "**/obj/**", "**/node_modules/**"],
        DefaultTargetFramework: null,
        Output: OutputConfiguration.Default,
        Diagnostics: DiagnosticsConfiguration.Default);
}
