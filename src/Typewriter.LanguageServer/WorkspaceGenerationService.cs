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

    public WorkspaceGenerationService()
    {
        MetadataCacheInvalidation.EnableTracking();
    }

    public void Dispose()
    {
        MetadataCacheInvalidation.Reset();
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
            var changedInputs = MapChangedInputs(changedInputs: request.ChangedInputs, basePath: GetPathBase(path: workspacePath));
            ApplyMetadataInvalidation(changedInputs: changedInputs);

            var result = await _generator.GenerateAsync(
                request: CreateGenerationRequest(
                    request: request,
                    settings: settings,
                    workspacePath: workspacePath,
                    projectPath: projectPath,
                    mode: mode,
                    configuration: configuration,
                    changedInputs: changedInputs),
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

    private static GenerationRequest CreateGenerationRequest(
        WorkspaceGenerationRequest request,
        LanguageServerSettings settings,
        string workspacePath,
        string? projectPath,
        GenerationMode mode,
        TypewriterConfiguration configuration,
        IReadOnlyCollection<ChangedInput>? changedInputs)
    {
        var basePath = GetPathBase(path: workspacePath);
        return new GenerationRequest(
            WorkspacePath: workspacePath,
            ProjectPath: projectPath,
            TemplatePath: ResolveOptionalPath(value: request.TemplatePath, basePath: basePath),
            Mode: mode,
            Configuration: configuration,
            AllProjects: request.AllProjects ?? settings.AllProjects,
            TemplateSearchPath: ResolveOptionalPath(value: request.TemplateSearchPath, basePath: basePath))
        {
            ChangedInputs = changedInputs,
        };
    }

    private static void ApplyMetadataInvalidation(IReadOnlyCollection<ChangedInput>? changedInputs)
    {
        if (changedInputs is null || changedInputs.Count == 0)
        {
            MetadataCacheInvalidation.MarkAllDirty();
            return;
        }

        foreach (var changedInput in changedInputs)
        {
            if (changedInput.Kind is ChangedInputKind.Deleted or ChangedInputKind.Renamed)
            {
                MetadataCacheInvalidation.MarkAllDirty();
                return;
            }

            MetadataCacheInvalidation.MarkDirty(fullPath: changedInput.FullPath);
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

    /// <summary>
    /// Maps the request's changed inputs to engine changed inputs. Returns <c>null</c>
    /// (unknown provenance, full generation) when no changed inputs were provided or any
    /// entry cannot be resolved to a path.
    /// </summary>
    /// <param name="changedInputs">The changed inputs from the generation request.</param>
    /// <param name="basePath">The base path used to resolve relative paths.</param>
    /// <returns>The mapped changed inputs, or <c>null</c>.</returns>
    private static IReadOnlyCollection<ChangedInput>? MapChangedInputs(
        IReadOnlyList<WorkspaceChangedInput>? changedInputs,
        string basePath)
    {
        if (changedInputs is null || changedInputs.Count == 0)
        {
            return null;
        }

        var mapped = new List<ChangedInput>(capacity: changedInputs.Count);
        foreach (var changedInput in changedInputs)
        {
            string? fullPath;
            try
            {
                fullPath = ResolveOptionalPath(value: changedInput.FullPath, basePath: basePath);
            }
            catch (ArgumentException)
            {
                return null;
            }
            catch (NotSupportedException)
            {
                return null;
            }
            catch (PathTooLongException)
            {
                return null;
            }

            if (fullPath is null)
            {
                return null;
            }

            mapped.Add(item: new ChangedInput(FullPath: fullPath, Kind: MapChangedInputKind(kind: changedInput.Kind)));
        }

        return mapped;
    }

    private static ChangedInputKind MapChangedInputKind(string? kind)
    {
        if (string.IsNullOrWhiteSpace(value: kind))
        {
            return ChangedInputKind.Modified;
        }

        return Enum.TryParse<ChangedInputKind>(value: kind, ignoreCase: true, result: out var parsed)
            ? parsed
            : ChangedInputKind.Modified;
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
