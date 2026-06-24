namespace Typewriter.Abstractions;

public sealed record TypewriterConfiguration(
    IReadOnlyList<string> Templates,
    IReadOnlyList<string> Exclude,
    IReadOnlyList<string> InputExtensions,
    string? DefaultTargetFramework,
    OutputConfiguration Output,
    DiagnosticsConfiguration Diagnostics)
{
    public static IReadOnlyList<string> DefaultInputExtensions { get; } =
    [
        ".cs",
        ".csproj",
        ".json",
        ".props",
        ".sln",
        ".slnx",
        ".targets",
        ".tst",
    ];

    public static TypewriterConfiguration Default { get; } = new(
        Templates: ["**/*.tst"],
        Exclude: ["**/bin/**", "**/obj/**", "**/node_modules/**"],
        InputExtensions: DefaultInputExtensions,
        DefaultTargetFramework: null,
        Output: OutputConfiguration.Default,
        Diagnostics: DiagnosticsConfiguration.Default);
}
