using System.CommandLine.Invocation;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Typewriter.Abstractions;
using Typewriter.Engine;
using Typewriter.Roslyn;

namespace Typewriter.Cli;

internal static class TypewriterCli
{
    private const string DefaultConfigurationFileName = "typewriter.json";
    private static readonly TimeSpan WatchDebounceDelay = TimeSpan.FromMilliseconds(milliseconds: 300);

    public static async Task<int> RunAsync(
        string[] args,
        CancellationToken cancellationToken)
    {
        var rootCommand = CliCommandLine.CreateRootCommand(executeAsync: ExecuteAsync);
        var parseResult = rootCommand.Parse(args: args);
        if (parseResult.Action is ParseErrorAction)
        {
            await rootCommand.Parse(args: ["--help"]).InvokeAsync(configuration: null, cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
            foreach (var parseError in parseResult.Errors)
            {
                await Console.Error.WriteLineAsync(parseError.Message.ToCharArray(), cancellationToken).ConfigureAwait(false);
            }

            Environment.ExitCode = 2;
            return 2;
        }

        return await parseResult.InvokeAsync(configuration: null, cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
    }

    private static Task<int> ExecuteAsync(
        CliOptions options,
        CancellationToken cancellationToken) =>
        options.Command switch
        {
            CliCommand.Init => InitAsync(options: options, cancellationToken: cancellationToken),
            CliCommand.ListTemplates => ListTemplatesAsync(options: options, cancellationToken: cancellationToken),
            CliCommand.Watch => WatchAsync(options: options, cancellationToken: cancellationToken),
            _ => GenerateAsync(options: options, cancellationToken: cancellationToken),
        };

    private static async Task<int> InitAsync(
        CliOptions options,
        CancellationToken cancellationToken)
    {
        var directory = ResolveConfigurationDirectory(workspacePath: options.WorkspacePath);
        Directory.CreateDirectory(path: directory);

        var configurationPath = Path.Combine(path1: directory, path2: DefaultConfigurationFileName);
        var existed = File.Exists(path: configurationPath);
        if (existed && !options.Force)
        {
            await Console.Error.WriteLineAsync($"Configuration file already exists: {configurationPath}".ToCharArray(), cancellationToken).ConfigureAwait(false);
            await Console.Error.WriteLineAsync("Use --force to overwrite it.".ToCharArray(), cancellationToken).ConfigureAwait(false);
            return 1;
        }

        var contents = JsonSerializer.Serialize(value: TypewriterConfiguration.Default, options: CreateJsonOptions());

        // The init command intentionally writes into the caller-selected workspace.
#pragma warning disable SCS0018
        await File.WriteAllTextAsync(
                path: configurationPath,
                contents: contents + Environment.NewLine,
                cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
#pragma warning restore SCS0018

        Console.WriteLine(value: $"{(existed ? "updated" : "created")}: {configurationPath}");
        return 0;
    }

    private static async Task<int> GenerateAsync(
        CliOptions options,
        CancellationToken cancellationToken)
    {
        var result = await GenerateOnceAsync(options: options, cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        WriteResult(result: result, output: options.Output);
        return GetExitCode(result: result);
    }

    private static async Task<int> WatchAsync(
        CliOptions options,
        CancellationToken cancellationToken)
    {
        try
        {
            var configuration = await CreateConfigurationAsync(options: options, cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
            using var watcher = FileSystemGenerationWatcher.Create(options: options, configuration: configuration);
            await RunWatchedGenerationAsync(options: options, watcher: watcher, cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);

            while (!cancellationToken.IsCancellationRequested)
            {
                await watcher.WaitForChangeAsync(cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
                await watcher.WaitForQuietPeriodAsync(quietPeriod: WatchDebounceDelay, cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
                await RunWatchedGenerationAsync(options: options, watcher: watcher, cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return 0;
        }

        return 0;
    }

    private static async Task RunWatchedGenerationAsync(
        CliOptions options,
        FileSystemGenerationWatcher watcher,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            using var changeCancellation = CancellationTokenSource.CreateLinkedTokenSource(token: cancellationToken);

            var changeTask = watcher.WaitForChangeAsync(cancellationToken: changeCancellation.Token);
            var changeObserved = false;
            try
            {
                await GenerateAndWriteAsync(options: options, cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
            }
            finally
            {
                await changeCancellation.CancelAsync().ConfigureAwait(continueOnCapturedContext: false);
                try
                {
                    await changeTask.ConfigureAwait(continueOnCapturedContext: false);
                    changeObserved = true;
                }
                catch (OperationCanceledException) when (changeCancellation.IsCancellationRequested)
                {
                    changeObserved = false;
                }
            }

            if (!changeObserved)
            {
                return;
            }

            await watcher.WaitForQuietPeriodAsync(quietPeriod: WatchDebounceDelay, cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        }
    }

    private static async Task GenerateAndWriteAsync(
        CliOptions options,
        CancellationToken cancellationToken)
    {
        var result = await GenerateOnceAsync(options: options, cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        WriteResult(result: result, output: options.Output);
    }

    private static async Task<GenerationResult> GenerateOnceAsync(
        CliOptions options,
        CancellationToken cancellationToken)
    {
        var configuration = await CreateConfigurationAsync(options: options, cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        var request = new GenerationRequest(
            WorkspacePath: options.WorkspacePath,
            ProjectPath: options.ProjectPath,
            TemplatePath: options.TemplatePath,
            Mode: options.Command == CliCommand.Validate ? GenerationMode.Validate : GenerationMode.Generate,
            Configuration: configuration,
            AllProjects: options.AllProjects);

        var generator = new TypewriterGenerator(
            templateDiscovery: new FileSystemTemplateDiscovery(),
            metadataProvider: new CSharpProjectMetadataProvider(),
            fileWriter: new FileSystemGeneratedFileWriter());

        return await generator.GenerateAsync(request: request, cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
    }

    private static async Task<int> ListTemplatesAsync(
        CliOptions options,
        CancellationToken cancellationToken)
    {
        var workspacePath = options.WorkspacePath
            ?? options.ProjectPath
            ?? Environment.CurrentDirectory;
        var request = new GenerationRequest(
            WorkspacePath: workspacePath,
            ProjectPath: options.ProjectPath,
            TemplatePath: options.TemplatePath,
            Mode: GenerationMode.Validate,
            Configuration: await CreateConfigurationAsync(options: options, cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false));

        var templates = await new FileSystemTemplateDiscovery()
            .FindTemplatesAsync(
                workspace: new WorkspaceContext(RootPath: Path.GetFullPath(path: workspacePath)),
                request: request,
                cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        foreach (var template in templates)
        {
            Console.WriteLine(value: template.Path);
        }

        return 0;
    }

    private static async Task<TypewriterConfiguration> CreateConfigurationAsync(
        CliOptions options,
        CancellationToken cancellationToken)
    {
        var configuration = await TypewriterConfigurationLoader.LoadAsync(
            workspacePath: options.WorkspacePath,
            projectPath: options.ProjectPath,
            cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);

        return configuration with
        {
            DefaultTargetFramework = options.Framework ?? configuration.DefaultTargetFramework,
            Output = configuration.Output with
            {
                DryRun = configuration.Output.DryRun || options.DryRun || options.Command == CliCommand.Validate,
            },
            Diagnostics = configuration.Diagnostics with
            {
                FailOnWarning = options.FailOnWarning || configuration.Diagnostics.FailOnWarning,
            },
        };
    }

    private static string ResolveConfigurationDirectory(string? workspacePath)
    {
        if (string.IsNullOrWhiteSpace(value: workspacePath))
        {
            return Environment.CurrentDirectory;
        }

        var fullPath = Path.GetFullPath(path: workspacePath);
        if (File.Exists(path: fullPath))
        {
            return Path.GetDirectoryName(path: fullPath) ?? Environment.CurrentDirectory;
        }

        if (Directory.Exists(path: fullPath) || string.IsNullOrEmpty(value: Path.GetExtension(path: fullPath)))
        {
            return fullPath;
        }

        return Path.GetDirectoryName(path: fullPath) ?? Environment.CurrentDirectory;
    }

    private static int GetExitCode(GenerationResult result)
    {
        if (result.Success)
        {
            return 0;
        }

        if (result.Diagnostics.Any(predicate: diagnostic => string.Equals(diagnostic.Code, "TW0003", StringComparison.OrdinalIgnoreCase)))
        {
            return 3;
        }

        if (result.Diagnostics.Any(predicate: diagnostic => string.Equals(diagnostic.Code, "TW0002", StringComparison.OrdinalIgnoreCase)))
        {
            return 4;
        }

        return 1;
    }

    private static void WriteResult(
        GenerationResult result,
        string output)
    {
        if (output.Equals(value: "json", comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine(
                value: JsonSerializer.Serialize(
                    value: new
                    {
                        result.Success,
                        DurationMs = (long)result.Duration.TotalMilliseconds,
                        GeneratedFiles = result.GeneratedFiles.Select(
                            selector: file => new
                            {
                                file.Path,
                                file.Changed,
                            }),
                        Diagnostics = result.Diagnostics,
                    },
                    options: CreateJsonOptions()));
            return;
        }

        foreach (var diagnostic in result.Diagnostics)
        {
            Console.Error.WriteLine(value: FormatDiagnostic(diagnostic: diagnostic));
        }

        foreach (var file in result.GeneratedFiles)
        {
            var status = file.Changed ? "updated" : "unchanged";
            Console.WriteLine(value: $"{status}: {file.Path}");
        }
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        };
        options.Converters.Add(item: new JsonStringEnumConverter(namingPolicy: JsonNamingPolicy.CamelCase));
        return options;
    }

    private static string FormatDiagnostic(GenerationDiagnostic diagnostic)
    {
        var severity = diagnostic.Severity.ToString().ToLowerInvariant();
        var code = string.IsNullOrWhiteSpace(value: diagnostic.Code) ? string.Empty : " " + diagnostic.Code;
        var location = FormatDiagnosticLocation(diagnostic: diagnostic);
        var message = $"{severity}{code}: {diagnostic.Message}";
        return string.IsNullOrEmpty(value: location)
            ? message
            : $"{location}: {message}";
    }

    private static string FormatDiagnosticLocation(GenerationDiagnostic diagnostic)
    {
        if (string.IsNullOrWhiteSpace(value: diagnostic.File))
        {
            return string.Empty;
        }

        if (diagnostic.Line is null)
        {
            return diagnostic.File;
        }

        return diagnostic.Column is null
            ? string.Create(
                provider: CultureInfo.InvariantCulture,
                handler: $"{diagnostic.File}({diagnostic.Line.Value})")
            : string.Create(
                provider: CultureInfo.InvariantCulture,
                handler: $"{diagnostic.File}({diagnostic.Line.Value},{diagnostic.Column.Value})");
    }
}
