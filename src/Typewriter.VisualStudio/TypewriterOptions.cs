using System.ComponentModel;
using Microsoft.VisualStudio.Shell;

namespace Typewriter.VisualStudio;

public sealed class TypewriterOptions : DialogPage
{
    [Category(category: "CLI")]
    [DisplayName(displayName: "CLI path")]
    [Description(description: "Path to the Typewriter CLI executable. Leave empty to use the local repo CLI when available, then fall back to typewriter on PATH.")]
    public string CliPath { get; set; } = string.Empty;

    [Category(category: "CLI")]
    [DisplayName(displayName: "CLI arguments")]
    [Description(description: "Arguments inserted before the Typewriter command. Useful when CLI path is dotnet.")]
    public string CliArguments { get; set; } = string.Empty;

    [Category(category: "Language Server")]
    [DisplayName(displayName: "Language server enabled")]
    [Description(description: "Start the Typewriter language server for .tst IntelliSense.")]
    public bool LanguageServerEnabled { get; set; } = true;

    [Category(category: "Language Server")]
    [DisplayName(displayName: "Language server path")]
    [Description(description: "Path to the Typewriter language server executable. Leave empty to use the packaged server, then fall back to typewriter-lsp on PATH.")]
    public string LanguageServerPath { get; set; } = string.Empty;

    [Category(category: "Language Server")]
    [DisplayName(displayName: "Language server arguments")]
    [Description(description: "Arguments inserted before the language server command. Useful when language server path is dotnet.")]
    public string LanguageServerArguments { get; set; } = string.Empty;

    [Category(category: "Generation")]
    [DisplayName(displayName: "Workspace path")]
    [Description(description: "Optional workspace, solution, project, or folder path passed to --workspace.")]
    public string WorkspacePath { get; set; } = string.Empty;

    [Category(category: "Generation")]
    [DisplayName(displayName: "Project path")]
    [Description(description: "Optional project path passed to --project.")]
    public string ProjectPath { get; set; } = string.Empty;

    [Category(category: "Generation")]
    [DisplayName(displayName: "Target framework")]
    [Description(description: "Optional target framework passed to --framework.")]
    public string Framework { get; set; } = string.Empty;

    [Category(category: "Generation")]
    [DisplayName(displayName: "Generate all projects")]
    [Description(description: "Pass --all-projects when generating all templates in a multi-project workspace.")]
    public bool AllProjects { get; set; }

    [Category(category: "Generation")]
    [DisplayName(displayName: "Generate on save")]
    [Description(description: "Generate after saving files matched by typewriter.json inputExtensions.")]
    public bool GenerateOnSave { get; set; } = true;
}
