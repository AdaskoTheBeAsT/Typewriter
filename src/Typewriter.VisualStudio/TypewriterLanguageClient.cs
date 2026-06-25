using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Utilities;
using StreamJsonRpc;

namespace Typewriter.VisualStudio;

[Export(contractType: typeof(ILanguageClient))]
[ContentType(name: TypewriterEditorContentTypes.ContentTypeName)]
[Name(name: "Typewriter Language Server")]
internal sealed class TypewriterLanguageClient : ILanguageClient, ILanguageClientCustomMessage2
{
    private static TypewriterLanguageClient? _activeClient;
    private TypewriterLanguageServerContext? _context;
    private System.Diagnostics.Process? _process;
    private JsonRpc? _rpc;

    public event AsyncEventHandler<EventArgs>? StartAsync;

    public event AsyncEventHandler<EventArgs>? StopAsync;

    public string Name => "Typewriter Language Server";

    public IEnumerable<string> ConfigurationSections => [];

    public object? InitializationOptions => _context?.InitializationOptions;

    public IEnumerable<string> FilesToWatch => ["**/*.tst", "**/*.cs", "**/*.csproj"];

    public bool ShowNotificationOnInitializeFailed => true;

    public object? MiddleLayer => null;

    public object? CustomMessageTarget => null;

    public async Task OnLoadedAsync()
    {
        _context = await CreateContextAsync(cancellationToken: CancellationToken.None).ConfigureAwait(continueOnCapturedContext: false);
        if (_context is null || StartAsync is null)
        {
            return;
        }

        await StartAsync.InvokeAsync(sender: this, args: EventArgs.Empty).ConfigureAwait(continueOnCapturedContext: false);
    }

    public Task OnServerInitializedAsync()
    {
        ActivityLog.TryLogInformation(source: "Typewriter", message: "Typewriter language server initialized.");
        return Task.CompletedTask;
    }

    public Task AttachForCustomMessageAsync(JsonRpc rpc)
    {
        _rpc = rpc;
        _activeClient = this;
        rpc.Disconnected += (_, _) =>
        {
            if (ReferenceEquals(objA: _activeClient, objB: this))
            {
                _activeClient = null;
            }

            _rpc = null;
        };
        return Task.CompletedTask;
    }

    internal static async Task<CliResult?> TryGenerateAsync(
        TypewriterGenerationRequest request,
        CancellationToken cancellationToken)
    {
        var rpc = _activeClient?._rpc;
        if (rpc is null)
        {
            return null;
        }

        try
        {
            return await rpc.InvokeWithParameterObjectAsync<CliResult>(
                targetName: "typewriter/generate",
                argument: request,
                cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            ActivityLog.TryLogInformation(
                source: "Typewriter",
                message: "Persistent generation unavailable; falling back to the CLI: " + exception.Message);
            return null;
        }
    }

    public async Task<Connection?> ActivateAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var context = _context;
        if (context is null)
        {
            return null;
        }

        await StopServerAsync().ConfigureAwait(continueOnCapturedContext: false);

        var process = CreateLanguageServerProcess(context: context);
        try
        {
            process.Start();
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            process.Dispose();
            ActivityLog.TryLogError(source: "Typewriter", message: exception.ToString());
            throw new InvalidOperationException(
                message: "Unable to start the Typewriter language server. Reinstall the VSIX so it includes the packaged language server, set Tools > Options > Typewriter > Language server path, or install typewriter-lsp on PATH.",
                innerException: exception);
        }

        _process = process;
        _ = Task.Run(function: () => DrainErrorAsync(process: process), cancellationToken: CancellationToken.None);
        ActivityLog.TryLogInformation(
            source: "Typewriter",
            message: string.Format(
                provider: CultureInfo.InvariantCulture,
                format: "Started Typewriter language server: {0}",
                arg0: FormatCommand(command: context.Invocation.Command, arguments: context.Invocation.Arguments)));
        return new Connection(
            reader: process.StandardOutput.BaseStream,
            writer: process.StandardInput.BaseStream);
    }

    public async Task<InitializationFailureContext?> OnServerInitializeFailedAsync(
        ILanguageClientInitializationInfo initializationState)
    {
        await StopServerAsync().ConfigureAwait(continueOnCapturedContext: false);
        return new InitializationFailureContext
        {
            FailureMessage = "Typewriter language server failed to initialize. " + initializationState.StatusMessage,
        };
    }

    private static async Task<TypewriterLanguageServerContext?> CreateContextAsync(CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken: cancellationToken);
        var options = TypewriterPackage.Current?.GetTypewriterOptions() ?? new TypewriterOptions();
        if (!options.LanguageServerEnabled)
        {
            return null;
        }

        var dte = Package.GetGlobalService(serviceType: typeof(DTE)) as DTE2;
        var templatePath = ResolveActiveTemplatePath(dte: dte);
        var workspacePath = ResolveConfiguredPath(value: options.WorkspacePath)
            ?? GetSolutionPath(dte: dte)
            ?? GetWorkspaceFallback(templatePath: templatePath)
            ?? Environment.CurrentDirectory;
        var resolvedWorkspacePath = Path.GetFullPath(path: workspacePath);
        var projectPath = ResolveConfiguredPath(value: options.ProjectPath)
            ?? FindProjectPathForTemplate(dte: dte, templatePath: templatePath)
            ?? GetActiveProjectPath(dte: dte);
        var workingDirectory = GetWorkingDirectory(workspacePath: resolvedWorkspacePath);
        var framework = options.Framework.Trim();
        return new TypewriterLanguageServerContext(
            workspacePath: resolvedWorkspacePath,
            projectPath: projectPath,
            framework: framework,
            allProjects: options.AllProjects,
            workingDirectory: workingDirectory,
            invocation: ResolveLanguageServerInvocation(options: options, workspacePath: resolvedWorkspacePath));
    }

    private static System.Diagnostics.Process CreateLanguageServerProcess(TypewriterLanguageServerContext context) =>
        new()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = context.Invocation.Command,
                Arguments = string.Join(separator: " ", values: context.Invocation.Arguments.Select(selector: QuoteArgument)),
                WorkingDirectory = context.WorkingDirectory,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            },
            EnableRaisingEvents = true,
        };

    internal static async Task DrainErrorAsync(System.Diagnostics.Process process)
    {
        try
        {
            while (!process.HasExited)
            {
                var line = await process.StandardError.ReadLineAsync().ConfigureAwait(continueOnCapturedContext: false);
                if (line is null)
                {
                    return;
                }

                if (!string.IsNullOrWhiteSpace(value: line))
                {
                    ActivityLog.TryLogInformation(source: "Typewriter", message: "Typewriter language server: " + line);
                }
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or ObjectDisposedException or IOException)
        {
            ActivityLog.TryLogInformation(source: "Typewriter", message: exception.Message);
        }
    }

    internal static CliInvocation ResolveLanguageServerInvocation(
        TypewriterOptions options,
        string workspacePath)
    {
        if (!string.IsNullOrWhiteSpace(value: options.LanguageServerPath))
        {
            return new CliInvocation(
                command: options.LanguageServerPath.Trim(),
                arguments: SplitArguments(arguments: options.LanguageServerArguments));
        }

        var packageDirectory = Path.GetDirectoryName(path: typeof(TypewriterLanguageClient).Assembly.Location);
        var localServerProject = FindLocalLanguageServerProject(startPath: packageDirectory)
            ?? FindLocalLanguageServerProject(startPath: workspacePath);
        if (localServerProject is not null)
        {
            var localServerAssembly = FindBuiltLanguageServerAssembly(projectPath: localServerProject);
            if (localServerAssembly is not null)
            {
                return new CliInvocation(
                    command: "dotnet",
                    arguments: [localServerAssembly]);
            }

            return new CliInvocation(
                command: "dotnet",
                arguments: ["run", "--project", localServerProject, "--no-launch-profile", "--"]);
        }

        return FindPackagedLanguageServerInvocation()
            ?? new CliInvocation(command: "typewriter-lsp", arguments: []);
    }

    private static CliInvocation? FindPackagedLanguageServerInvocation()
    {
        var packageDirectory = Path.GetDirectoryName(path: typeof(TypewriterLanguageClient).Assembly.Location);
        if (string.IsNullOrWhiteSpace(value: packageDirectory))
        {
            return null;
        }

        var serverDirectory = Path.Combine(path1: packageDirectory, path2: "tools", path3: "typewriter-lsp");
        var serverExecutable = Path.Combine(path1: serverDirectory, path2: "Typewriter.LanguageServer.exe");
        if (File.Exists(path: serverExecutable))
        {
            return new CliInvocation(command: serverExecutable, arguments: []);
        }

        var serverAssembly = Path.Combine(path1: serverDirectory, path2: "Typewriter.LanguageServer.dll");
        return File.Exists(path: serverAssembly)
            ? new CliInvocation(command: "dotnet", arguments: [serverAssembly])
            : null;
    }

    private static string? FindLocalLanguageServerProject(string? startPath)
    {
        if (string.IsNullOrWhiteSpace(value: startPath))
        {
            return null;
        }

        var directory = Directory.Exists(path: startPath)
            ? new DirectoryInfo(path: startPath)
            : new FileInfo(fileName: startPath).Directory;
        while (directory is not null)
        {
            var candidate = Path.Combine(
                path1: directory.FullName,
                path2: "src",
                path3: "Typewriter.LanguageServer",
                path4: "Typewriter.LanguageServer.csproj");
            if (File.Exists(path: candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static string? FindBuiltLanguageServerAssembly(string projectPath)
    {
        var projectDirectory = Path.GetDirectoryName(path: projectPath);
        if (string.IsNullOrWhiteSpace(value: projectDirectory))
        {
            return null;
        }

        var outputRoot = Path.Combine(path1: projectDirectory, path2: "bin");
        if (!Directory.Exists(path: outputRoot))
        {
            return null;
        }

        return Directory.EnumerateFiles(path: outputRoot, searchPattern: "Typewriter.LanguageServer.dll", searchOption: SearchOption.AllDirectories)
            .Select(selector: path => new FileInfo(fileName: path))
            .Where(predicate: file => file.Exists)
            .OrderByDescending(keySelector: file => file.LastWriteTimeUtc)
            .Select(selector: file => file.FullName)
            .FirstOrDefault();
    }

    private static string? ResolveActiveTemplatePath(DTE2? dte)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var candidate = dte?.ActiveDocument?.FullName;
        return !string.IsNullOrWhiteSpace(value: candidate)
            && Path.GetExtension(path: candidate).Equals(value: ".tst", comparisonType: StringComparison.OrdinalIgnoreCase)
                ? candidate
                : null;
    }

    private static string? GetSolutionPath(DTE2? dte)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var solutionPath = dte?.Solution?.FullName;
        return string.IsNullOrWhiteSpace(value: solutionPath) ? null : solutionPath;
    }

    private static string? GetActiveProjectPath(DTE2? dte)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        return dte?.ActiveDocument?.ProjectItem?.ContainingProject?.FullName;
    }

    private static string? FindProjectPathForTemplate(
        DTE2? dte,
        string? templatePath)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (dte?.Solution?.Projects is null || string.IsNullOrWhiteSpace(value: templatePath))
        {
            return null;
        }

        return EnumerateProjects(projects: dte.Solution.Projects)
            .Select(selector: project => project.FullName)
            .Where(predicate: path => !string.IsNullOrWhiteSpace(value: path))
            .FirstOrDefault(predicate: projectPath =>
                IsPathBelowDirectory(path: templatePath ?? string.Empty, directory: Path.GetDirectoryName(path: projectPath) ?? string.Empty));
    }

    private static IEnumerable<Project> EnumerateProjects(Projects projects)
    {
        foreach (Project project in projects)
        {
            if (project.Kind == ProjectKinds.vsProjectKindSolutionFolder && project.ProjectItems is not null)
            {
                foreach (var nested in EnumerateProjectItems(projectItems: project.ProjectItems))
                {
                    yield return nested;
                }

                continue;
            }

            yield return project;
        }
    }

    private static IEnumerable<Project> EnumerateProjectItems(ProjectItems projectItems)
    {
        foreach (ProjectItem item in projectItems)
        {
            if (item.SubProject is not null)
            {
                yield return item.SubProject;
            }

            if (item.ProjectItems is not null)
            {
                foreach (var nested in EnumerateProjectItems(projectItems: item.ProjectItems))
                {
                    yield return nested;
                }
            }
        }
    }

    private static bool IsPathBelowDirectory(
        string path,
        string directory)
    {
        if (string.IsNullOrWhiteSpace(value: directory))
        {
            return false;
        }

        var fullPath = Path.GetFullPath(path: path);
        var fullDirectory = Path.GetFullPath(path: directory)
            .TrimEnd(trimChars: [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar])
            + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(value: fullDirectory, comparisonType: StringComparison.OrdinalIgnoreCase);
    }

    private static string? GetWorkspaceFallback(string? templatePath) =>
        string.IsNullOrWhiteSpace(value: templatePath)
            ? null
            : Path.GetDirectoryName(path: templatePath);

    private static string? ResolveConfiguredPath(string value) =>
        string.IsNullOrWhiteSpace(value: value)
            ? null
            : Path.GetFullPath(path: value);

    private static string GetWorkingDirectory(string workspacePath)
    {
        if (Directory.Exists(path: workspacePath))
        {
            return workspacePath;
        }

        return Path.GetDirectoryName(path: workspacePath) ?? Environment.CurrentDirectory;
    }

    private static IReadOnlyList<string> SplitArguments(string arguments)
    {
        if (string.IsNullOrWhiteSpace(value: arguments))
        {
            return [];
        }

        return arguments.Split(separator: [' '], options: StringSplitOptions.RemoveEmptyEntries);
    }

    private static string QuoteArgument(string argument)
    {
        if (argument.Length == 0)
        {
            return "\"\"";
        }

        return argument.Any(predicate: char.IsWhiteSpace) || argument.Contains(value: "\"")
            ? "\"" + argument.Replace(oldValue: "\\", newValue: "\\\\").Replace(oldValue: "\"", newValue: "\\\"") + "\""
            : argument;
    }

    private static string FormatCommand(
        string command,
        IEnumerable<string> arguments) =>
        string.Join(separator: " ", values: new[] { command }.Concat(second: arguments).Select(selector: QuoteArgument));

    private async Task StopServerAsync()
    {
        var stopAsync = StopAsync;
        if (_process is not null && stopAsync is not null)
        {
            await stopAsync.InvokeAsync(sender: this, args: EventArgs.Empty).ConfigureAwait(continueOnCapturedContext: false);
        }

        DisposeProcess();
    }

    private void DisposeProcess()
    {
        if (ReferenceEquals(objA: _activeClient, objB: this))
        {
            _activeClient = null;
        }

        _rpc = null;
        var process = _process;
        _process = null;
        if (process is null)
        {
            return;
        }

        try
        {
            if (!process.HasExited)
            {
                process.Kill();
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception)
        {
            ActivityLog.TryLogInformation(source: "Typewriter", message: exception.Message);
        }
        finally
        {
            process.Dispose();
        }
    }

    private sealed class TypewriterLanguageServerContext
    {
        public TypewriterLanguageServerContext(
            string workspacePath,
            string? projectPath,
            string framework,
            bool allProjects,
            string workingDirectory,
            CliInvocation invocation)
        {
            WorkingDirectory = workingDirectory;
            Invocation = invocation;
            InitializationOptions = new
            {
                workspacePath,
                projectPath,
                framework = string.IsNullOrWhiteSpace(value: framework) ? null : framework,
                allProjects,
            };
        }

        public string WorkingDirectory { get; }

        public CliInvocation Invocation { get; }

        public object InitializationOptions { get; }
    }
}
