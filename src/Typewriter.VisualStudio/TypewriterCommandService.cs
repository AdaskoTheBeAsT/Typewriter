using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;

namespace Typewriter.VisualStudio;

internal sealed class TypewriterCommandService
{
    private readonly TypewriterPackage _package;
    private readonly TypewriterOutputPane _outputPane;
    private readonly TypewriterDiagnosticReporter _diagnosticReporter;

    public TypewriterCommandService(
        TypewriterPackage package,
        TypewriterOutputPane outputPane,
        TypewriterDiagnosticReporter diagnosticReporter)
    {
        _package = package;
        _outputPane = outputPane;
        _diagnosticReporter = diagnosticReporter;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await _package.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken: cancellationToken);
        if (await _package.GetVisualStudioServiceAsync(serviceType: typeof(IMenuCommandService), cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: true) is not OleMenuCommandService commandService)
        {
            return;
        }

        AddCommand(
            commandService: commandService,
            commandId: TypewriterVisualStudioConstants.GenerateCurrentTemplateCommandId,
            action: () => GenerateCurrentTemplateAsync(templatePathOverride: null, cancellationToken: CancellationToken.None));
        AddCommand(
            commandService: commandService,
            commandId: TypewriterVisualStudioConstants.GenerateAllTemplatesCommandId,
            action: () => GenerateAllTemplatesAsync(cancellationToken: CancellationToken.None));
        AddCommand(
            commandService: commandService,
            commandId: TypewriterVisualStudioConstants.ValidateCurrentTemplateCommandId,
            action: () => ValidateCurrentTemplateAsync(cancellationToken: CancellationToken.None));
        AddContextCommand(
            commandService: commandService,
            commandId: TypewriterVisualStudioConstants.RenderTemplateCommandId,
            action: () => GenerateCurrentTemplateAsync(templatePathOverride: null, cancellationToken: CancellationToken.None),
            isVisible: HasTemplateContext);
        AddContextCommand(
            commandService: commandService,
            commandId: TypewriterVisualStudioConstants.RenderAllTemplatesCommandId,
            action: () => GenerateAllTemplatesAsync(cancellationToken: CancellationToken.None),
            isVisible: HasRenderAllContext);
        AddContextCommand(
            commandService: commandService,
            commandId: TypewriterVisualStudioConstants.RenderSolutionAllTemplatesCommandId,
            action: () => GenerateSolutionAllTemplatesAsync(cancellationToken: CancellationToken.None),
            isVisible: HasSolutionContext);
        AddContextCommand(
            commandService: commandService,
            commandId: TypewriterVisualStudioConstants.ValidateTemplateCommandId,
            action: () => ValidateCurrentTemplateAsync(cancellationToken: CancellationToken.None),
            isVisible: HasTemplateContext);
    }

    public Task GenerateSavedTemplateAsync(
        string templatePath,
        CancellationToken cancellationToken) =>
        RunWithReportingAsync(
            message: "Typewriter generate-on-save failed.",
            action: () => GenerateCurrentTemplateAsync(templatePathOverride: templatePath, cancellationToken: cancellationToken),
            cancellationToken: cancellationToken);

    public Task GenerateSavedInputAsync(
        string inputPath,
        CancellationToken cancellationToken) =>
        RunWithReportingAsync(
            message: "Typewriter generate-on-save failed.",
            action: () => GenerateAllTemplatesAsync(inputPathOverride: inputPath, cancellationToken: cancellationToken),
            cancellationToken: cancellationToken);

    private void AddCommand(
        OleMenuCommandService commandService,
        int commandId,
        Func<Task> action)
    {
        commandService.AddCommand(
            command: new OleMenuCommand(
                invokeHandler: (_, _) => RunAsync(action: action),
                id: CreateCommandId(commandId: commandId)));
    }

    private void AddContextCommand(
        OleMenuCommandService commandService,
        int commandId,
        Func<Task> action,
        Func<bool> isVisible)
    {
        var command = new OleMenuCommand(
            invokeHandler: (_, _) => RunAsync(action: action),
            id: CreateCommandId(commandId: commandId));
        command.BeforeQueryStatus += (_, _) =>
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var visible = isVisible();
            command.Visible = visible;
            command.Enabled = visible;
        };
        commandService.AddCommand(command: command);
    }

    private static CommandID CreateCommandId(int commandId) =>
        new(
            menuGroup: TypewriterVisualStudioConstants.CommandSetGuid,
            commandID: commandId);

    private void RunAsync(Func<Task> action)
    {
        _ = _package.JoinableTaskFactory.RunAsync(
            asyncMethod: async () =>
            {
                try
                {
                    await action().ConfigureAwait(continueOnCapturedContext: false);
                }
                catch (Exception exception)
                {
                    await ReportExceptionAsync(message: "Typewriter command failed.", exception: exception, cancellationToken: CancellationToken.None)
                        .ConfigureAwait(continueOnCapturedContext: false);
                }
            });
    }

    private async Task RunWithReportingAsync(
        string message,
        Func<Task> action,
        CancellationToken cancellationToken)
    {
        try
        {
            await action().ConfigureAwait(continueOnCapturedContext: false);
        }
        catch (Exception exception)
        {
            await ReportExceptionAsync(message: message, exception: exception, cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        }
    }

    private async Task ReportExceptionAsync(
        string message,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ActivityLog.TryLogError(source: "Typewriter", message: exception.ToString());
        await _package.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken: cancellationToken);
        _outputPane.WriteLine(message: message);
        if (exception is Win32Exception win32Exception)
        {
            _outputPane.WriteLine(message: FormatProcessStartFailure(exception: win32Exception));
        }
        else
        {
            _outputPane.WriteLine(message: exception.Message);
        }

        _outputPane.Show();
    }

    private async Task GenerateCurrentTemplateAsync(
        string? templatePathOverride,
        CancellationToken cancellationToken)
    {
        var context = await CreateGenerationContextAsync(
                templatePathOverride: templatePathOverride,
                inputPathOverride: null,
                includeTemplate: true,
                solutionWide: false,
                cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
        if (context is null)
        {
            return;
        }

        await ExecuteCliAsync(command: "generate", context: context, allTemplates: false, cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
    }

    private async Task ValidateCurrentTemplateAsync(CancellationToken cancellationToken)
    {
        var context = await CreateGenerationContextAsync(
                templatePathOverride: null,
                inputPathOverride: null,
                includeTemplate: true,
                solutionWide: false,
                cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
        if (context is null)
        {
            return;
        }

        await ExecuteCliAsync(command: "validate", context: context, allTemplates: false, cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
    }

    private Task GenerateAllTemplatesAsync(CancellationToken cancellationToken) =>
        GenerateAllTemplatesAsync(inputPathOverride: null, cancellationToken: cancellationToken);

    private async Task GenerateAllTemplatesAsync(
        string? inputPathOverride,
        CancellationToken cancellationToken)
    {
        var context = await CreateGenerationContextAsync(
                templatePathOverride: null,
                inputPathOverride: inputPathOverride,
                includeTemplate: false,
                solutionWide: false,
                cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
        if (context is null)
        {
            return;
        }

        await ExecuteCliAsync(command: "generate", context: context, allTemplates: true, cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
    }

    private async Task GenerateSolutionAllTemplatesAsync(CancellationToken cancellationToken)
    {
        var context = await CreateGenerationContextAsync(
                templatePathOverride: null,
                inputPathOverride: null,
                includeTemplate: false,
                solutionWide: true,
                cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
        if (context is null)
        {
            return;
        }

        await ExecuteCliAsync(command: "generate", context: context, allTemplates: true, cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
    }

    private async Task<GenerationContext?> CreateGenerationContextAsync(
        string? templatePathOverride,
        string? inputPathOverride,
        bool includeTemplate,
        bool solutionWide,
        CancellationToken cancellationToken)
    {
        await _package.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken: cancellationToken);

        var dte = await _package.GetVisualStudioServiceAsync(serviceType: typeof(DTE), cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: true) as DTE2;
        var options = _package.GetTypewriterOptions();
        var templatePath = includeTemplate
            ? ResolveTemplatePath(dte: dte, templatePathOverride: templatePathOverride)
            : null;
        var inputPath = ResolveInputPath(inputPath: inputPathOverride);
        if (includeTemplate && string.IsNullOrWhiteSpace(value: templatePath))
        {
            _outputPane.WriteLine(message: "No active .tst template was found.");
            return null;
        }

        var selectedProjectPath = IsSolutionExplorerActive(dte: dte) ? GetSelectedProjectPath(dte: dte) : null;
        var inputProjectPath = FindProjectPathForInput(dte: dte, inputPath: inputPath);
        var projectPath = solutionWide
            ? null
            : ResolveConfiguredPath(value: options.ProjectPath)
              ?? inputProjectPath
              ?? (templatePath is not null ? FindProjectPathForTemplate(dte: dte, templatePath: templatePath) : selectedProjectPath)
              ?? selectedProjectPath
              ?? GetActiveProjectPath(dte: dte);
        var workspacePath = ResolveConfiguredPath(value: options.WorkspacePath)
            ?? GetSolutionPath(dte: dte)
            ?? projectPath
            ?? GetWorkspaceFallback(templatePath: templatePath, inputPath: inputPath);
        if (string.IsNullOrWhiteSpace(value: workspacePath))
        {
            _outputPane.WriteLine(message: "No workspace, solution, project, or template directory was found.");
            return null;
        }

        var resolvedWorkspacePath = workspacePath ?? string.Empty;
        return new GenerationContext(
            workspacePath: resolvedWorkspacePath,
            projectPath: projectPath,
            templatePath: templatePath,
            framework: options.Framework.Trim(),
            allProjects: solutionWide || options.AllProjects,
            workingDirectory: GetWorkingDirectory(workspacePath: resolvedWorkspacePath),
            cliInvocation: ResolveCliInvocation(options: options, workspacePath: resolvedWorkspacePath));
    }

    private async Task ExecuteCliAsync(
        string command,
        GenerationContext context,
        bool allTemplates,
        CancellationToken cancellationToken)
    {
        var args = BuildTypewriterArguments(command: command, context: context, allTemplates: allTemplates);
        await _package.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken: cancellationToken);
        _outputPane.WriteLine(message: FormatCommand(command: context.CliInvocation.Command, arguments: context.CliInvocation.Arguments.Concat(second: args)));
        _diagnosticReporter.Clear();

        var processResult = await RunProcessAsync(invocation: context.CliInvocation, typewriterArguments: args, workingDirectory: context.WorkingDirectory, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
        var payload = ParsePayload(standardOutput: processResult.StandardOutput);

        await _package.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken: CancellationToken.None);
        WriteProcessOutput(processResult: processResult);
        if (payload is not null)
        {
            _diagnosticReporter.Publish(diagnostics: payload.Diagnostics, workingDirectory: context.WorkingDirectory);
            WriteResultSummary(payload: payload);
        }

        if (processResult.ExitCode != 0 || payload?.Success == false)
        {
            _outputPane.Show();
        }
    }

    private static IReadOnlyList<string> BuildTypewriterArguments(
        string command,
        GenerationContext context,
        bool allTemplates)
    {
        var args = new List<string>
        {
            command,
            "--workspace",
            context.WorkspacePath,
            "--output",
            "json",
        };

        if (!string.IsNullOrWhiteSpace(value: context.ProjectPath))
        {
            args.Add(item: "--project");
            args.Add(item: context.ProjectPath ?? string.Empty);
        }

        if (!allTemplates && !string.IsNullOrWhiteSpace(value: context.TemplatePath))
        {
            args.Add(item: "--template");
            args.Add(item: context.TemplatePath ?? string.Empty);
        }

        if (!string.IsNullOrWhiteSpace(value: context.Framework))
        {
            args.Add(item: "--framework");
            args.Add(item: context.Framework);
        }

        if (allTemplates && context.AllProjects)
        {
            args.Add(item: "--all-projects");
        }

        return args;
    }

    private static async Task<CliProcessResult> RunProcessAsync(
        CliInvocation invocation,
        IReadOnlyList<string> typewriterArguments,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        using var process = new System.Diagnostics.Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = invocation.Command,
                Arguments = string.Join(
                    separator: " ",
                    values: invocation.Arguments.Concat(second: typewriterArguments).Select(selector: QuoteArgument)),
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            },
        };

        process.Start();
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        await Task.Run(action: process.WaitForExit, cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        return new CliProcessResult(
            exitCode: process.ExitCode,
            standardOutput: await outputTask.ConfigureAwait(continueOnCapturedContext: false),
            standardError: await errorTask.ConfigureAwait(continueOnCapturedContext: false));
    }

    private static CliResult? ParsePayload(string standardOutput)
    {
        var start = standardOutput.IndexOf(value: '{');
        var end = standardOutput.LastIndexOf(value: '}');
        if (start < 0 || end < start)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<CliResult>(json: standardOutput.Substring(startIndex: start, length: end - start + 1));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private void WriteProcessOutput(CliProcessResult processResult)
    {
        if (!string.IsNullOrWhiteSpace(value: processResult.StandardOutput))
        {
            _outputPane.WriteLine(message: processResult.StandardOutput.TrimEnd());
        }

        if (!string.IsNullOrWhiteSpace(value: processResult.StandardError))
        {
            _outputPane.WriteLine(message: processResult.StandardError.TrimEnd());
        }
    }

    private void WriteResultSummary(CliResult payload)
    {
        foreach (var file in payload.GeneratedFiles)
        {
            _outputPane.WriteLine(message: string.Format(
                provider: CultureInfo.InvariantCulture,
                format: "{0}: {1}",
                arg0: file.Changed ? "updated" : "unchanged",
                arg1: file.Path));
        }

        if (payload.Diagnostics.Count > 0)
        {
            _outputPane.WriteLine(message: string.Format(
                provider: CultureInfo.InvariantCulture,
                format: "{0} diagnostic(s).",
                arg0: payload.Diagnostics.Count));
        }
    }

    private static string? ResolveTemplatePath(
        DTE2? dte,
        string? templatePathOverride)
    {
        var candidate = templatePathOverride;
        if (string.IsNullOrWhiteSpace(value: candidate))
        {
            var selectedTemplatePath = ResolveSelectedTemplatePath(dte: dte);
            var activeTemplatePath = dte?.ActiveDocument?.FullName;
            candidate = IsSolutionExplorerActive(dte: dte)
                ? selectedTemplatePath ?? activeTemplatePath
                : activeTemplatePath ?? selectedTemplatePath;
        }

        return IsTemplatePath(path: candidate) ? candidate : null;
    }

    private static string? ResolveInputPath(string? inputPath) =>
        string.IsNullOrWhiteSpace(value: inputPath)
            ? null
            : Path.GetFullPath(path: inputPath);

    private static string? GetSolutionPath(DTE2? dte)
    {
        var solutionPath = dte?.Solution?.FullName;
        return string.IsNullOrWhiteSpace(value: solutionPath) ? null : solutionPath;
    }

    private static string? GetActiveProjectPath(DTE2? dte)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        return dte?.ActiveDocument?.ProjectItem?.ContainingProject?.FullName;
    }

    private static string? ResolveSelectedTemplatePath(DTE2? dte)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        return GetSelectedItems(dte: dte)
            .Select(selector: selectedItem => GetProjectItemPath(projectItem: GetSelectedProjectItem(selectedItem: selectedItem)))
            .FirstOrDefault(predicate: IsTemplatePath);
    }

    private static string? GetSelectedProjectPath(DTE2? dte)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        foreach (var selectedItem in GetSelectedItems(dte: dte))
        {
            var projectItem = GetSelectedProjectItem(selectedItem: selectedItem);
            var projectPath = GetProjectPath(project: GetSelectedProject(selectedItem: selectedItem))
                ?? GetProjectPath(project: projectItem?.ContainingProject);
            if (!string.IsNullOrWhiteSpace(value: projectPath))
            {
                return projectPath;
            }
        }

        return null;
    }

    private static string? GetSelectedProjectNodePath(DTE2? dte)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        return GetSelectedItems(dte: dte)
            .Select(selector: selectedItem => GetProjectPath(project: GetSelectedProject(selectedItem: selectedItem)))
            .FirstOrDefault(predicate: path => !string.IsNullOrWhiteSpace(value: path));
    }

    private static IReadOnlyList<SelectedItem> GetSelectedItems(DTE2? dte)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var selectedItems = dte?.SelectedItems;
        if (selectedItems is null)
        {
            return [];
        }

        int count;
        try
        {
            count = selectedItems.Count;
        }
        catch (COMException)
        {
            return [];
        }

        var items = new List<SelectedItem>();
        for (var index = 1; index <= count; index++)
        {
            try
            {
                if (selectedItems.Item(index) is SelectedItem selectedItem)
                {
                    items.Add(item: selectedItem);
                }
            }
            catch (COMException)
            {
            }
            catch (ArgumentException)
            {
            }
        }

        return items;
    }

    private static ProjectItem? GetSelectedProjectItem(SelectedItem selectedItem)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        try
        {
            return selectedItem.ProjectItem;
        }
        catch (COMException)
        {
            return null;
        }
        catch (NotImplementedException)
        {
            return null;
        }
    }

    private static Project? GetSelectedProject(SelectedItem selectedItem)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        try
        {
            return selectedItem.Project;
        }
        catch (COMException)
        {
            return null;
        }
        catch (NotImplementedException)
        {
            return null;
        }
    }

    private static string? GetProjectItemPath(ProjectItem? projectItem)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (projectItem is null)
        {
            return null;
        }

        try
        {
            for (var index = 1; index <= projectItem.FileCount; index++)
            {
                var path = projectItem.FileNames[(short)index];
                if (!string.IsNullOrWhiteSpace(value: path))
                {
                    return path;
                }
            }
        }
        catch (COMException)
        {
        }
        catch (ArgumentException)
        {
        }
        catch (NotImplementedException)
        {
        }

        return null;
    }

    private static string? GetProjectPath(Project? project)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (project is null)
        {
            return null;
        }

        try
        {
            return string.IsNullOrWhiteSpace(value: project.FullName) ? null : project.FullName;
        }
        catch (COMException)
        {
            return null;
        }
        catch (NotImplementedException)
        {
            return null;
        }
    }

    private static bool IsTemplatePath(string? path) =>
        !string.IsNullOrWhiteSpace(value: path)
        && Path.GetExtension(path: path).Equals(value: ".tst", comparisonType: StringComparison.OrdinalIgnoreCase);

    private static bool HasTemplateContext()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var dte = GetGlobalDte();
        return IsSolutionExplorerActive(dte: dte)
            ? IsTemplatePath(path: ResolveSelectedTemplatePath(dte: dte))
            : IsTemplatePath(path: dte?.ActiveDocument?.FullName);
    }

    private static bool HasRenderAllContext()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var dte = GetGlobalDte();
        return IsSolutionExplorerActive(dte: dte)
            ? GetSelectedProjectNodePath(dte: dte) is not null
              || IsTemplatePath(path: ResolveSelectedTemplatePath(dte: dte))
            : IsTemplatePath(path: dte?.ActiveDocument?.FullName);
    }

    private static bool HasSolutionContext()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var dte = GetGlobalDte();
        return IsSolutionExplorerActive(dte: dte)
            && !string.IsNullOrWhiteSpace(value: GetSolutionPath(dte: dte));
    }

    private static DTE2? GetGlobalDte()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        return Package.GetGlobalService(typeof(DTE)) as DTE2;
    }

    private static bool IsSolutionExplorerActive(DTE2? dte)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        try
        {
            return string.Equals(
                a: dte?.ActiveWindow?.ObjectKind,
                b: Constants.vsWindowKindSolutionExplorer,
                comparisonType: StringComparison.OrdinalIgnoreCase);
        }
        catch (COMException)
        {
            return false;
        }
        catch (NotImplementedException)
        {
            return false;
        }
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

    private static string? FindProjectPathForInput(
        DTE2? dte,
        string? inputPath)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (string.IsNullOrWhiteSpace(value: inputPath))
        {
            return null;
        }

        var fullInputPath = Path.GetFullPath(path: inputPath);
        if (fullInputPath.EndsWith(value: ".csproj", comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            return fullInputPath;
        }

        if (dte?.Solution?.Projects is null)
        {
            return null;
        }

        return EnumerateProjects(projects: dte.Solution.Projects)
            .Select(selector: GetProjectPath)
            .Where(predicate: path => !string.IsNullOrWhiteSpace(value: path))
            .Select(selector: path => path!)
            .FirstOrDefault(predicate: projectPath =>
                IsPathBelowDirectory(path: fullInputPath, directory: Path.GetDirectoryName(path: projectPath) ?? string.Empty));
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

    private static string? GetWorkspaceFallback(
        string? templatePath,
        string? inputPath)
    {
        var fallbackPath = string.IsNullOrWhiteSpace(value: templatePath)
            ? inputPath
            : templatePath;
        return string.IsNullOrWhiteSpace(value: fallbackPath)
            ? null
            : Path.GetDirectoryName(path: fallbackPath);
    }

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

    private static CliInvocation ResolveCliInvocation(
        TypewriterOptions options,
        string workspacePath)
    {
        if (!string.IsNullOrWhiteSpace(value: options.CliPath))
        {
            return new CliInvocation(
                command: options.CliPath.Trim(),
                arguments: SplitArguments(arguments: options.CliArguments));
        }

        var localCliProject = FindLocalCliProject(workspacePath: workspacePath);
        if (localCliProject is not null)
        {
            return new CliInvocation(
                command: "dotnet",
                arguments: ["run", "--project", localCliProject, "--"]);
        }

        return FindPackagedCliInvocation()
            ?? new CliInvocation(command: "typewriter", arguments: []);
    }

    private static string? FindLocalCliProject(string workspacePath)
    {
        var directory = Directory.Exists(path: workspacePath)
            ? new DirectoryInfo(path: workspacePath)
            : new FileInfo(fileName: workspacePath).Directory;
        while (directory is not null)
        {
            var candidate = Path.Combine(path1: directory.FullName, path2: "src", path3: "Typewriter.Cli", path4: "Typewriter.Cli.csproj");
            if (File.Exists(path: candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static CliInvocation? FindPackagedCliInvocation()
    {
        var packageDirectory = Path.GetDirectoryName(path: typeof(TypewriterCommandService).Assembly.Location);
        if (string.IsNullOrWhiteSpace(value: packageDirectory))
        {
            return null;
        }

        var cliDirectory = Path.Combine(path1: packageDirectory, path2: "tools", path3: "typewriter-cli");
        var cliExecutable = Path.Combine(path1: cliDirectory, path2: "Typewriter.Cli.exe");
        if (File.Exists(path: cliExecutable))
        {
            return new CliInvocation(command: cliExecutable, arguments: []);
        }

        var cliAssembly = Path.Combine(path1: cliDirectory, path2: "Typewriter.Cli.dll");
        return File.Exists(path: cliAssembly)
            ? new CliInvocation(command: "dotnet", arguments: [cliAssembly])
            : null;
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
        "> " + string.Join(separator: " ", values: new[] { command }.Concat(second: arguments).Select(selector: QuoteArgument));

    private static string FormatProcessStartFailure(Win32Exception exception) =>
        "Unable to start the Typewriter CLI. "
        + "Reinstall the VSIX so it includes the packaged CLI, install the typewriter dotnet tool, "
        + "set Tools > Options > Typewriter > CLI path, "
        + "or open a workspace that contains src/Typewriter.Cli/Typewriter.Cli.csproj. "
        + exception.Message;
}
