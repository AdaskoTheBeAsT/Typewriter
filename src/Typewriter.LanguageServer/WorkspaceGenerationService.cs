using Typewriter.Abstractions;
using Typewriter.Engine;
using Typewriter.Roslyn;

namespace Typewriter.LanguageServer;

internal sealed class WorkspaceGenerationService : IWorkspaceGenerationService, IDisposable
{
    private readonly TypewriterGenerator _generator = new(
        templateDiscovery: new FileSystemTemplateDiscovery(),
        metadataProvider: new CSharpProjectMetadataProvider(),
        fileWriter: new FileSystemGeneratedFileWriter());

    private readonly SemaphoreSlim _generationLock = new(initialCount: 1, maxCount: 1);

    public void Dispose()
    {
        _generationLock.Dispose();
    }

    public async Task<WorkspaceGenerationResult> GenerateAsync(
        WorkspaceGenerationRequest request,
        LanguageServerSettings settings,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(argument: request);
        ArgumentNullException.ThrowIfNull(argument: settings);

        await _generationLock.WaitAsync(cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        try
        {
            var workspacePath = ResolveWorkspacePath(request: request, settings: settings);
            var projectPath = ResolveOptionalPath(value: request.ProjectPath, basePath: GetPathBase(path: workspacePath))
                ?? settings.ResolveProjectPath(workspacePath: workspacePath);
            var configuration = await TypewriterConfigurationLoader.LoadAsync(
                workspacePath: workspacePath,
                projectPath: projectPath,
                cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
            var mode = request.Command?.Equals(value: "validate", comparisonType: StringComparison.OrdinalIgnoreCase) == true
                ? GenerationMode.Validate
                : GenerationMode.Generate;
            configuration = configuration with
            {
                DefaultTargetFramework = request.Framework ?? settings.Framework ?? configuration.DefaultTargetFramework,
                Output = configuration.Output with
                {
                    DryRun = mode == GenerationMode.Validate || configuration.Output.DryRun,
                },
            };

            var result = await _generator.GenerateAsync(
                request: new GenerationRequest(
                    WorkspacePath: workspacePath,
                    ProjectPath: projectPath,
                    TemplatePath: ResolveOptionalPath(value: request.TemplatePath, basePath: GetPathBase(path: workspacePath)),
                    Mode: mode,
                    Configuration: configuration,
                    AllProjects: request.AllProjects ?? settings.AllProjects,
                    TemplateSearchPath: ResolveOptionalPath(value: request.TemplateSearchPath, basePath: GetPathBase(path: workspacePath))),
                cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
            return new WorkspaceGenerationResult(
                Success: result.Success,
                DurationMs: (long)result.Duration.TotalMilliseconds,
                GeneratedFiles: result.GeneratedFiles
                    .Select(selector: file => new WorkspaceGeneratedFile(Path: file.Path, Changed: file.Changed))
                    .ToArray(),
                Diagnostics: result.Diagnostics
                    .Select(
                        selector: diagnostic => new WorkspaceGenerationDiagnostic(
                            File: diagnostic.File,
                            Line: diagnostic.Line,
                            Column: diagnostic.Column,
                            Severity: FormatSeverity(severity: diagnostic.Severity),
                            Message: diagnostic.Message,
                            Code: diagnostic.Code,
                            HelpLink: diagnostic.HelpLink))
                    .ToArray());
        }
        finally
        {
            _ = _generationLock.Release();
        }
    }

    private static string ResolveWorkspacePath(
        WorkspaceGenerationRequest request,
        LanguageServerSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(value: request.WorkspacePath))
        {
            var basePath = settings.RootPath ?? Environment.CurrentDirectory;
            return ResolveOptionalPath(value: request.WorkspacePath, basePath: basePath) ?? basePath;
        }

        return settings.ResolveWorkspacePath(
            documentPath: request.TemplatePath
                ?? settings.RootPath
                ?? Environment.CurrentDirectory);
    }

    private static string? ResolveOptionalPath(
        string? value,
        string basePath)
    {
        if (string.IsNullOrWhiteSpace(value: value))
        {
            return null;
        }

        return Path.IsPathRooted(path: value)
            ? Path.GetFullPath(path: value)
            : Path.GetFullPath(path: Path.Combine(path1: basePath, path2: value));
    }

    private static string GetPathBase(string path)
    {
        var fullPath = Path.GetFullPath(path: path);
        return File.Exists(path: fullPath)
            ? Path.GetDirectoryName(path: fullPath) ?? Environment.CurrentDirectory
            : fullPath;
    }

    private static string FormatSeverity(DiagnosticSeverity severity) =>
        severity switch
        {
            DiagnosticSeverity.Error => "error",
            DiagnosticSeverity.Warning => "warning",
            DiagnosticSeverity.Info => "information",
            _ => "hidden",
        };
}
