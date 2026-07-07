using System.CommandLine;

namespace Typewriter.Cli;

internal static class CliCommandLine
{
    public static RootCommand CreateRootCommand(
        Func<CliOptions, CancellationToken, Task<int>> executeAsync)
    {
        ArgumentNullException.ThrowIfNull(argument: executeAsync);

        var rootOptions = CliOptionSet.Create();
        var rootCommand = new RootCommand(description: "Generate TypeScript from C# projects using Typewriter templates.");
        rootOptions.AddTo(command: rootCommand);
        rootCommand.SetAction(
            action: (parseResult, cancellationToken) =>
                executeAsync(arg1: rootOptions.CreateOptions(command: CliCommand.Generate, parseResult: parseResult), arg2: cancellationToken));

        rootCommand.Subcommands.Add(
            item: CreateInitCommand(executeAsync: executeAsync));
        rootCommand.Subcommands.Add(
            item: CreateCommand(
                name: "generate",
                description: "Generate TypeScript files from templates.",
                command: CliCommand.Generate,
                executeAsync: executeAsync));
        rootCommand.Subcommands.Add(
            item: CreateCommand(
                name: "validate",
                description: "Validate templates and project metadata without writing files.",
                command: CliCommand.Validate,
                executeAsync: executeAsync));
        rootCommand.Subcommands.Add(
            item: CreateCommand(
                name: "watch",
                description: "Watch C# projects and templates, regenerating after changes.",
                command: CliCommand.Watch,
                executeAsync: executeAsync));
        rootCommand.Subcommands.Add(
            item: CreateCommand(
                name: "list-templates",
                description: "List templates discovered for the workspace.",
                command: CliCommand.ListTemplates,
                executeAsync: executeAsync));

        return rootCommand;
    }

    private static Command CreateInitCommand(
        Func<CliOptions, CancellationToken, Task<int>> executeAsync)
    {
        ArgumentNullException.ThrowIfNull(argument: executeAsync);

        var workspace = new Option<string?>(name: "--workspace")
        {
            Description = "Path to the workspace folder, solution, or project where typewriter.json should be created.",
        };
        var force = new Option<bool>(name: "--force")
        {
            Description = "Overwrite an existing typewriter.json file.",
        };

        var cliCommand = new Command(name: "init", description: "Create a typewriter.json configuration file with default values.");
        cliCommand.Options.Add(item: workspace);
        cliCommand.Options.Add(item: force);
        cliCommand.SetAction(
            action: (parseResult, cancellationToken) =>
                executeAsync(
                    arg1: CliOptions.Default with
                    {
                        Command = CliCommand.Init,
                        WorkspacePath = parseResult.GetValue(option: workspace),
                        Force = parseResult.GetValue(option: force),
                    },
                    arg2: cancellationToken));
        return cliCommand;
    }

    private static Command CreateCommand(
        string name,
        string description,
        CliCommand command,
        Func<CliOptions, CancellationToken, Task<int>> executeAsync)
    {
        ArgumentNullException.ThrowIfNull(argument: executeAsync);

        var options = CliOptionSet.Create();
        var cliCommand = new Command(name: name, description: description);
        options.AddTo(command: cliCommand);
        cliCommand.SetAction(
            action: (parseResult, cancellationToken) =>
                executeAsync(arg1: options.CreateOptions(command: command, parseResult: parseResult), arg2: cancellationToken));
        return cliCommand;
    }

    private sealed class CliOptionSet
    {
#pragma warning disable S107
        private CliOptionSet(
            Option<string?> workspace,
            Option<string?> project,
            Option<string?> template,
            Option<string?> templateSearchPath,
            Option<string?> framework,
            Option<string> output,
            Option<bool> dryRun,
            Option<bool> failOnWarning,
            Option<bool> allProjects,
            Option<bool> diff,
            Option<string[]> changed)
#pragma warning restore S107
        {
            Workspace = workspace;
            Project = project;
            Template = template;
            TemplateSearchPath = templateSearchPath;
            Framework = framework;
            Output = output;
            DryRun = dryRun;
            FailOnWarning = failOnWarning;
            AllProjects = allProjects;
            Diff = diff;
            Changed = changed;
        }

        private Option<string?> Workspace { get; }

        private Option<string?> Project { get; }

        private Option<string?> Template { get; }

        private Option<string?> TemplateSearchPath { get; }

        private Option<string?> Framework { get; }

        private Option<string> Output { get; }

        private Option<bool> DryRun { get; }

        private Option<bool> FailOnWarning { get; }

        private Option<bool> AllProjects { get; }

        private Option<bool> Diff { get; }

        private Option<string[]> Changed { get; }

        public static CliOptionSet Create()
        {
            var output = new Option<string>(name: "--output")
            {
                Description = "Output format.",
                DefaultValueFactory = _ => "text",
            };
            output.AcceptOnlyFromAmong("text", "json");

            return new CliOptionSet(
                workspace: new Option<string?>(name: "--workspace")
                {
                    Description = "Path to a solution, project, or workspace folder.",
                },
                project: new Option<string?>(name: "--project")
                {
                    Description = "Path to the C# project to read.",
                },
                template: new Option<string?>(name: "--template")
                {
                    Description = "Path to a Typewriter template or template directory.",
                },
                templateSearchPath: new Option<string?>(name: "--template-search-path")
                {
                    Description = "Directory used to discover templates without changing the workspace or project.",
                },
                framework: new Option<string?>(name: "--framework")
                {
                    Description = "Target framework to use when loading project metadata.",
                },
                output: output,
                dryRun: new Option<bool>(name: "--dry-run")
                {
                    Description = "Run generation without writing files.",
                },
                failOnWarning: new Option<bool>(name: "--fail-on-warning")
                {
                    Description = "Return a non-zero exit code when warnings are emitted.",
                },
                allProjects: new Option<bool>(name: "--all-projects")
                {
                    Description = "Generate for every project in a multi-project workspace.",
                },
                diff: new Option<bool>(name: "--diff")
                {
                    Description = "Include unified diffs for changed files in the output.",
                },
                changed: new Option<string[]>(name: "--changed")
                {
                    Description = "Path of an input file changed since the previous generation. May be repeated. When every changed input is a C# source file, only the affected outputs are re-rendered.",
                });
        }

        public void AddTo(Command command)
        {
            command.Options.Add(item: Workspace);
            command.Options.Add(item: Project);
            command.Options.Add(item: Template);
            command.Options.Add(item: TemplateSearchPath);
            command.Options.Add(item: Framework);
            command.Options.Add(item: Output);
            command.Options.Add(item: DryRun);
            command.Options.Add(item: FailOnWarning);
            command.Options.Add(item: AllProjects);
            command.Options.Add(item: Diff);
            command.Options.Add(item: Changed);
        }

        public CliOptions CreateOptions(
            CliCommand command,
            ParseResult parseResult) =>
            new(
                Command: command,
                WorkspacePath: parseResult.GetValue(option: Workspace),
                ProjectPath: parseResult.GetValue(option: Project),
                TemplatePath: parseResult.GetValue(option: Template),
                TemplateSearchPath: parseResult.GetValue(option: TemplateSearchPath),
                Framework: parseResult.GetValue(option: Framework),
                Output: (parseResult.GetValue(option: Output) ?? "text").ToLowerInvariant(),
                DryRun: parseResult.GetValue(option: DryRun),
                FailOnWarning: parseResult.GetValue(option: FailOnWarning),
                AllProjects: parseResult.GetValue(option: AllProjects),
                Diff: parseResult.GetValue(option: Diff),
                Force: false,
                Help: false)
            {
                ChangedPaths = parseResult.GetValue(option: Changed) ?? [],
            };
    }
}
