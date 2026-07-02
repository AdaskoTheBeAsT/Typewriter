namespace Typewriter.Cli;

internal sealed record CliOptions(
    CliCommand Command,
    string? WorkspacePath,
    string? ProjectPath,
    string? TemplatePath,
    string? TemplateSearchPath,
    string? Framework,
    string Output,
    bool DryRun,
    bool FailOnWarning,
    bool AllProjects,
    bool Diff,
    bool Force,
    bool Help)
{
    public static CliOptions Default { get; } = new(
        Command: CliCommand.Generate,
        WorkspacePath: null,
        ProjectPath: null,
        TemplatePath: null,
        TemplateSearchPath: null,
        Framework: null,
        Output: "text",
        DryRun: false,
        FailOnWarning: false,
        AllProjects: false,
        Diff: false,
        Force: false,
        Help: false);
}
