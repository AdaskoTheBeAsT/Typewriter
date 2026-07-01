using System.Collections.Generic;

namespace Typewriter.VisualStudio;

internal sealed class CliInvocation
{
    public CliInvocation(
        string command,
        IReadOnlyList<string> arguments)
    {
        Command = command;
        Arguments = arguments;
    }

    public string Command { get; }

    public IReadOnlyList<string> Arguments { get; }
}

internal sealed class CliProcessResult
{
    public CliProcessResult(
        int exitCode,
        string standardOutput,
        string standardError)
    {
        ExitCode = exitCode;
        StandardOutput = standardOutput;
        StandardError = standardError;
    }

    public int ExitCode { get; }

    public string StandardOutput { get; }

    public string StandardError { get; }
}

internal sealed class CliResult
{
    public bool Success { get; set; }

    public long DurationMs { get; set; }

    public List<CliGeneratedFile> GeneratedFiles { get; set; } = [];

    public List<CliDiagnostic> Diagnostics { get; set; } = [];
}

internal sealed class TypewriterGenerationRequest
{
    public string Command { get; set; } = string.Empty;

    public string WorkspacePath { get; set; } = string.Empty;

    public string? ProjectPath { get; set; }

    public string? TemplatePath { get; set; }

    public string? TemplateSearchPath { get; set; }

    public string? Framework { get; set; }

    public bool AllProjects { get; set; }
}

internal sealed class CliGeneratedFile
{
    public string Path { get; set; } = string.Empty;

    public bool Changed { get; set; }
}

internal sealed class CliDiagnostic
{
    public string? File { get; set; }

    public int? Line { get; set; }

    public int? Column { get; set; }

    public string Severity { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string? Code { get; set; }

    public string? HelpLink { get; set; }
}

internal sealed class GenerationContext
{
    public GenerationContext(
        string workspacePath,
        string? projectPath,
        string? templatePath,
        string? templateSearchPath,
        string framework,
        bool allProjects,
        string workingDirectory,
        CliInvocation cliInvocation)
    {
        WorkspacePath = workspacePath;
        ProjectPath = projectPath;
        TemplatePath = templatePath;
        TemplateSearchPath = templateSearchPath;
        Framework = framework;
        AllProjects = allProjects;
        WorkingDirectory = workingDirectory;
        CliInvocation = cliInvocation;
    }

    public string WorkspacePath { get; }

    public string? ProjectPath { get; }

    public string? TemplatePath { get; }

    public string? TemplateSearchPath { get; }

    public string Framework { get; }

    public bool AllProjects { get; }

    public string WorkingDirectory { get; }

    public CliInvocation CliInvocation { get; }
}
