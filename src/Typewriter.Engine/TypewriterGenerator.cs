using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using Typewriter.Abstractions;

namespace Typewriter.Engine;

public sealed class TypewriterGenerator : ITypewriterGenerator
{
    private const int MaxTemplateDocumentCacheEntries = 256;
    private static readonly ConcurrentDictionary<TemplateDocumentCacheKey, TemplateDocumentCacheEntry> TemplateDocumentCache = new();
    private static readonly ConcurrentQueue<TemplateDocumentCacheKey> TemplateDocumentCacheOrder = new();
    private readonly IGeneratedFileWriter _fileWriter;
    private readonly IProjectMetadataProvider _metadataProvider;
    private readonly GeneratedFilePlanner _planner;
    private readonly ITemplateDiscovery _templateDiscovery;
    private readonly TemplateRenderer _templateRenderer;

    public TypewriterGenerator(
        ITemplateDiscovery templateDiscovery,
        IProjectMetadataProvider metadataProvider,
        IGeneratedFileWriter fileWriter)
        : this(
            templateDiscovery: templateDiscovery,
            metadataProvider: metadataProvider,
            fileWriter: fileWriter,
            templateRenderer: new TemplateRenderer(typeMapper: new TypeScriptTypeMapper()),
            planner: new GeneratedFilePlanner())
    {
    }

    public TypewriterGenerator(
        ITemplateDiscovery templateDiscovery,
        IProjectMetadataProvider metadataProvider,
        IGeneratedFileWriter fileWriter,
        TemplateRenderer templateRenderer,
        GeneratedFilePlanner planner)
    {
        _templateDiscovery = templateDiscovery;
        _metadataProvider = metadataProvider;
        _fileWriter = fileWriter;
        _templateRenderer = templateRenderer;
        _planner = planner;
    }

    public async Task<GenerationResult> GenerateAsync(
        GenerationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(argument: request);

        var stopwatch = Stopwatch.StartNew();
        var performanceTrace = GenerationPerformanceTrace.Create();
        var diagnostics = new List<GenerationDiagnostic>();
        var generatedFiles = new List<GeneratedFile>();
        var plannedOutputPaths = new Dictionary<string, string>(comparer: StringComparer.OrdinalIgnoreCase);

        var resolutionStopwatch = Stopwatch.StartNew();
        var workspace = new WorkspaceContext(RootPath: WorkspaceProjectResolver.ResolveWorkspacePath(request: request));
        var projectPaths = WorkspaceProjectResolver.ResolveProjectPaths(request: request, workspacePath: workspace.RootPath, diagnostics: diagnostics);
        performanceTrace.Add(stage: "Workspace/project resolution", elapsed: resolutionStopwatch.Elapsed);
        if (projectPaths.Count == 0)
        {
            return CreateResult(stopwatch: stopwatch, generatedFiles: generatedFiles, diagnostics: diagnostics, request: request, performanceTrace: performanceTrace);
        }

        var defaultsStopwatch = Stopwatch.StartNew();
        var renderDefaults = TemplateRenderDefaults.FromConfiguration(
            configuration: request.Configuration,
            solutionFullName: ResolveSolutionFullName(workspaceRootPath: workspace.RootPath));
        performanceTrace.Add(stage: "Render default resolution", elapsed: defaultsStopwatch.Elapsed);
        foreach (var projectPath in projectPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await GenerateProjectAsync(
                request: request,
                workspace: workspace,
                templateWorkspace: CreateProjectWorkspace(request: request, workspace: workspace, projectPath: projectPath, projectCount: projectPaths.Count),
                projectPath: projectPath,
                renderDefaults: renderDefaults,
                diagnostics: diagnostics,
                generatedFiles: generatedFiles,
                plannedOutputPaths: plannedOutputPaths,
                performanceTrace: performanceTrace,
                cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        }

        return CreateResult(stopwatch: stopwatch, generatedFiles: generatedFiles, diagnostics: diagnostics, request: request, performanceTrace: performanceTrace);
    }

    private static void ReportDuplicateOutputPath(
        TemplateDocument template,
        GeneratedFile plannedFile,
        ICollection<GenerationDiagnostic> diagnostics,
        IDictionary<string, string> plannedOutputPaths)
    {
        var normalizedPath = Path.GetFullPath(path: plannedFile.Path);
        if (!plannedOutputPaths.TryGetValue(key: normalizedPath, value: out var firstTemplatePath))
        {
            plannedOutputPaths[key: normalizedPath] = template.Path;
            return;
        }

        var message = string.Equals(a: firstTemplatePath, b: template.Path, comparisonType: StringComparison.OrdinalIgnoreCase)
            ? $"Template '{template.Path}' generates the output file '{normalizedPath}' more than once. The last render wins."
            : $"Output file '{normalizedPath}' is generated by more than one template: '{firstTemplatePath}' and '{template.Path}'. The last render wins.";
        diagnostics.Add(
            item: new GenerationDiagnostic(
                File: template.Path,
                Line: null,
                Column: null,
                Severity: DiagnosticSeverity.Warning,
                Message: message,
                Code: DiagnosticCodes.DuplicateGeneratedOutput));
    }

    private static bool ShouldRenderPerSourceFile(
        TemplateDocument template,
        ProjectMetadata project,
        TemplateRenderInspection? inspection = null)
    {
        return template.OutputPath is null
            && inspection?.IsSingleFileMode != true
            && project.SourceFiles.Any(predicate: sourceFile => sourceFile.Types.Count > 0);
    }

    private static bool RequiresSingleFileModeInspection(TemplateDocument template) =>
        template.OutputPath is null
        && template.CodeBlocks.Any(predicate: block => block.Content.Contains(value: "SingleFileMode", comparisonType: StringComparison.Ordinal));

    private static TemplateRenderResult ApplyLegacySourceOutputPath(
        SourceFileMetadata sourceFile,
        TemplateRenderResult renderResult,
        FileNameConvention fileNameConvention)
    {
        if (!string.IsNullOrWhiteSpace(value: renderResult.OutputPath))
        {
            return renderResult;
        }

        var outputFileName = FileNameConventionFormatter.Format(
            value: Path.GetFileNameWithoutExtension(path: sourceFile.Path),
            convention: fileNameConvention)
            + NormalizeOutputExtension(outputExtension: renderResult.OutputExtension);
        var outputPath = string.IsNullOrWhiteSpace(value: renderResult.OutputDirectory)
            ? outputFileName
            : Path.Combine(path1: renderResult.OutputDirectory, path2: outputFileName);

        return renderResult with { OutputPath = outputPath };
    }

    private static string NormalizeOutputExtension(string? outputExtension)
    {
        if (string.IsNullOrWhiteSpace(value: outputExtension))
        {
            return ".ts";
        }

        return outputExtension.StartsWith(value: '.')
            ? outputExtension
            : "." + outputExtension;
    }

    private static ProjectMetadata CreateSourceFileProject(
        ProjectMetadata project,
        SourceFileMetadata sourceFile)
    {
        return new ProjectMetadata(
            ProjectPath: project.ProjectPath,
            SourceFiles: [sourceFile],
            Types: sourceFile.Types,
            Diagnostics: []);
    }

    private static IReadOnlyList<SourceFileRenderContext> CreateSourceFileRenderContexts(ProjectMetadata project)
    {
        var contexts = new List<SourceFileRenderContext>();
        foreach (var sourceFile in project.SourceFiles.Where(predicate: sourceFile => sourceFile.Types.Count > 0))
        {
            var sourceProject = CreateSourceFileProject(project: project, sourceFile: sourceFile);
            contexts.Add(
                item: new SourceFileRenderContext(
                    SourceFile: sourceFile,
                    Project: sourceProject,
                    MetadataIndex: ProjectMetadataIndex.Create(metadata: sourceProject)));
        }

        return contexts;
    }

    private static TemplateDocument ParseTemplate(
        TemplateFile templateFile,
        ICollection<GenerationDiagnostic> diagnostics)
    {
        if (!TryCreateTemplateDocumentCacheKey(templateFile: templateFile, cacheKey: out var cacheKey))
        {
            return TemplateDocument.Parse(template: templateFile, diagnostics: diagnostics);
        }

        if (!TemplateDocumentCache.TryGetValue(key: cacheKey, value: out var entry)
            || !entry.SourceContent.Equals(value: templateFile.Content, comparisonType: StringComparison.Ordinal))
        {
            var parseDiagnostics = new List<GenerationDiagnostic>();
            entry = new TemplateDocumentCacheEntry(
                Document: TemplateDocument.Parse(template: templateFile, diagnostics: parseDiagnostics),
                Diagnostics: parseDiagnostics.ToArray(),
                SourceContent: templateFile.Content);
            if (TemplateDocumentCache.TryAdd(key: cacheKey, value: entry))
            {
                TemplateDocumentCacheOrder.Enqueue(item: cacheKey);
                TrimTemplateDocumentCache();
            }
            else
            {
                TemplateDocumentCache[key: cacheKey] = entry;
            }
        }

        foreach (var diagnostic in entry.Diagnostics)
        {
            diagnostics.Add(item: diagnostic);
        }

        return entry.Document;
    }

    private static void TrimTemplateDocumentCache()
    {
        while (TemplateDocumentCache.Count > MaxTemplateDocumentCacheEntries)
        {
            if (!TemplateDocumentCacheOrder.TryDequeue(result: out var cacheKey))
            {
                return;
            }

            _ = TemplateDocumentCache.TryRemove(key: cacheKey, value: out _);
        }
    }

    private static bool TryCreateTemplateDocumentCacheKey(
        TemplateFile templateFile,
        out TemplateDocumentCacheKey cacheKey)
    {
        try
        {
            var fullPath = Path.GetFullPath(path: templateFile.Path);
            if (!File.Exists(path: fullPath))
            {
                cacheKey = default!;
                return false;
            }

#pragma warning disable SCS0018
            var file = new FileInfo(fileName: fullPath);
#pragma warning restore SCS0018
            cacheKey = new TemplateDocumentCacheKey(
                Path: fullPath,
                Length: file.Length,
                LastWriteTicks: file.LastWriteTimeUtc.Ticks);
            return true;
        }
        catch (ArgumentException ex)
        {
            _ = ex;
        }
        catch (IOException ex)
        {
            _ = ex;
        }
        catch (NotSupportedException ex)
        {
            _ = ex;
        }
        catch (UnauthorizedAccessException ex)
        {
            _ = ex;
        }

        cacheKey = default!;
        return false;
    }

    private static string? ResolveSolutionFullName(string workspaceRootPath)
    {
        var fullPath = Path.GetFullPath(path: workspaceRootPath);
        var isSolutionFile = fullPath.EndsWith(value: ".sln", comparisonType: StringComparison.OrdinalIgnoreCase)
            || fullPath.EndsWith(value: ".slnx", comparisonType: StringComparison.OrdinalIgnoreCase);
        return isSolutionFile && File.Exists(path: fullPath)
            ? fullPath
            : null;
    }

    private static WorkspaceContext CreateProjectWorkspace(
        GenerationRequest request,
        WorkspaceContext workspace,
        string projectPath,
        int projectCount)
    {
        if (!string.IsNullOrWhiteSpace(value: request.TemplateSearchPath))
        {
            var workspaceBasePath = File.Exists(path: workspace.RootPath)
                ? Path.GetDirectoryName(path: workspace.RootPath) ?? Environment.CurrentDirectory
                : workspace.RootPath;
            var templateSearchPath = Path.IsPathRooted(path: request.TemplateSearchPath)
                ? request.TemplateSearchPath
                : Path.Combine(path1: workspaceBasePath, path2: request.TemplateSearchPath);
            return new WorkspaceContext(RootPath: Path.GetFullPath(path: templateSearchPath));
        }

        if (projectCount <= 1 || !string.IsNullOrWhiteSpace(value: request.TemplatePath))
        {
            return workspace;
        }

        return new WorkspaceContext(
            RootPath: Path.GetDirectoryName(path: Path.GetFullPath(path: projectPath))
                      ?? workspace.RootPath);
    }

    private static GenerationResult CreateResult(
        Stopwatch stopwatch,
        IReadOnlyList<GeneratedFile> generatedFiles,
        ICollection<GenerationDiagnostic> diagnostics,
        GenerationRequest request,
        GenerationPerformanceTrace performanceTrace)
    {
        stopwatch.Stop();
        performanceTrace.Add(stage: "Total generation", elapsed: stopwatch.Elapsed);
        performanceTrace.AddDiagnostics(diagnostics: diagnostics, file: request.WorkspacePath ?? request.ProjectPath ?? request.TemplatePath ?? Environment.CurrentDirectory);
        var hasErrors = diagnostics.Any(predicate: diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        var hasFailedWarnings = request.Configuration.Diagnostics.FailOnWarning
            && diagnostics.Any(predicate: diagnostic => diagnostic.Severity == DiagnosticSeverity.Warning);

        return new GenerationResult(
            Success: !hasErrors && !hasFailedWarnings,
            GeneratedFiles: generatedFiles,
            Diagnostics: diagnostics.ToArray(),
            Duration: stopwatch.Elapsed);
    }

    private static CompiledTemplateFactory? CreateCompiledTemplateFactory(
        TemplateDocument template,
        ICollection<GenerationDiagnostic> diagnostics) =>
        template.CodeBlocks.Count == 0
            ? null
            : TemplateRuntimeCompiler.CompileFactory(template: template, diagnostics: diagnostics);

#pragma warning disable MA0051,S107,S3776
    private async Task GenerateProjectAsync(
        GenerationRequest request,
        WorkspaceContext workspace,
        WorkspaceContext templateWorkspace,
        string projectPath,
        TemplateRenderDefaults renderDefaults,
        ICollection<GenerationDiagnostic> diagnostics,
        ICollection<GeneratedFile> generatedFiles,
        IDictionary<string, string> plannedOutputPaths,
        GenerationPerformanceTrace performanceTrace,
        CancellationToken cancellationToken)
#pragma warning restore MA0051,S107,S3776
    {
        var templateDiscoveryStopwatch = Stopwatch.StartNew();
        var templates = await _templateDiscovery.FindTemplatesAsync(
            workspace: templateWorkspace,
            request: request,
            cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        performanceTrace.Add(stage: $"Template discovery ({Path.GetFileName(path: projectPath)})", elapsed: templateDiscoveryStopwatch.Elapsed);

        if (templates.Count == 0)
        {
            diagnostics.Add(
                item: new GenerationDiagnostic(
                    File: workspace.RootPath,
                    Line: null,
                    Column: null,
                    Severity: DiagnosticSeverity.Warning,
                    Message: $"No .tst templates were found for project: {projectPath}.",
                    Code: null));
            return;
        }

        var metadataStopwatch = Stopwatch.StartNew();
        var project = await _metadataProvider.GetMetadataAsync(
            project: new ProjectContext(
                ProjectPath: projectPath,
                WorkspacePath: workspace.RootPath,
                TargetFramework: request.Configuration.DefaultTargetFramework,
                RunFullDiagnostics: request.Mode == GenerationMode.Validate),
            cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        performanceTrace.Add(stage: $"Metadata load ({Path.GetFileName(path: projectPath)})", elapsed: metadataStopwatch.Elapsed);
        foreach (var diagnostic in project.Diagnostics)
        {
            diagnostics.Add(item: diagnostic);
        }

        if (project.Diagnostics.Any(predicate: diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
        {
            return;
        }

        var projectIndex = ProjectMetadataIndex.Create(metadata: project);
        IReadOnlyList<SourceFileRenderContext>? sourceFileRenderContexts = null;
        foreach (var templateFile in templates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var templateDiagnostics = new List<GenerationDiagnostic>();
            var templateParseStopwatch = Stopwatch.StartNew();
            var template = ParseTemplate(templateFile: templateFile, diagnostics: templateDiagnostics);
#pragma warning disable CA2000
            var compiledTemplateFactory = CreateCompiledTemplateFactory(template: template, diagnostics: templateDiagnostics);
#pragma warning restore CA2000
            performanceTrace.Add(stage: $"Template parse/compile ({Path.GetFileName(path: templateFile.Path)})", elapsed: templateParseStopwatch.Elapsed);
            try
            {
                if (templateDiagnostics.Any(predicate: diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
                {
                    foreach (var templateDiagnostic in templateDiagnostics)
                    {
                        diagnostics.Add(item: templateDiagnostic);
                    }

                    continue;
                }

                TemplateRenderInspection? renderInspection = null;
                if (RequiresSingleFileModeInspection(template: template))
                {
                    var inspectionDiagnostics = new List<GenerationDiagnostic>();
                    renderInspection = TemplateRenderer.InspectTemplate(
                        template: template,
                        metadata: project,
                        diagnostics: inspectionDiagnostics,
                        defaults: renderDefaults,
                        compiledTemplateFactory: compiledTemplateFactory,
                        metadataIndex: projectIndex);
                    if (inspectionDiagnostics.Any(predicate: diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
                    {
                        foreach (var inspectionDiagnostic in inspectionDiagnostics)
                        {
                            diagnostics.Add(item: inspectionDiagnostic);
                        }

                        continue;
                    }
                }

                if (ShouldRenderPerSourceFile(template: template, project: project, inspection: renderInspection))
                {
                    foreach (var templateDiagnostic in templateDiagnostics)
                    {
                        diagnostics.Add(item: templateDiagnostic);
                    }

                    sourceFileRenderContexts ??= CreateSourceFileRenderContexts(project: project);
                    foreach (var sourceContext in sourceFileRenderContexts)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var sourceDiagnostics = new List<GenerationDiagnostic>();
                        var sourceRenderStopwatch = Stopwatch.StartNew();
                        var sourceRenderResult = _templateRenderer.RenderTemplate(
                            template: template,
                            metadata: sourceContext.Project,
                            diagnostics: sourceDiagnostics,
                            defaults: renderDefaults,
                            compiledTemplateFactory: compiledTemplateFactory,
                            metadataIndex: sourceContext.MetadataIndex);
                        performanceTrace.Add(stage: $"Render source file ({Path.GetFileName(path: sourceContext.SourceFile.Path)})", elapsed: sourceRenderStopwatch.Elapsed);
                        foreach (var sourceDiagnostic in sourceDiagnostics)
                        {
                            diagnostics.Add(item: sourceDiagnostic);
                        }

                        if (sourceDiagnostics.Any(predicate: diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
                        {
                            continue;
                        }

                        if (sourceRenderResult.RootItemCount == 0)
                        {
                            continue;
                        }

                        sourceRenderResult = ApplyLegacySourceOutputPath(
                            sourceFile: sourceContext.SourceFile,
                            renderResult: sourceRenderResult,
                            fileNameConvention: request.Configuration.Output.FileNameConvention);
                        await PlanAndWriteAsync(
                            request: request,
                            templateWorkspace: templateWorkspace,
                            template: template,
                            renderResult: sourceRenderResult,
                            diagnostics: diagnostics,
                            generatedFiles: generatedFiles,
                            plannedOutputPaths: plannedOutputPaths,
                            cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
                    }

                    continue;
                }

                var renderStopwatch = Stopwatch.StartNew();
                var renderResult = _templateRenderer.RenderTemplate(
                    template: template,
                    metadata: project,
                    diagnostics: templateDiagnostics,
                    defaults: renderDefaults,
                    compiledTemplateFactory: compiledTemplateFactory,
                    metadataIndex: projectIndex);
                performanceTrace.Add(stage: $"Render template ({Path.GetFileName(path: template.Path)})", elapsed: renderStopwatch.Elapsed);
                foreach (var templateDiagnostic in templateDiagnostics)
                {
                    diagnostics.Add(item: templateDiagnostic);
                }

                await PlanAndWriteAsync(
                    request: request,
                    templateWorkspace: templateWorkspace,
                    template: template,
                    renderResult: renderResult,
                    diagnostics: diagnostics,
                    generatedFiles: generatedFiles,
                    plannedOutputPaths: plannedOutputPaths,
                    cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
            }
            finally
            {
                compiledTemplateFactory?.Dispose();
            }
        }
    }

#pragma warning disable S107
    private async Task PlanAndWriteAsync(
        GenerationRequest request,
        WorkspaceContext templateWorkspace,
        TemplateDocument template,
        TemplateRenderResult renderResult,
        ICollection<GenerationDiagnostic> diagnostics,
        ICollection<GeneratedFile> generatedFiles,
        IDictionary<string, string> plannedOutputPaths,
        CancellationToken cancellationToken)
#pragma warning restore S107
    {
        if (!_planner.TryPlan(
                workspace: templateWorkspace,
                template: template,
                outputPath: renderResult.OutputPath,
                content: renderResult.Content,
                fileNameConvention: request.Configuration.Output.FileNameConvention,
                utf8Bom: renderResult.Utf8Bom,
                generatedFile: out var plannedFile,
                diagnostic: out var diagnostic))
        {
            if (diagnostic is not null)
            {
                diagnostics.Add(item: diagnostic);
            }

            return;
        }

        if (plannedFile is not null)
        {
            ReportDuplicateOutputPath(template: template, plannedFile: plannedFile, diagnostics: diagnostics, plannedOutputPaths: plannedOutputPaths);
            var generatedFile = await _fileWriter.WriteAsync(
                file: plannedFile,
                request: request,
                cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
            generatedFiles.Add(item: generatedFile);
        }
    }

    private sealed class GenerationPerformanceTrace
    {
        private const string EnvironmentVariableName = "TYPEWRITER_TRACE_PERF";
        private readonly List<(string Stage, TimeSpan Elapsed)> _entries = [];

        private GenerationPerformanceTrace(bool enabled)
        {
            Enabled = enabled;
        }

        private bool Enabled { get; }

        public static GenerationPerformanceTrace Create()
        {
            var value = Environment.GetEnvironmentVariable(variable: EnvironmentVariableName);
            var enabled = value is not null
                && (value.Equals(value: "1", comparisonType: StringComparison.OrdinalIgnoreCase)
                    || value.Equals(value: "true", comparisonType: StringComparison.OrdinalIgnoreCase)
                    || value.Equals(value: "yes", comparisonType: StringComparison.OrdinalIgnoreCase));
            return new GenerationPerformanceTrace(enabled: enabled);
        }

        public void Add(
            string stage,
            TimeSpan elapsed)
        {
            if (Enabled)
            {
                _entries.Add(item: (stage, elapsed));
            }
        }

        public void AddDiagnostics(
            ICollection<GenerationDiagnostic> diagnostics,
            string file)
        {
            if (!Enabled)
            {
                return;
            }

            foreach (var entry in _entries)
            {
                diagnostics.Add(
                    item: new GenerationDiagnostic(
                        File: file,
                        Line: null,
                        Column: null,
                        Severity: DiagnosticSeverity.Info,
                        Message: string.Create(
                            provider: CultureInfo.InvariantCulture,
                            handler: $"{entry.Stage}: {entry.Elapsed.TotalMilliseconds:F1} ms"),
                        Code: "TWPERF"));
            }
        }
    }

    private sealed record SourceFileRenderContext(
        SourceFileMetadata SourceFile,
        ProjectMetadata Project,
        ProjectMetadataIndex MetadataIndex);

    private sealed record TemplateDocumentCacheKey(
        string Path,
        long Length,
        long LastWriteTicks);

    private sealed record TemplateDocumentCacheEntry(
        TemplateDocument Document,
        IReadOnlyList<GenerationDiagnostic> Diagnostics,
        string SourceContent);
}
