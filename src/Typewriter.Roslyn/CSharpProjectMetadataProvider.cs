using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Typewriter.Abstractions;
using Typewriter.Buildalyzer;
using Typewriter.CompilerServices;

namespace Typewriter.Roslyn;

public sealed partial class CSharpProjectMetadataProvider : IProjectMetadataProvider
{
    private static readonly ConcurrentDictionary<ProjectMetadataCacheKey, ProjectMetadataCacheEntry> MetadataCache = [];
    private static readonly ConcurrentBag<HashSet<ITypeSymbol>> TypeReferenceVisitedSymbolSets = [];
    private static readonly ConditionalWeakTable<ISymbol, CachedDocComment> DocCommentCache = [];
    private static readonly ConcurrentDictionary<string, CachedSyntaxTree> SyntaxTreeCache = new(comparer: StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentQueue<string> SyntaxTreeCacheOrder = new();
    private static readonly ConcurrentDictionary<string, int> SourceParseCounts = new(comparer: StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, string> LastCacheOutcomeByProject = new(comparer: StringComparer.OrdinalIgnoreCase);
    private readonly IProjectWorkspaceLoader _projectLoader;

    public CSharpProjectMetadataProvider()
        : this(projectLoader: new MsBuildProjectLoader())
    {
    }

    public CSharpProjectMetadataProvider(IProjectWorkspaceLoader projectLoader)
    {
        _projectLoader = projectLoader;
    }

    private enum CacheLookupOutcome
    {
        Miss,
        Hit,
        RebuildFromSources,
    }

    public Task<ProjectMetadata> GetMetadataAsync(
        ProjectContext project,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(argument: project);

        var loadedProjects = new Dictionary<string, ProjectMetadataBuildResult>(comparer: StringComparer.OrdinalIgnoreCase);
        var loadingProjects = new HashSet<string>(comparer: StringComparer.OrdinalIgnoreCase);
        return GetMergedMetadataAsync(
            projectLoader: _projectLoader.CreateSession(),
            project: project,
            loadedProjects: loadedProjects,
            loadingProjects: loadingProjects,
            validationSession: new ManifestValidationSession(cancellationToken: cancellationToken),
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Returns the Roslyn compilation built for the project, reusing the metadata cache.
    /// Used by editor services that need real symbol information for the loaded workspace,
    /// for example IntelliSense inside template C# helper blocks.
    /// </summary>
    /// <param name="project">The project to load the compilation for.</param>
    /// <param name="cancellationToken">Token used to cancel the load.</param>
    /// <returns>The compilation, or <c>null</c> when the project could not be compiled.</returns>
    public async Task<Compilation?> GetCompilationAsync(
        ProjectContext project,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(argument: project);

        var loadedProjects = new Dictionary<string, ProjectMetadataBuildResult>(comparer: StringComparer.OrdinalIgnoreCase);
        var loadingProjects = new HashSet<string>(comparer: StringComparer.OrdinalIgnoreCase);
        var result = await GetMetadataCoreAsync(
            projectLoader: _projectLoader.CreateSession(),
            project: project,
            loadedProjects: loadedProjects,
            loadingProjects: loadingProjects,
            validationSession: new ManifestValidationSession(cancellationToken: cancellationToken),
            cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        return result.Compilation;
    }

    internal static int GetSourceParseCountForTests(string path) =>
        SourceParseCounts.TryGetValue(key: NormalizeSourceCachePath(path: path), value: out var count) ? count : 0;

    internal static string? GetLastCacheOutcomeForTests(string projectPath) =>
        LastCacheOutcomeByProject.TryGetValue(key: NormalizeSourceCachePath(path: projectPath), value: out var outcome) ? outcome : null;

    internal static void ClearCachesForTests()
    {
        MetadataCache.Clear();
        ClearSyntaxTreeCacheForTests();
        SourceParseCounts.Clear();
        LastCacheOutcomeByProject.Clear();
    }

    internal static void ClearSyntaxTreeCacheForTests()
    {
        SyntaxTreeCache.Clear();
        SyntaxTreeCacheOrder.Clear();
    }

    private static async Task<ProjectMetadata> GetMergedMetadataAsync(
        IProjectWorkspaceLoader projectLoader,
        ProjectContext project,
        IDictionary<string, ProjectMetadataBuildResult> loadedProjects,
        ISet<string> loadingProjects,
        ManifestValidationSession validationSession,
        CancellationToken cancellationToken)
    {
        var result = await GetMetadataCoreAsync(
            projectLoader: projectLoader,
            project: project,
            loadedProjects: loadedProjects,
            loadingProjects: loadingProjects,
            validationSession: validationSession,
            cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        var timer = new MetadataStageTimer(project: project);
        var metadata = result.MetadataGraph.GetMetadata(cancellationToken: cancellationToken);
        timer.Complete(stage: "Metadata merge");
        return metadata;
    }

#pragma warning disable MA0051 // Method is too long
    private static async Task<ProjectMetadataBuildResult> GetMetadataCoreAsync(
        IProjectWorkspaceLoader projectLoader,
        ProjectContext project,
        IDictionary<string, ProjectMetadataBuildResult> loadedProjects,
        ISet<string> loadingProjects,
        ManifestValidationSession validationSession,
        CancellationToken cancellationToken)
#pragma warning restore MA0051 // Method is too long
    {
        var projectPath = Path.GetFullPath(path: project.ProjectPath);
        if (loadedProjects.TryGetValue(key: projectPath, value: out var cachedResult))
        {
            return cachedResult;
        }

        var validationVersion = validationSession.Version;
        var metadataCacheKey = new ProjectMetadataCacheKey(
            ProjectPath: projectPath,
            WorkspacePath: Path.GetFullPath(path: project.WorkspacePath),
            TargetFramework: project.TargetFramework ?? string.Empty,
            RunFullDiagnostics: project.RunFullDiagnostics);
        var timer = new MetadataStageTimer(project: project);
        var cacheLookup = LookupCache(cacheKey: metadataCacheKey, validationSession: validationSession);
        if (project.PerformanceSink is not null)
        {
            timer.Complete(stage: $"Cache validation ({cacheLookup.Outcome})");
        }

        if (cacheLookup.Outcome == CacheLookupOutcome.Hit)
        {
            cachedResult = cacheLookup.Result!;
            loadedProjects[key: projectPath] = cachedResult;
            return cachedResult;
        }

        if (!loadingProjects.Add(item: projectPath))
        {
            return new ProjectMetadataBuildResult(
                LocalMetadata: CreateEmptyProjectMetadata(projectPath: projectPath),
                Compilation: null,
                ReferencedProjects: []);
        }

        ProjectLoadResult loadedProject;
        if (cacheLookup.Outcome == CacheLookupOutcome.RebuildFromSources)
        {
            loadedProject = cacheLookup.LoadedProject!;
            MetadataCacheMetrics.RecordSourceOnlyRebuild();
        }
        else
        {
            if (!File.Exists(path: projectPath))
            {
                return CacheResult(
                    projectPath: projectPath,
                    result: new ProjectMetadataBuildResult(
                        LocalMetadata: CreateProjectFileMissingMetadata(projectPath: projectPath),
                        Compilation: null,
                        ReferencedProjects: []),
                    loadedProjects: loadedProjects,
                    loadingProjects: loadingProjects);
            }

            timer = new MetadataStageTimer(project: project);
            loadedProject = await projectLoader.LoadAsync(project: project, cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
            timer.Complete(stage: "MSBuild");
            MetadataCacheMetrics.RecordFullLoad();
        }

        if (loadedProject.Diagnostics.Any(predicate: diagnostic => diagnostic.Severity == Typewriter.Abstractions.DiagnosticSeverity.Error))
        {
            return CacheResult(
                projectPath: projectPath,
                result: new ProjectMetadataBuildResult(
                    LocalMetadata: CreateProjectLoadFailedMetadata(projectPath: projectPath, diagnostics: loadedProject.Diagnostics),
                    Compilation: null,
                    ReferencedProjects: []),
                loadedProjects: loadedProjects,
                loadingProjects: loadingProjects);
        }

        var referencedProjects = new List<ProjectMetadataBuildResult>();
        foreach (var projectReference in loadedProject.ProjectReferences)
        {
            cancellationToken.ThrowIfCancellationRequested();
            referencedProjects.Add(
                item: await GetMetadataCoreAsync(
                    projectLoader: projectLoader,
                    project: new ProjectContext(
                        ProjectPath: projectReference,
                        WorkspacePath: project.WorkspacePath,
                        TargetFramework: project.TargetFramework ?? loadedProject.TargetFramework)
                    {
                        PerformanceSink = project.PerformanceSink,
                    },
                    loadedProjects: loadedProjects,
                    loadingProjects: loadingProjects,
                    validationSession: validationSession,
                    cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false));
        }

        timer = new MetadataStageTimer(project: project);
        var projectReferences = CreateCompilationReferences(
            referencedProjects: referencedProjects,
            cancellationToken: cancellationToken);
        timer.Complete(stage: "Project references");
        var currentProject = await CreateCurrentProjectMetadataAsync(
            project: project,
            loadedProject: loadedProject,
            projectReferences: projectReferences,
            previousSourceTrees: cacheLookup.Result?.SourceTrees,
            cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        var result = new ProjectMetadataBuildResult(
            LocalMetadata: currentProject.LocalMetadata,
            Compilation: currentProject.Compilation,
            ReferencedProjects: referencedProjects)
        {
            SourceTrees = currentProject.SourceTrees,
        };
        timer = new MetadataStageTimer(project: project);
        AddCachedResult(cacheKey: metadataCacheKey, result: result, loadedProject: loadedProject, validatedVersion: validationVersion);
        timer.Complete(stage: "Cache publication");
        return CacheResult(
            projectPath: projectPath,
            result: result,
            loadedProjects: loadedProjects,
            loadingProjects: loadingProjects);
    }

#pragma warning disable MA0051 // Method is too long
    private static async Task<ProjectMetadataBuildResult> CreateCurrentProjectMetadataAsync(
        ProjectContext project,
        ProjectLoadResult loadedProject,
        IReadOnlyList<MetadataReference> projectReferences,
        IReadOnlyDictionary<string, CachedSyntaxTree>? previousSourceTrees,
        CancellationToken cancellationToken)
#pragma warning restore MA0051 // Method is too long
    {
        var projectPath = Path.GetFullPath(path: project.ProjectPath);
        var timer = new MetadataStageTimer(project: project);
        var sourcePaths = loadedProject.SourceFiles.ToArray();
        var parseOptions = CSharpParseOptions.Default
            .WithLanguageVersion(version: LanguageVersion.Preview)
            .WithPreprocessorSymbols(preprocessorSymbols: loadedProject.PreprocessorSymbols);
        var parseOptionsKey = CreateParseOptionsKey(parseOptions: parseOptions);
        var parsedTrees = new SyntaxTree[sourcePaths.Length];
        var sourceEntries = new KeyValuePair<string, CachedSyntaxTree>[sourcePaths.Length];
        await Parallel.ForAsync(
            fromInclusive: 0,
            toExclusive: sourcePaths.Length,
            cancellationToken: cancellationToken,
            body: async (index, token) =>
            {
                token.ThrowIfCancellationRequested();
                var sourcePath = sourcePaths[index];
                var cacheKey = NormalizeSourceCachePath(path: sourcePath);
                var stamp = FileStamp.Create(path: sourcePath);
                CachedSyntaxTree? cachedTree = null;
                _ = previousSourceTrees?.TryGetValue(key: cacheKey, value: out cachedTree);
                if (cachedTree is null
                    || cachedTree.Stamp != stamp
                    || !cachedTree.ParseOptionsKey.Equals(value: parseOptionsKey, comparisonType: StringComparison.Ordinal))
                {
                    _ = SyntaxTreeCache.TryGetValue(key: cacheKey, value: out cachedTree);
                }

                if (stamp.Exists
                    && cachedTree is not null
                    && cachedTree.Stamp == stamp
                    && cachedTree.ParseOptionsKey.Equals(value: parseOptionsKey, comparisonType: StringComparison.Ordinal))
                {
                    parsedTrees[index] = cachedTree.Tree;
                    sourceEntries[index] = new KeyValuePair<string, CachedSyntaxTree>(key: cacheKey, value: cachedTree);
                    MetadataCacheMetrics.RecordReusedSyntaxTree();
                    return;
                }

#pragma warning disable SCS0018 // Potential Path Traversal vulnerability was found where '{0}' in '{1}' may be tainted by user-controlled data from '{2}' in method '{3}'.
                var sourceText = await File.ReadAllTextAsync(path: sourcePath, cancellationToken: token).ConfigureAwait(continueOnCapturedContext: false);
#pragma warning restore SCS0018 // Potential Path Traversal vulnerability was found where '{0}' in '{1}' may be tainted by user-controlled data from '{2}' in method '{3}'.
                var parsedTree = CSharpSyntaxTree.ParseText(
                    text: sourceText,
                    options: parseOptions,
                    path: sourcePath,
                    cancellationToken: token);
                parsedTrees[index] = parsedTree;
                var entry = new CachedSyntaxTree(
                    ParseOptionsKey: parseOptionsKey,
                    Stamp: stamp == FileStamp.Create(path: sourcePath) ? stamp : FileStamp.Unreadable,
                    Tree: parsedTree);
                sourceEntries[index] = new KeyValuePair<string, CachedSyntaxTree>(key: cacheKey, value: entry);
                AddCachedSyntaxTree(cacheKey: cacheKey, entry: entry);
                MetadataCacheMetrics.RecordParsedSourceFile();
                _ = SourceParseCounts.AddOrUpdate(key: cacheKey, addValue: 1, updateValueFactory: static (_, count) => count + 1);
            }).ConfigureAwait(continueOnCapturedContext: false);

        // Owned by this build result; global eviction must not lose trees still in its compilation.
        var sourceTrees = new Dictionary<string, CachedSyntaxTree>(capacity: sourceEntries.Length, comparer: StringComparer.OrdinalIgnoreCase);
        foreach (var entry in sourceEntries)
        {
            sourceTrees[key: entry.Key] = entry.Value;
        }

        timer.Complete(stage: "Parsing");
        timer = new MetadataStageTimer(project: project);
        var syntaxTrees = new List<SyntaxTree>(capacity: sourcePaths.Length + 1);
        syntaxTrees.AddRange(collection: parsedTrees);
        syntaxTrees.Add(item: CreateDefaultGlobalUsingsTree(globalUsings: loadedProject.GlobalUsings, parseOptions: parseOptions, cancellationToken: cancellationToken));

        var compilation = CSharpCompilation.Create(
            assemblyName: Path.GetFileNameWithoutExtension(path: projectPath),
            syntaxTrees: syntaxTrees,
            references: CreateReferences(referencePaths: loadedProject.ReferencePaths).Concat(second: projectReferences),
            options: new CSharpCompilationOptions(
                outputKind: OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        timer.Complete(stage: "Compilation/references");
        timer = new MetadataStageTimer(project: project);
        compilation = RunSourceGenerators(
            compilation: compilation,
            loadedProject: loadedProject,
            parseOptions: parseOptions,
            cancellationToken: cancellationToken,
            diagnostics: out var generatorDiagnostics);
        timer.Complete(stage: "Source generators");
        timer = new MetadataStageTimer(project: project);
        var diagnostics = GetCompilationDiagnostics(
                compilation: compilation,
                generatorDiagnostics: generatorDiagnostics,
                runFullDiagnostics: project.RunFullDiagnostics,
                cancellationToken: cancellationToken)
            .Where(predicate: diagnostic => diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
            .Select(selector: ToGenerationDiagnostic)
            .ToArray();

        timer.Complete(stage: "Diagnostics");
        timer = new MetadataStageTimer(project: project);
        var extractor = new MetadataExtractor();
        var sourceSymbols = extractor.GetSourceSymbols(compilation: compilation, syntaxTrees: syntaxTrees, cancellationToken: cancellationToken);
        var symbolMetadata = sourceSymbols.Types
            .Select(
                selector: symbol => new
                {
                    Symbol = symbol,
                    HasMetadata = extractor.TryCreateType(symbol: symbol, compilation: compilation, cancellationToken: cancellationToken, metadata: out var metadata),
                    Metadata = metadata,
                })
            .Where(predicate: item => item.HasMetadata)
            .ToArray();
        var delegateMetadata = sourceSymbols.Delegates
            .Select(
                selector: symbol => new
                {
                    Symbol = symbol,
                    HasMetadata = extractor.TryCreateDelegate(symbol: symbol, compilation: compilation, metadata: out var metadata),
                    Metadata = metadata,
                })
            .Where(predicate: item => item.HasMetadata)
            .ToArray();

        var typesByFile = symbolMetadata
            .SelectMany(
                selector: item => item.Symbol.DeclaringSyntaxReferences
                    .Select(selector: reference => reference.SyntaxTree.FilePath)
                    .Where(predicate: path => !string.IsNullOrWhiteSpace(value: path))
                    .Select(selector: path => new
                    {
                        Path = Path.GetFullPath(path: path),
                        Type = item.Metadata,
                    }))
            .GroupBy(keySelector: item => item.Path, comparer: StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                keySelector: group => group.Key,
                elementSelector: group => (IReadOnlyList<TypeMetadata>)group.Select(selector: item => item.Type).ToArray(),
                comparer: StringComparer.OrdinalIgnoreCase);
        var delegatesByFile = delegateMetadata
            .SelectMany(
                selector: item => item.Symbol.DeclaringSyntaxReferences
                    .Select(selector: reference => reference.SyntaxTree.FilePath)
                    .Where(predicate: path => !string.IsNullOrWhiteSpace(value: path))
                    .Select(selector: path => new
                    {
                        Path = Path.GetFullPath(path: path),
                        Delegate = item.Metadata,
                    }))
            .GroupBy(keySelector: item => item.Path, comparer: StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                keySelector: group => group.Key,
                elementSelector: group => (IReadOnlyList<DelegateMetadata>)group.Select(selector: item => item.Delegate).ToArray(),
                comparer: StringComparer.OrdinalIgnoreCase);

        var sourceFiles = sourcePaths
            .Select(
                selector: path =>
                {
                    var fullPath = Path.GetFullPath(path: path);
                    return new SourceFileMetadata(
                        Path: fullPath,
                        Types: typesByFile.TryGetValue(key: fullPath, value: out var types) ? types : [])
                    {
                        Delegates = delegatesByFile.TryGetValue(key: fullPath, value: out var delegates)
                            ? delegates
                            : [],
                    };
                })
            .ToArray();

        var metadata = new ProjectMetadata(
            ProjectPath: projectPath,
            SourceFiles: sourceFiles,
            Types: symbolMetadata
                .Select(selector: item => item.Metadata)
                .OrderBy(keySelector: type => type.FullName, comparer: StringComparer.Ordinal)
                .ToArray(),
            Diagnostics: loadedProject.Diagnostics.Concat(second: diagnostics).ToArray())
        {
            Delegates = delegateMetadata
                .Select(selector: item => item.Metadata)
                .OrderBy(keySelector: type => type.FullName, comparer: StringComparer.Ordinal)
                .ToArray(),
        };

        timer.Complete(stage: "Metadata extraction");
        return new ProjectMetadataBuildResult(
            LocalMetadata: metadata,
            Compilation: compilation,
            ReferencedProjects: [])
        {
            SourceTrees = sourceTrees,
        };
    }

    private static IReadOnlyList<MetadataReference> CreateCompilationReferences(
        IReadOnlyList<ProjectMetadataBuildResult> referencedProjects,
        CancellationToken cancellationToken)
    {
        var references = new List<MetadataReference>();
        var seenProjectPaths = new HashSet<string>(comparer: StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<ProjectMetadataBuildResult>(comparer: ReferenceEqualityComparer.Instance);
        var pending = new Stack<ProjectMetadataBuildResult>();
        for (var index = referencedProjects.Count - 1; index >= 0; index--)
        {
            pending.Push(item: referencedProjects[index]);
        }

        // Keep transitive compilation references (required for symbol binding), but
        // traverse the DAG once instead of concatenating every child's full closure.
        while (pending.TryPop(result: out var current))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!visited.Add(item: current))
            {
                continue;
            }

            if (current.CompilationReference is not null
                && seenProjectPaths.Add(item: current.LocalMetadata.ProjectPath))
            {
                references.Add(item: current.CompilationReference);
            }

            for (var index = current.ReferencedProjects.Count - 1; index >= 0; index--)
            {
                pending.Push(item: current.ReferencedProjects[index]);
            }
        }

        return references;
    }

    private static CacheLookupResult LookupCache(
        ProjectMetadataCacheKey cacheKey,
        ManifestValidationSession validationSession)
    {
        if (!MetadataCache.TryGetValue(key: cacheKey, value: out var entry))
        {
            MetadataCacheMetrics.RecordCacheMiss(reason: null);
            RecordCacheOutcome(projectPath: cacheKey.ProjectPath, outcome: "miss");
            return CacheLookupResult.Miss;
        }

        var snapshot = validationSession.GetSnapshot(sinceVersion: entry.LastValidatedVersion);
        var dirtyPaths = snapshot.Paths;
        ManifestValidation validation;
        bool dirtyValidated;
        if (dirtyPaths is not null)
        {
            dirtyValidated = true;
            validation = dirtyPaths.Count == 0
                ? ManifestValidation.Valid
                : validationSession.Validate(manifest: entry.Manifest);
        }
        else
        {
            dirtyValidated = false;
            validation = validationSession.Validate(manifest: entry.Manifest);
        }

        if (validation.IsValid)
        {
            entry.LastValidatedVersion = snapshot.Version;
            MetadataCacheMetrics.RecordCacheHit(dirtyValidated: dirtyValidated);
            RecordCacheOutcome(projectPath: cacheKey.ProjectPath, outcome: dirtyValidated ? "hit-dirty-validated" : "hit-stat-validated");
            return new CacheLookupResult(Outcome: CacheLookupOutcome.Hit, Result: entry.Result, LoadedProject: null);
        }

        _ = MetadataCache.TryRemove(key: cacheKey, value: out _);
        MetadataCacheMetrics.RecordCacheMiss(reason: validation.InvalidationReason);
        if (validation.CanRebuildFromSources && entry.LoadedProject is not null)
        {
            RecordCacheOutcome(projectPath: cacheKey.ProjectPath, outcome: "source-rebuild");
            return new CacheLookupResult(Outcome: CacheLookupOutcome.RebuildFromSources, Result: entry.Result, LoadedProject: entry.LoadedProject);
        }

        RecordCacheOutcome(projectPath: cacheKey.ProjectPath, outcome: "miss");
        return CacheLookupResult.Miss;
    }

    private static void AddCachedResult(
        ProjectMetadataCacheKey cacheKey,
        ProjectMetadataBuildResult result,
        ProjectLoadResult loadedProject,
        long validatedVersion)
    {
        if (result.MetadataGraph.HasErrors)
        {
            return;
        }

        result.InputManifest = ProjectInputManifest.Create(result: result, validatedVersion: validatedVersion);
        MetadataCache[key: cacheKey] = new ProjectMetadataCacheEntry(
            result: result,
            manifest: result.InputManifest,
            loadedProject: loadedProject,
            validatedVersion: validatedVersion);
        TrimMetadataCache();
    }

    private static void RecordCacheOutcome(
        string projectPath,
        string outcome) =>
        LastCacheOutcomeByProject[key: projectPath] = outcome;

    private static string NormalizeSourceCachePath(string path)
    {
        try
        {
            return Path.GetFullPath(path: path);
        }
        catch (ArgumentException)
        {
            return path;
        }
        catch (NotSupportedException)
        {
            return path;
        }
        catch (PathTooLongException)
        {
            return path;
        }
    }

    private static string CreateParseOptionsKey(CSharpParseOptions parseOptions) =>
        string.Join(
            separator: ';',
            values: parseOptions.PreprocessorSymbolNames
                .Order(comparer: StringComparer.Ordinal)
                .Prepend(element: parseOptions.LanguageVersion.ToString()));

    private static void AddCachedSyntaxTree(
        string cacheKey,
        CachedSyntaxTree entry)
    {
        if (SyntaxTreeCache.TryAdd(key: cacheKey, value: entry))
        {
            SyntaxTreeCacheOrder.Enqueue(item: cacheKey);
            TrimSyntaxTreeCache();
            return;
        }

        SyntaxTreeCache[key: cacheKey] = entry;
    }

    private static void TrimSyntaxTreeCache()
    {
        const int MaxEntries = 8192;
        while (SyntaxTreeCache.Count > MaxEntries && SyntaxTreeCacheOrder.TryDequeue(result: out var oldest))
        {
            _ = SyntaxTreeCache.TryRemove(key: oldest, value: out _);
        }
    }

    private static void TrimMetadataCache()
    {
        const int MaxEntries = 32;
        while (MetadataCache.Count > MaxEntries)
        {
            var key = MetadataCache.Keys.FirstOrDefault();
            if (key is null || !MetadataCache.TryRemove(key: key, value: out _))
            {
                return;
            }
        }
    }

    private static ProjectMetadataBuildResult CacheResult(
        string projectPath,
        ProjectMetadataBuildResult result,
        IDictionary<string, ProjectMetadataBuildResult> loadedProjects,
        ISet<string> loadingProjects)
    {
        loadingProjects.Remove(item: projectPath);
        loadedProjects[key: projectPath] = result;
        return result;
    }

    private static ProjectMetadata CreateEmptyProjectMetadata(string projectPath) =>
        new(
            ProjectPath: projectPath,
            SourceFiles: [],
            Types: [],
            Diagnostics: []);

    private static ProjectMetadata CreateProjectFileMissingMetadata(string projectPath) =>
        new(
            ProjectPath: projectPath,
            SourceFiles: [],
            Types: [],
            Diagnostics:
            [
                new GenerationDiagnostic(
                    File: projectPath,
                    Line: null,
                    Column: null,
                    Severity: Typewriter.Abstractions.DiagnosticSeverity.Error,
                    Message: $"Project file does not exist: {projectPath}.",
                    Code: "TW0003"),
            ]);

    private static ProjectMetadata CreateProjectLoadFailedMetadata(
        string projectPath,
        IReadOnlyList<GenerationDiagnostic> diagnostics) =>
        new(
            ProjectPath: projectPath,
            SourceFiles: [],
            Types: [],
            Diagnostics: diagnostics);

    private static SyntaxTree CreateDefaultGlobalUsingsTree(
        IEnumerable<string> globalUsings,
        CSharpParseOptions parseOptions,
        CancellationToken cancellationToken)
    {
        var source = string.Join(
            separator: Environment.NewLine,
            values: globalUsings.Select(selector: usingName => $"global using {usingName};"));

        return CSharpSyntaxTree.ParseText(
            text: source,
            options: parseOptions,
            path: "__Typewriter.GlobalUsings.g.cs",
            cancellationToken: cancellationToken);
    }

    private static IEnumerable<MetadataReference> CreateReferences(IEnumerable<string> referencePaths)
    {
        var projectReferences = referencePaths
            .Where(predicate: File.Exists)
            .Distinct(comparer: StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (projectReferences.Length > 0)
        {
            return projectReferences.Select(selector: path => StampedAssemblyReferenceCache.Shared.GetReference(path: path));
        }

        var trustedAssemblies = (string?)AppContext.GetData(name: "TRUSTED_PLATFORM_ASSEMBLIES");
        if (string.IsNullOrWhiteSpace(value: trustedAssemblies))
        {
            return [];
        }

        return trustedAssemblies
            .Split(separator: Path.PathSeparator, options: StringSplitOptions.RemoveEmptyEntries)
            .Distinct(comparer: StringComparer.OrdinalIgnoreCase)
            .Select(selector: path => StampedAssemblyReferenceCache.Shared.GetReference(path: path));
    }

    private static CSharpCompilation RunSourceGenerators(
        CSharpCompilation compilation,
        ProjectLoadResult loadedProject,
        CSharpParseOptions parseOptions,
        CancellationToken cancellationToken,
        out ImmutableArray<Diagnostic> diagnostics)
    {
        using var loader = new AnalyzerAssemblyLoader();
        var generators = CreateSourceGenerators(analyzerReferences: loadedProject.AnalyzerReferences, loader: loader).ToArray();
        if (generators.Length == 0)
        {
            diagnostics = [];
            return compilation;
        }

        var additionalTexts = loadedProject.AdditionalFiles
            .Where(predicate: File.Exists)
            .Select(selector: path => new FileAdditionalText(path: path))
            .ToArray();
        var analyzerConfigOptionsProvider = CreateAnalyzerConfigOptionsProvider(
            analyzerConfigFiles: loadedProject.AnalyzerConfigFiles,
            cancellationToken: cancellationToken);
        var driver = CSharpGeneratorDriver.Create(
            generators: generators,
            additionalTexts: additionalTexts,
            parseOptions: parseOptions,
            optionsProvider: analyzerConfigOptionsProvider);
        _ = driver.RunGeneratorsAndUpdateCompilation(
            compilation: compilation,
            outputCompilation: out var updatedCompilation,
            diagnostics: out diagnostics,
            cancellationToken: cancellationToken);

        return (CSharpCompilation)updatedCompilation;
    }

    private static AnalyzerConfigOptionsProvider CreateAnalyzerConfigOptionsProvider(
        IReadOnlyList<string> analyzerConfigFiles,
        CancellationToken cancellationToken) =>
        new ProjectAnalyzerConfigOptionsProvider(
            globalOptions: LoadGlobalAnalyzerConfigOptions(
                analyzerConfigFiles: analyzerConfigFiles,
                cancellationToken: cancellationToken));

#pragma warning disable S3776
    private static IReadOnlyDictionary<string, string> LoadGlobalAnalyzerConfigOptions(
        IEnumerable<string> analyzerConfigFiles,
        CancellationToken cancellationToken)
    {
        var options = new Dictionary<string, string>(comparer: StringComparer.OrdinalIgnoreCase);
        foreach (var analyzerConfigFile in analyzerConfigFiles.Where(predicate: File.Exists).Distinct(comparer: StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fileOptions = new Dictionary<string, string>(comparer: StringComparer.OrdinalIgnoreCase);
            var isGlobal = false;

#pragma warning disable SCS0018,SEC0116
            foreach (var line in File.ReadLines(path: analyzerConfigFile))
#pragma warning restore SCS0018,SEC0116
            {
                cancellationToken.ThrowIfCancellationRequested();
                var trimmedLine = line.Trim();
                if (trimmedLine.Length == 0
                    || trimmedLine.StartsWith(value: '#')
                    || trimmedLine.StartsWith(value: ';'))
                {
                    continue;
                }

                if (trimmedLine.StartsWith(value: '['))
                {
                    break;
                }

                var separatorIndex = trimmedLine.IndexOf(value: '=');
                if (separatorIndex < 0)
                {
                    continue;
                }

                var key = trimmedLine[..separatorIndex].Trim();
                if (key.Length == 0)
                {
                    continue;
                }

                var value = trimmedLine[(separatorIndex + 1)..].Trim();
                if (key.Equals(value: "is_global", comparisonType: StringComparison.OrdinalIgnoreCase))
                {
                    isGlobal = value.Equals(value: "true", comparisonType: StringComparison.OrdinalIgnoreCase);
                    continue;
                }

                fileOptions[key: key] = value;
            }

            if (!isGlobal)
            {
                continue;
            }

            foreach (var option in fileOptions)
            {
                options[key: option.Key] = option.Value;
            }
        }

        return options;
    }
#pragma warning restore S3776

    private static IEnumerable<ISourceGenerator> CreateSourceGenerators(
        IEnumerable<string> analyzerReferences,
        IAnalyzerAssemblyLoader loader)
    {
        foreach (var analyzerReference in analyzerReferences.Where(predicate: File.Exists).Distinct(comparer: StringComparer.OrdinalIgnoreCase))
        {
            var reference = new AnalyzerFileReference(fullPath: analyzerReference, assemblyLoader: loader);
            foreach (var generator in reference.GetGenerators(language: LanguageNames.CSharp))
            {
                yield return generator;
            }
        }
    }

    private static IEnumerable<Diagnostic> GetCompilationDiagnostics(
        CSharpCompilation compilation,
        ImmutableArray<Diagnostic> generatorDiagnostics,
        bool runFullDiagnostics,
        CancellationToken cancellationToken)
    {
        return runFullDiagnostics
            ? compilation.GetDiagnostics(cancellationToken: cancellationToken).Concat(second: generatorDiagnostics)
            : compilation.GetParseDiagnostics(cancellationToken: cancellationToken)
                .Concat(second: compilation.GetDeclarationDiagnostics(cancellationToken: cancellationToken))
                .Concat(second: generatorDiagnostics);
    }

    private static GenerationDiagnostic ToGenerationDiagnostic(Diagnostic diagnostic)
    {
        var location = diagnostic.Location.GetLineSpan();
        return new GenerationDiagnostic(
            File: location.Path,
            Line: location.StartLinePosition.Line + 1,
            Column: location.StartLinePosition.Character + 1,
            Severity: Typewriter.Abstractions.DiagnosticSeverity.Error,
            Message: diagnostic.GetMessage(formatProvider: CultureInfo.InvariantCulture),
            Code: "TW0004");
    }

    private sealed record SourceSymbols(
        IReadOnlyList<INamedTypeSymbol> Types,
        IReadOnlyList<INamedTypeSymbol> Delegates);

    private sealed record ProjectMetadataCacheKey(
        string ProjectPath,
        string WorkspacePath,
        string TargetFramework,
        bool RunFullDiagnostics);

    private sealed record CacheLookupResult(
        CacheLookupOutcome Outcome,
        ProjectMetadataBuildResult? Result,
        ProjectLoadResult? LoadedProject)
    {
        public static CacheLookupResult Miss { get; } = new(Outcome: CacheLookupOutcome.Miss, Result: null, LoadedProject: null);
    }

    private sealed class ProjectMetadataCacheEntry
    {
        private long _lastValidatedVersion;

        public ProjectMetadataCacheEntry(
            ProjectMetadataBuildResult result,
            ProjectInputManifest manifest,
            ProjectLoadResult? loadedProject,
            long validatedVersion)
        {
            Result = result;
            Manifest = manifest;
            LoadedProject = loadedProject;
            _lastValidatedVersion = validatedVersion;
        }

        public ProjectMetadataBuildResult Result { get; }

        public ProjectInputManifest Manifest { get; }

        public ProjectLoadResult? LoadedProject { get; }

        public long LastValidatedVersion
        {
            get => Interlocked.Read(location: ref _lastValidatedVersion);
            set => Interlocked.Exchange(location1: ref _lastValidatedVersion, value: value);
        }
    }

    private sealed record FileStamp(
        bool Exists,
        long Length,
        long LastWriteTicks)
    {
        public static FileStamp Missing { get; } = new(Exists: false, Length: -1, LastWriteTicks: -1);

        public static FileStamp Unreadable { get; } = new(Exists: false, Length: -2, LastWriteTicks: -2);

        public static FileStamp Create(string path)
        {
            try
            {
#pragma warning disable SCS0018 // Potential Path Traversal vulnerability was found where '{0}' in '{1}' may be tainted by user-controlled data from '{2}' in method '{3}'.
                var info = new FileInfo(fileName: path);
#pragma warning restore SCS0018 // Potential Path Traversal vulnerability was found where '{0}' in '{1}' may be tainted by user-controlled data from '{2}' in method '{3}'.
                return info.Exists
                    ? new FileStamp(Exists: true, Length: info.Length, LastWriteTicks: info.LastWriteTimeUtc.Ticks)
                    : Missing;
            }
            catch (IOException)
            {
                return Unreadable;
            }
            catch (UnauthorizedAccessException)
            {
                return Unreadable;
            }
            catch (ArgumentException)
            {
                return Unreadable;
            }
            catch (NotSupportedException)
            {
                return Unreadable;
            }
        }
    }

    private sealed record CachedSyntaxTree(
        string ParseOptionsKey,
        FileStamp Stamp,
        SyntaxTree Tree);

    private sealed class LazyTypeReferenceList(Func<IReadOnlyList<TypeMetadataReference>> valueFactory) : IReadOnlyList<TypeMetadataReference>
    {
        private readonly Lazy<IReadOnlyList<TypeMetadataReference>> _references = new(valueFactory: valueFactory);

        public int Count => _references.Value.Count;

        public TypeMetadataReference this[int index] => _references.Value[index];

        public IEnumerator<TypeMetadataReference> GetEnumerator() => _references.Value.GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class TypeReferenceCacheNode
    {
        public TypeMetadataReference?[] ByAnnotation { get; } = new TypeMetadataReference?[3];
    }

    private sealed record ManifestValidation(
        bool IsValid,
        bool CanRebuildFromSources,
        IReadOnlyList<string> ChangedSourceFiles,
        string? InvalidationReason)
    {
        public static ManifestValidation Valid { get; } = new(
            IsValid: true,
            CanRebuildFromSources: false,
            ChangedSourceFiles: [],
            InvalidationReason: null);

        public static ManifestValidation ShapeChanged(string reason) => new(
            IsValid: false,
            CanRebuildFromSources: false,
            ChangedSourceFiles: [],
            InvalidationReason: reason);

        public static ManifestValidation SourcesChanged(IReadOnlyList<string> changedSourceFiles) => new(
            IsValid: false,
            CanRebuildFromSources: true,
            ChangedSourceFiles: changedSourceFiles,
            InvalidationReason: $"source changed: {changedSourceFiles[0]}");
    }

    private sealed record DirtyPathSnapshot(long Version, MetadataDirtyPathIndex? Paths);

    private sealed class ManifestValidationSession(CancellationToken cancellationToken)
    {
        private readonly Dictionary<ProjectInputManifest, ManifestValidation> _validations = new(comparer: ReferenceEqualityComparer.Instance);
        private readonly Dictionary<long, DirtyPathSnapshot> _snapshots = [];

        // Later lookups may observe newer edits, but must never acknowledge them
        // using a manifest result memoized earlier in this request.
        public long Version { get; } = MetadataCacheInvalidation.CurrentVersion;

        public DirtyPathSnapshot GetSnapshot(long sinceVersion)
        {
            if (!_snapshots.TryGetValue(key: sinceVersion, value: out var snapshot))
            {
                var dirtyPaths = MetadataCacheInvalidation.GetDirtySince(sinceVersion: sinceVersion);
                snapshot = new DirtyPathSnapshot(
                    Version: Version,
                    Paths: dirtyPaths is null ? null : new MetadataDirtyPathIndex(paths: dirtyPaths));
                _snapshots.Add(key: sinceVersion, value: snapshot);
            }

            return snapshot;
        }

        public ManifestValidation Validate(ProjectInputManifest manifest)
        {
            var pending = new Stack<(ProjectInputManifest Manifest, bool Expanded)>();
            pending.Push(item: (manifest, false));
            while (pending.TryPop(result: out var item))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (_validations.ContainsKey(key: item.Manifest))
                {
                    continue;
                }

                if (!item.Expanded)
                {
                    pending.Push(item: (item.Manifest, true));
                    foreach (var reference in item.Manifest.References)
                    {
                        pending.Push(item: (reference, false));
                    }

                    continue;
                }

                var validation = item.Manifest.ValidateOwnInputs(session: this);
                foreach (var reference in item.Manifest.References)
                {
                    var referenceValidation = _validations[key: reference];
                    if (!referenceValidation.IsValid
                        && (validation.IsValid || !referenceValidation.CanRebuildFromSources))
                    {
                        validation = referenceValidation;
                    }
                }

                _validations.Add(key: item.Manifest, value: validation);
            }

            return _validations[key: manifest];
        }
    }

    private sealed class ProjectInputManifest
    {
        private readonly Dictionary<string, FileStamp> _sourceFiles;
        private readonly Dictionary<string, FileStamp> _shapeFiles;
        private readonly Dictionary<string, long> _directories;
        private long _lastValidatedVersion;

        private ProjectInputManifest(
            Dictionary<string, FileStamp> sourceFiles,
            Dictionary<string, FileStamp> shapeFiles,
            Dictionary<string, long> directories,
            IReadOnlyList<ProjectInputManifest> references,
            long validatedVersion)
        {
            _sourceFiles = sourceFiles;
            _shapeFiles = shapeFiles;
            _directories = directories;
            References = references;
            _lastValidatedVersion = validatedVersion;
        }

        public IReadOnlyList<ProjectInputManifest> References { get; }

        public static ProjectInputManifest Create(
            ProjectMetadataBuildResult result,
            long validatedVersion)
        {
            var sourceFiles = new Dictionary<string, FileStamp>(comparer: StringComparer.OrdinalIgnoreCase);
            foreach (var sourceFile in result.LocalMetadata.SourceFiles)
            {
                var path = NormalizeSourceCachePath(path: sourceFile.Path);
                sourceFiles[key: path] = result.SourceTrees.TryGetValue(key: path, value: out var sourceTree)
                    ? sourceTree.Stamp
                    : FileStamp.Create(path: path);
            }

            var shapeFiles = new Dictionary<string, FileStamp>(comparer: StringComparer.OrdinalIgnoreCase);
            AddStamp(stamps: shapeFiles, path: result.LocalMetadata.ProjectPath);

            if (result.Compilation is not null)
            {
                foreach (var referencePath in result.Compilation.References.Select(selector: reference => reference.Display))
                {
                    AddStamp(stamps: shapeFiles, path: referencePath);
                }
            }

            var coveredDirectories = result.ReferencedProjects
                .Where(predicate: reference => reference.InputManifest is not null)
                .Select(selector: reference => Path.GetDirectoryName(path: reference.LocalMetadata.ProjectPath))
                .OfType<string>()
                .ToHashSet(comparer: StringComparer.OrdinalIgnoreCase);
            var directories = new Dictionary<string, long>(comparer: StringComparer.OrdinalIgnoreCase);
            var projectDirectory = Path.GetDirectoryName(path: result.LocalMetadata.ProjectPath);
            if (!string.IsNullOrWhiteSpace(value: projectDirectory) && Directory.Exists(path: projectDirectory))
            {
                foreach (var directory in EnumerateInputDirectories(root: projectDirectory, coveredDirectories: coveredDirectories))
                {
                    directories[key: directory] = GetDirectoryStamp(directory: directory);
                }
            }

            foreach (var sourceDirectory in result.LocalMetadata.SourceFiles
                         .Select(selector: sourceFile => Path.GetDirectoryName(path: sourceFile.Path))
                         .Where(predicate: sourceDirectory => !string.IsNullOrWhiteSpace(value: sourceDirectory)))
            {
                var directory = Path.GetFullPath(path: sourceDirectory!);
                if (!directories.ContainsKey(key: directory))
                {
                    directories[key: directory] = GetDirectoryStamp(directory: directory);
                }
            }

            return new ProjectInputManifest(
                sourceFiles: sourceFiles,
                shapeFiles: shapeFiles,
                directories: directories,
                references: result.ReferencedProjects
                    .Select(selector: reference => reference.InputManifest)
                    .OfType<ProjectInputManifest>()
                    .ToArray(),
                validatedVersion: validatedVersion);
        }

        public ManifestValidation ValidateOwnInputs(ManifestValidationSession session)
        {
            var snapshot = session.GetSnapshot(sinceVersion: Interlocked.Read(location: ref _lastValidatedVersion));
            var dirtyPaths = snapshot.Paths;
            var validation = dirtyPaths is null
                ? ValidateInputs()
                : ValidateDirtyPaths(dirtyPaths: dirtyPaths);
            if (validation.IsValid)
            {
                Interlocked.Exchange(location1: ref _lastValidatedVersion, value: snapshot.Version);
            }

            return validation;
        }

        private static void AddStamp(
            Dictionary<string, FileStamp> stamps,
            string? path)
        {
            if (string.IsNullOrWhiteSpace(value: path))
            {
                return;
            }

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(path: path);
            }
            catch (ArgumentException)
            {
                return;
            }
            catch (NotSupportedException)
            {
                return;
            }
            catch (PathTooLongException)
            {
                return;
            }

            if (!stamps.ContainsKey(key: fullPath))
            {
                stamps[key: fullPath] = FileStamp.Create(path: fullPath);
            }
        }

        private static long GetDirectoryStamp(string directory)
        {
            try
            {
                return Directory.Exists(path: directory)
                    ? new DirectoryInfo(path: directory).LastWriteTimeUtc.Ticks
                    : -1L;
            }
            catch (IOException)
            {
                return -2L;
            }
            catch (UnauthorizedAccessException)
            {
                return -2L;
            }
        }

        private static IEnumerable<string> EnumerateInputDirectories(
            string root,
            ISet<string> coveredDirectories)
        {
            var parent = (string?)Path.GetFullPath(path: root);
            while (!string.IsNullOrEmpty(value: parent))
            {
                if (coveredDirectories.Contains(item: parent))
                {
                    yield break;
                }

                if (IsIgnoredInputDirectory(name: Path.GetFileName(path: parent)))
                {
                    break;
                }

                parent = Path.GetDirectoryName(path: parent);
            }

            var pending = new Stack<string>();
            pending.Push(item: Path.GetFullPath(path: root));
            while (pending.Count > 0)
            {
                var directory = pending.Pop();
                if (coveredDirectories.Contains(item: directory))
                {
                    continue;
                }

                yield return directory;
                foreach (var childDirectory in Directory.EnumerateDirectories(path: directory)
                             .Where(predicate: childDirectory => !IsIgnoredInputDirectory(name: Path.GetFileName(path: childDirectory))))
                {
                    pending.Push(item: childDirectory);
                }
            }
        }

        private static bool IsIgnoredInputDirectory(string name) =>
            name.Equals(value: ".git", comparisonType: StringComparison.OrdinalIgnoreCase)
            || name.Equals(value: "artifacts", comparisonType: StringComparison.OrdinalIgnoreCase)
            || name.Equals(value: "bin", comparisonType: StringComparison.OrdinalIgnoreCase)
            || name.Equals(value: "node_modules", comparisonType: StringComparison.OrdinalIgnoreCase)
            || name.Equals(value: "obj", comparisonType: StringComparison.OrdinalIgnoreCase);

        private ManifestValidation ValidateInputs()
        {
            foreach (var (path, stamp) in _shapeFiles)
            {
                if (FileStamp.Create(path: path) != stamp)
                {
                    return ManifestValidation.ShapeChanged(reason: $"input changed: {path}");
                }
            }

            foreach (var (directory, stamp) in _directories)
            {
                if (GetDirectoryStamp(directory: directory) != stamp)
                {
                    return ManifestValidation.ShapeChanged(reason: $"directory changed: {directory}");
                }
            }

            List<string>? changedSources = null;
            foreach (var (path, stamp) in _sourceFiles)
            {
                var current = FileStamp.Create(path: path);
                if (current == stamp)
                {
                    continue;
                }

                if (!current.Exists)
                {
                    return ManifestValidation.ShapeChanged(reason: $"source removed: {path}");
                }

                (changedSources ??= []).Add(item: path);
            }

            return changedSources is null
                ? ManifestValidation.Valid
                : ManifestValidation.SourcesChanged(changedSourceFiles: changedSources);
        }

#pragma warning disable S3776
        private ManifestValidation ValidateDirtyPaths(MetadataDirtyPathIndex dirtyPaths)
        {
            List<string>? changedSources = null;
            var checkedDirectories = new HashSet<string>(comparer: StringComparer.OrdinalIgnoreCase);
            foreach (var dirtyPath in dirtyPaths.FindRelevantPaths(sourceFiles: _sourceFiles, shapeFiles: _shapeFiles, directories: _directories))
            {
                if (_shapeFiles.TryGetValue(key: dirtyPath, value: out var shapeStamp)
                    && FileStamp.Create(path: dirtyPath) != shapeStamp)
                {
                    return ManifestValidation.ShapeChanged(reason: $"input changed: {dirtyPath}");
                }

                if (_sourceFiles.TryGetValue(key: dirtyPath, value: out var sourceStamp))
                {
                    var current = FileStamp.Create(path: dirtyPath);
                    if (current != sourceStamp)
                    {
                        if (!current.Exists)
                        {
                            return ManifestValidation.ShapeChanged(reason: $"source removed: {dirtyPath}");
                        }

                        (changedSources ??= []).Add(item: dirtyPath);
                    }
                }

                var directory = (string?)dirtyPath;
                while (!string.IsNullOrEmpty(value: directory) && checkedDirectories.Add(item: directory))
                {
                    if (_directories.TryGetValue(key: directory, value: out var stamp)
                        && GetDirectoryStamp(directory: directory) != stamp)
                    {
                        return ManifestValidation.ShapeChanged(reason: $"directory changed: {directory}");
                    }

                    directory = Path.GetDirectoryName(path: directory);
                }
            }

            return changedSources is null
                ? ManifestValidation.Valid
                : ManifestValidation.SourcesChanged(changedSourceFiles: changedSources);
        }
#pragma warning restore S3776

    }

    private sealed record CachedDocComment(DocCommentMetadata? Value);

    private sealed record ProjectMetadataBuildResult(
        ProjectMetadata LocalMetadata,
        CSharpCompilation? Compilation,
        IReadOnlyList<ProjectMetadataBuildResult> ReferencedProjects)
    {
        public IReadOnlyDictionary<string, CachedSyntaxTree> SourceTrees { get; init; } = ImmutableDictionary<string, CachedSyntaxTree>.Empty;

        public ProjectMetadataGraph MetadataGraph { get; } = new(
            localMetadata: LocalMetadata,
            references: ReferencedProjects.Select(selector: reference => reference.MetadataGraph).ToArray());

        public ProjectInputManifest? InputManifest { get; set; }

        public MetadataReference? CompilationReference { get; } = Compilation?.ToMetadataReference();
    }

    private sealed class FileAdditionalText(string path) : AdditionalText
    {
        public override string Path { get; } = path;

        public override SourceText? GetText(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
#pragma warning disable SCS0018,SEC0116
            return SourceText.From(text: File.ReadAllText(path: Path));
#pragma warning restore SCS0018,SEC0116
        }
    }

    private sealed class ProjectAnalyzerConfigOptionsProvider(IReadOnlyDictionary<string, string> globalOptions) : AnalyzerConfigOptionsProvider
    {
        private readonly AnalyzerConfigOptions _globalOptions = new ProjectAnalyzerConfigOptions(options: globalOptions);

        public override AnalyzerConfigOptions GlobalOptions => _globalOptions;

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => _globalOptions;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => _globalOptions;
    }

    private sealed class ProjectAnalyzerConfigOptions(IReadOnlyDictionary<string, string> options) : AnalyzerConfigOptions
    {
        public override IEnumerable<string> Keys => options.Keys;

        public override bool TryGetValue(
            string key,
            out string value)
        {
            if (options.TryGetValue(key: key, value: out var optionValue))
            {
                value = optionValue;
                return true;
            }

            value = string.Empty;
            return false;
        }
    }

    private sealed class AnalyzerAssemblyLoader : IAnalyzerAssemblyLoader, IDisposable
    {
        private readonly AnalyzerAssemblyLoadContext _loadContext = new();

        public void AddDependencyLocation(string fullPath)
        {
            _loadContext.AddDependencyLocation(fullPath: fullPath);
        }

        public Assembly LoadFromPath(string fullPath)
        {
            var analyzerPath = Path.GetFullPath(path: fullPath);
            AddDependencyLocation(fullPath: analyzerPath);
            return _loadContext.LoadAnalyzerAssembly(assemblyPath: analyzerPath);
        }

        public void Dispose()
        {
            _loadContext.Unload();
        }

        private sealed class AnalyzerAssemblyLoadContext : AssemblyLoadContext
        {
            private readonly ConcurrentDictionary<string, byte> _dependencyLocations = new(comparer: StringComparer.OrdinalIgnoreCase);
            private readonly ConcurrentDictionary<string, string> _referencePaths = new(comparer: StringComparer.OrdinalIgnoreCase);
            private readonly Lock _loadLock = new();

            public AnalyzerAssemblyLoadContext()
                : base(name: "Typewriter.SourceGenerators", isCollectible: true)
            {
            }

            public void AddDependencyLocation(string fullPath)
            {
                var dependencyPath = Path.GetFullPath(path: fullPath);
                _dependencyLocations.TryAdd(key: dependencyPath, value: 0);

                var referencePath = TryCreateReferencePath(path: dependencyPath);
                if (referencePath is not null)
                {
                    _referencePaths.TryAdd(key: referencePath.Value.Name, value: referencePath.Value.Path);
                }

                static (string Name, string Path)? TryCreateReferencePath(string path)
                {
                    try
                    {
                        var assemblyName = AssemblyName.GetAssemblyName(assemblyFile: path).Name;
                        return string.IsNullOrWhiteSpace(value: assemblyName)
                            ? null
                            : (assemblyName, path);
                    }
                    catch (BadImageFormatException)
                    {
                        return null;
                    }
                    catch (FileLoadException)
                    {
                        return null;
                    }
                    catch (FileNotFoundException)
                    {
                        return null;
                    }
                }
            }

            public Assembly LoadAnalyzerAssembly(string assemblyPath)
            {
                var analyzerPath = Path.GetFullPath(path: assemblyPath);
                return LoadFromAssemblyPathOnce(assemblyPath: analyzerPath, requestedName: TryGetAssemblyName(path: analyzerPath));
            }

            protected override Assembly? Load(AssemblyName assemblyName)
            {
                var sharedAssembly = AssemblyLoadContext.Default.Assemblies.FirstOrDefault(
                    predicate: assembly => AssemblyName.ReferenceMatchesDefinition(reference: assemblyName, definition: assembly.GetName()));
                if (sharedAssembly is not null)
                {
                    return sharedAssembly;
                }

                var loadedAssembly = FindLoadedAssembly(assemblyName: assemblyName);
                if (loadedAssembly is not null)
                {
                    return loadedAssembly;
                }

                if (assemblyName.Name is not null
                    && _referencePaths.TryGetValue(key: assemblyName.Name, value: out var referencePath))
                {
                    return LoadFromAssemblyPathOnce(assemblyPath: referencePath, requestedName: assemblyName);
                }

                return LoadFromDependencyDirectory(assemblyName: assemblyName);
            }

            private static AssemblyName? TryGetAssemblyName(string path)
            {
                try
                {
                    return AssemblyName.GetAssemblyName(assemblyFile: path);
                }
                catch (BadImageFormatException)
                {
                    return null;
                }
                catch (FileLoadException)
                {
                    return null;
                }
                catch (FileNotFoundException)
                {
                    return null;
                }
            }

            private Assembly? LoadFromDependencyDirectory(AssemblyName assemblyName)
            {
                if (assemblyName.Name is null)
                {
                    return null;
                }

                var assemblyFileName = assemblyName.Name + ".dll";
                foreach (var dependencyLocation in _dependencyLocations.Keys.ToArray())
                {
                    var directory = Path.GetDirectoryName(path: dependencyLocation);
                    if (string.IsNullOrWhiteSpace(value: directory))
                    {
                        continue;
                    }

                    var candidate = Path.Combine(path1: directory, path2: assemblyFileName);
                    if (File.Exists(path: candidate))
                    {
                        return LoadFromAssemblyPathOnce(assemblyPath: candidate, requestedName: assemblyName);
                    }
                }

                return null;
            }

            private Assembly LoadFromAssemblyPathOnce(
                string assemblyPath,
                AssemblyName? requestedName)
            {
                var fullPath = Path.GetFullPath(path: assemblyPath);
                var candidateName = TryGetAssemblyName(path: fullPath) ?? requestedName;
                lock (_loadLock)
                {
                    var loadedAssembly = candidateName is null
                        ? FindLoadedAssemblyByPath(assemblyPath: fullPath)
                        : FindLoadedAssembly(assemblyName: candidateName);
                    if (loadedAssembly is not null)
                    {
                        return loadedAssembly;
                    }

                    try
                    {
#pragma warning disable SCS0018
                        return LoadFromAssemblyPath(assemblyPath: fullPath);
#pragma warning restore SCS0018
                    }
                    catch (FileLoadException) when (candidateName is not null && FindLoadedAssembly(assemblyName: candidateName) is not null)
                    {
                        return FindLoadedAssembly(assemblyName: candidateName)!;
                    }
                    catch (FileLoadException) when (FindLoadedAssemblyByPath(assemblyPath: fullPath) is not null)
                    {
                        return FindLoadedAssemblyByPath(assemblyPath: fullPath)!;
                    }
                }
            }

            private Assembly? FindLoadedAssembly(AssemblyName assemblyName) =>
                Assemblies.FirstOrDefault(
                    predicate: assembly => AssemblyName.ReferenceMatchesDefinition(reference: assemblyName, definition: assembly.GetName()));

            private Assembly? FindLoadedAssemblyByPath(string assemblyPath)
            {
                var fullPath = Path.GetFullPath(path: assemblyPath);
                return Assemblies.FirstOrDefault(
                    predicate: assembly => assembly.Location.Length > 0
                                           && string.Equals(a: Path.GetFullPath(path: assembly.Location), b: fullPath, comparisonType: StringComparison.OrdinalIgnoreCase));
            }
        }
    }
}
