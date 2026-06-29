using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Typewriter.Abstractions;
using Typewriter.Buildalyzer;

namespace Typewriter.Roslyn;

public sealed class CSharpProjectMetadataProvider : IProjectMetadataProvider
{
    private static readonly ConcurrentDictionary<ProjectMetadataCacheKey, ProjectMetadataCacheEntry> MetadataCache = [];
    private static readonly ConcurrentBag<HashSet<ITypeSymbol>> TypeReferenceVisitedSymbolSets = [];
    private static readonly ConditionalWeakTable<ISymbol, CachedDocComment> DocCommentCache = [];
    private readonly IProjectWorkspaceLoader _projectLoader;

    public CSharpProjectMetadataProvider()
        : this(projectLoader: new MsBuildProjectLoader())
    {
    }

    public CSharpProjectMetadataProvider(IProjectWorkspaceLoader projectLoader)
    {
        _projectLoader = projectLoader;
    }

    public Task<ProjectMetadata> GetMetadataAsync(
        ProjectContext project,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(argument: project);

        var loadedProjects = new Dictionary<string, ProjectMetadataBuildResult>(comparer: StringComparer.OrdinalIgnoreCase);
        var loadingProjects = new HashSet<string>(comparer: StringComparer.OrdinalIgnoreCase);
        return GetMergedMetadataAsync(
            projectLoader: _projectLoader,
            project: project,
            loadedProjects: loadedProjects,
            loadingProjects: loadingProjects,
            cancellationToken: cancellationToken);
    }

    private static async Task<ProjectMetadata> GetMergedMetadataAsync(
        IProjectWorkspaceLoader projectLoader,
        ProjectContext project,
        IDictionary<string, ProjectMetadataBuildResult> loadedProjects,
        ISet<string> loadingProjects,
        CancellationToken cancellationToken)
    {
        var result = await GetMetadataCoreAsync(
            projectLoader: projectLoader,
            project: project,
            loadedProjects: loadedProjects,
            loadingProjects: loadingProjects,
            cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        return result.Metadata;
    }

#pragma warning disable MA0051 // Method is too long
    private static async Task<ProjectMetadataBuildResult> GetMetadataCoreAsync(
        IProjectWorkspaceLoader projectLoader,
        ProjectContext project,
        IDictionary<string, ProjectMetadataBuildResult> loadedProjects,
        ISet<string> loadingProjects,
        CancellationToken cancellationToken)
#pragma warning restore MA0051 // Method is too long
    {
        var projectPath = Path.GetFullPath(path: project.ProjectPath);
        if (loadedProjects.TryGetValue(key: projectPath, value: out var cachedResult))
        {
            return cachedResult;
        }

        var metadataCacheKey = new ProjectMetadataCacheKey(
            ProjectPath: projectPath,
            WorkspacePath: Path.GetFullPath(path: project.WorkspacePath),
            TargetFramework: project.TargetFramework ?? string.Empty,
            RunFullDiagnostics: project.RunFullDiagnostics);
        if (TryGetCachedResult(cacheKey: metadataCacheKey, result: out cachedResult))
        {
            loadedProjects[key: projectPath] = cachedResult;
            return cachedResult;
        }

        if (!loadingProjects.Add(item: projectPath))
        {
            return new ProjectMetadataBuildResult(
                Metadata: CreateEmptyProjectMetadata(projectPath: projectPath),
                Compilation: null,
                CompilationReferences: []);
        }

        if (!File.Exists(path: projectPath))
        {
            return CacheResult(
                projectPath: projectPath,
                result: new ProjectMetadataBuildResult(
                    Metadata: CreateProjectFileMissingMetadata(projectPath: projectPath),
                    Compilation: null,
                    CompilationReferences: []),
                loadedProjects: loadedProjects,
                loadingProjects: loadingProjects);
        }

        var loadedProject = await projectLoader.LoadAsync(project: project, cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        if (loadedProject.Diagnostics.Any(predicate: diagnostic => diagnostic.Severity == Typewriter.Abstractions.DiagnosticSeverity.Error))
        {
            return CacheResult(
                projectPath: projectPath,
                result: new ProjectMetadataBuildResult(
                    Metadata: CreateProjectLoadFailedMetadata(projectPath: projectPath, diagnostics: loadedProject.Diagnostics),
                    Compilation: null,
                    CompilationReferences: []),
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
                        TargetFramework: project.TargetFramework ?? loadedProject.TargetFramework),
                    loadedProjects: loadedProjects,
                    loadingProjects: loadingProjects,
                    cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false));
        }

        var projectReferences = referencedProjects
            .SelectMany(selector: reference => reference.CompilationReferences)
            .GroupBy(
                keySelector: reference => Path.GetFullPath(path: reference.ProjectPath),
                comparer: StringComparer.OrdinalIgnoreCase)
            .Select(selector: group => group.First())
            .Select(selector: reference => reference.Compilation.ToMetadataReference())
            .ToArray();
        var currentProject = await CreateCurrentProjectMetadataAsync(
            projectPath: projectPath,
            loadedProject: loadedProject,
            projectReferences: projectReferences,
            runFullDiagnostics: project.RunFullDiagnostics,
            cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        var result = new ProjectMetadataBuildResult(
            Metadata: MergeProjectMetadata(
                project: currentProject.Metadata,
                referencedProjects: referencedProjects.Select(selector: reference => reference.Metadata).ToArray()),
            Compilation: currentProject.Compilation,
            CompilationReferences: CreateCompilationReferences(
                projectPath: projectPath,
                compilation: currentProject.Compilation,
                referencedProjects: referencedProjects));
        AddCachedResult(cacheKey: metadataCacheKey, result: result);
        return CacheResult(
            projectPath: projectPath,
            result: result,
            loadedProjects: loadedProjects,
            loadingProjects: loadingProjects);
    }

#pragma warning disable MA0051 // Method is too long
    private static async Task<ProjectMetadataBuildResult> CreateCurrentProjectMetadataAsync(
        string projectPath,
        ProjectLoadResult loadedProject,
        IReadOnlyList<MetadataReference> projectReferences,
        bool runFullDiagnostics,
        CancellationToken cancellationToken)
#pragma warning restore MA0051 // Method is too long
    {
        var sourcePaths = loadedProject.SourceFiles.ToArray();
        var parseOptions = CSharpParseOptions.Default
            .WithLanguageVersion(version: LanguageVersion.Preview)
            .WithPreprocessorSymbols(preprocessorSymbols: loadedProject.PreprocessorSymbols);
        var syntaxTrees = new List<SyntaxTree>(capacity: sourcePaths.Length);
        foreach (var sourcePath in sourcePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
#pragma warning disable SCS0018 // Potential Path Traversal vulnerability was found where '{0}' in '{1}' may be tainted by user-controlled data from '{2}' in method '{3}'.
            var sourceText = await File.ReadAllTextAsync(path: sourcePath, cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
#pragma warning restore SCS0018 // Potential Path Traversal vulnerability was found where '{0}' in '{1}' may be tainted by user-controlled data from '{2}' in method '{3}'.
            syntaxTrees.Add(
                item: CSharpSyntaxTree.ParseText(
                    text: sourceText,
                    options: parseOptions,
                    path: sourcePath,
                    cancellationToken: cancellationToken));
        }

        syntaxTrees.Add(item: CreateDefaultGlobalUsingsTree(globalUsings: loadedProject.GlobalUsings, parseOptions: parseOptions, cancellationToken: cancellationToken));

        var compilation = CSharpCompilation.Create(
            assemblyName: Path.GetFileNameWithoutExtension(path: projectPath),
            syntaxTrees: syntaxTrees,
            references: CreateReferences(referencePaths: loadedProject.ReferencePaths).Concat(second: projectReferences),
            options: new CSharpCompilationOptions(
                outputKind: OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        compilation = RunSourceGenerators(
            compilation: compilation,
            loadedProject: loadedProject,
            parseOptions: parseOptions,
            cancellationToken: cancellationToken,
            diagnostics: out var generatorDiagnostics);
        var diagnostics = GetCompilationDiagnostics(
                compilation: compilation,
                generatorDiagnostics: generatorDiagnostics,
                runFullDiagnostics: runFullDiagnostics,
                cancellationToken: cancellationToken)
            .Where(predicate: diagnostic => diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
            .Select(selector: ToGenerationDiagnostic)
            .ToArray();

        var sourceSymbols = GetSourceSymbols(compilation: compilation, syntaxTrees: syntaxTrees, cancellationToken: cancellationToken);
        var symbolMetadata = sourceSymbols.Types
            .Select(
                selector: symbol => new
                {
                    Symbol = symbol,
                    HasMetadata = TryCreateType(symbol: symbol, compilation: compilation, metadata: out var metadata),
                    Metadata = metadata,
                })
            .Where(predicate: item => item.HasMetadata)
            .ToArray();
        var delegateMetadata = sourceSymbols.Delegates
            .Select(
                selector: symbol => new
                {
                    Symbol = symbol,
                    HasMetadata = TryCreateDelegate(symbol: symbol, compilation: compilation, metadata: out var metadata),
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

        return new ProjectMetadataBuildResult(
            Metadata: metadata,
            Compilation: compilation,
            CompilationReferences:
            [
                new ProjectCompilationReference(ProjectPath: projectPath, Compilation: compilation),
            ]);
    }

    private static IReadOnlyList<ProjectCompilationReference> CreateCompilationReferences(
        string projectPath,
        CSharpCompilation? compilation,
        IEnumerable<ProjectMetadataBuildResult> referencedProjects)
    {
        var references = new List<ProjectCompilationReference>();
        var seenProjectPaths = new HashSet<string>(comparer: StringComparer.OrdinalIgnoreCase);

        if (compilation is not null)
        {
            AddReference(reference: new ProjectCompilationReference(ProjectPath: projectPath, Compilation: compilation));
        }

        foreach (var reference in referencedProjects.SelectMany(selector: referencedProject => referencedProject.CompilationReferences))
        {
            AddReference(reference: reference);
        }

        return references;

        void AddReference(ProjectCompilationReference reference)
        {
            var referenceProjectPath = Path.GetFullPath(path: reference.ProjectPath);
            if (seenProjectPaths.Add(item: referenceProjectPath))
            {
                references.Add(item: new ProjectCompilationReference(ProjectPath: referenceProjectPath, Compilation: reference.Compilation));
            }
        }
    }

    private static bool TryGetCachedResult(
        ProjectMetadataCacheKey cacheKey,
        out ProjectMetadataBuildResult result)
    {
        if (MetadataCache.TryGetValue(key: cacheKey, value: out var entry)
            && entry.Fingerprint.IsCurrent())
        {
            result = entry.Result;
            return true;
        }

        _ = MetadataCache.TryRemove(key: cacheKey, value: out _);
        result = null!;
        return false;
    }

    private static void AddCachedResult(
        ProjectMetadataCacheKey cacheKey,
        ProjectMetadataBuildResult result)
    {
        if (result.Metadata.Diagnostics.Any(predicate: diagnostic => diagnostic.Severity == Typewriter.Abstractions.DiagnosticSeverity.Error))
        {
            return;
        }

        MetadataCache[key: cacheKey] = new ProjectMetadataCacheEntry(
            Result: result,
            Fingerprint: ProjectMetadataFingerprint.Create(result: result));
        TrimMetadataCache();
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

    private static ProjectMetadata MergeProjectMetadata(
        ProjectMetadata project,
        IReadOnlyList<ProjectMetadata> referencedProjects)
    {
        if (referencedProjects.Count == 0)
        {
            return project;
        }

        var sourceFiles = project.SourceFiles
            .Concat(second: referencedProjects.SelectMany(selector: reference => reference.SourceFiles))
            .GroupBy(keySelector: sourceFile => Path.GetFullPath(path: sourceFile.Path), comparer: StringComparer.OrdinalIgnoreCase)
            .Select(selector: group => group.First())
            .OrderBy(keySelector: sourceFile => sourceFile.Path, comparer: StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var types = project.Types
            .Concat(second: referencedProjects.SelectMany(selector: reference => reference.Types))
            .GroupBy(keySelector: GetMetadataIdentity, comparer: StringComparer.Ordinal)
            .Select(selector: group => group.First())
            .OrderBy(keySelector: type => type.FullName, comparer: StringComparer.Ordinal)
            .ThenBy(keySelector: type => type.AssemblyName, comparer: StringComparer.Ordinal)
            .ToArray();
        var delegates = project.Delegates
            .Concat(second: referencedProjects.SelectMany(selector: reference => reference.Delegates))
            .GroupBy(keySelector: GetMetadataIdentity, comparer: StringComparer.Ordinal)
            .Select(selector: group => group.First())
            .OrderBy(keySelector: type => type.FullName, comparer: StringComparer.Ordinal)
            .ThenBy(keySelector: type => type.AssemblyName, comparer: StringComparer.Ordinal)
            .ToArray();

        return new ProjectMetadata(
            ProjectPath: project.ProjectPath,
            SourceFiles: sourceFiles,
            Types: types,
            Diagnostics: project.Diagnostics.Concat(second: referencedProjects.SelectMany(selector: reference => reference.Diagnostics)).ToArray())
        {
            Delegates = delegates,
        };
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

    private static string GetMetadataIdentity(TypeMetadata type) =>
        string.Concat(str0: type.FullName, str1: "\u001F", str2: type.AssemblyName);

    private static string GetMetadataIdentity(DelegateMetadata type) =>
        string.Concat(str0: type.FullName, str1: "\u001F", str2: type.AssemblyName);

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
            return projectReferences.Select(selector: path => MetadataReference.CreateFromFile(path: path));
        }

        var trustedAssemblies = (string?)AppContext.GetData(name: "TRUSTED_PLATFORM_ASSEMBLIES");
        if (string.IsNullOrWhiteSpace(value: trustedAssemblies))
        {
            return [];
        }

        return trustedAssemblies
            .Split(separator: Path.PathSeparator, options: StringSplitOptions.RemoveEmptyEntries)
            .Distinct(comparer: StringComparer.OrdinalIgnoreCase)
            .Select(selector: path => MetadataReference.CreateFromFile(path: path));
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

    private static SourceSymbols GetSourceSymbols(
        CSharpCompilation compilation,
        IEnumerable<SyntaxTree> syntaxTrees,
        CancellationToken cancellationToken)
    {
        var typesByKey = new Dictionary<string, INamedTypeSymbol>(comparer: StringComparer.Ordinal);
        var delegatesByKey = new Dictionary<string, INamedTypeSymbol>(comparer: StringComparer.Ordinal);
        foreach (var syntaxTree in syntaxTrees)
        {
            var semanticModel = compilation.GetSemanticModel(syntaxTree: syntaxTree);
            var root = syntaxTree.GetRoot(cancellationToken: cancellationToken);
            foreach (var declaration in root.DescendantNodes(
                         descendIntoChildren: static node => node is CompilationUnitSyntax or BaseNamespaceDeclarationSyntax or BaseTypeDeclarationSyntax))
            {
                switch (declaration)
                {
                    case BaseTypeDeclarationSyntax typeDeclaration
                        when semanticModel.GetDeclaredSymbol(declarationSyntax: typeDeclaration, cancellationToken: cancellationToken) is INamedTypeSymbol type:
                        AddSymbol(symbolsByKey: typesByKey, symbol: type);
                        break;
                    case DelegateDeclarationSyntax delegateDeclaration
                        when semanticModel.GetDeclaredSymbol(declarationSyntax: delegateDeclaration, cancellationToken: cancellationToken) is INamedTypeSymbol { ContainingType: null } @delegate:
                        AddSymbol(symbolsByKey: delegatesByKey, symbol: @delegate);
                        break;
                    default:
                        break;
                }
            }
        }

        return new SourceSymbols(
            Types: typesByKey.Values.ToArray(),
            Delegates: delegatesByKey.Values.ToArray());
    }

    private static void AddSymbol(
        IDictionary<string, INamedTypeSymbol> symbolsByKey,
        INamedTypeSymbol symbol)
    {
        var key = GetFullName(symbol: symbol) + "`" + symbol.Arity.ToString(provider: CultureInfo.InvariantCulture);
        if (!symbolsByKey.ContainsKey(key: key))
        {
            symbolsByKey[key: key] = symbol;
        }
    }

#pragma warning disable MA0051 // Method is too long
    private static bool TryCreateType(
        INamedTypeSymbol symbol,
        Compilation compilation,
        out TypeMetadata metadata)
#pragma warning restore MA0051 // Method is too long
    {
        metadata = null!;
        if (symbol.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Internal)
            || symbol.IsImplicitlyDeclared)
        {
            return false;
        }

        var kind = symbol.TypeKind switch
        {
            Microsoft.CodeAnalysis.TypeKind.Class when symbol.IsRecord => TypeMetadataKind.Record,
            Microsoft.CodeAnalysis.TypeKind.Class => TypeMetadataKind.Class,
            Microsoft.CodeAnalysis.TypeKind.Struct => TypeMetadataKind.Struct,
            Microsoft.CodeAnalysis.TypeKind.Interface => TypeMetadataKind.Interface,
            Microsoft.CodeAnalysis.TypeKind.Enum => TypeMetadataKind.Enum,
            _ => (TypeMetadataKind?)null,
        };

        if (kind is null)
        {
            return false;
        }

        var docComment = GetDocComment(symbol: symbol);
        var members = symbol.GetMembers();
        var typeMembers = symbol.GetTypeMembers();
        var nestedTypes = GetNestedTypes(members: typeMembers, compilation: compilation).ToArray();
        metadata = new TypeMetadata(
            Name: symbol.Name,
            FullName: GetFullName(symbol: symbol),
            Namespace: GetNamespace(symbol: symbol),
            Kind: kind.Value,
            Accessibility: MapAccessibility(accessibility: symbol.DeclaredAccessibility),
            Properties: GetProperties(symbol: symbol, members: members).ToArray(),
            Attributes: GetAttributes(attributes: symbol.GetAttributes()).ToArray(),
            BaseTypes: GetBaseTypes(symbol: symbol).ToArray(),
            EnumValues: GetEnumValues(symbol: symbol).ToArray(),
            IsNullableAware: IsNullableAware(symbol: symbol, compilation: compilation))
        {
            Location = GetSourceLocation(symbol: symbol),
            FileLocations = symbol.Locations
                .Where(predicate: location => location.IsInSource)
                .Select(selector: location => location.GetLineSpan().Path)
                .Where(predicate: path => !string.IsNullOrWhiteSpace(value: path))
                .Select(selector: Path.GetFullPath)
                .Distinct(comparer: StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            Documentation = docComment?.Summary,
            DocComment = docComment,
            AssemblyName = GetAssemblyName(symbol: symbol),
            IsStatic = symbol.IsStatic,
            IsAbstract = symbol.IsAbstract,
            ContainingTypeFullName = symbol.ContainingType is null ? string.Empty : GetFullName(symbol: symbol.ContainingType),
            Methods = GetMethods(symbol: symbol, members: members).ToArray(),
            Constants = GetConstants(symbol: symbol, members: members).ToArray(),
            Fields = GetFields(symbol: symbol, members: members).ToArray(),
            StaticReadOnlyFields = GetStaticReadOnlyFields(symbol: symbol, members: members).ToArray(),
            Events = GetEvents(symbol: symbol, members: members).ToArray(),
            Delegates = GetDelegates(members: typeMembers, compilation: compilation).ToArray(),
            TypeParameters = GetTypeParameters(symbols: symbol.TypeParameters).ToArray(),
            TypeArguments = symbol.TypeArguments
                .Zip(
                    second: symbol.TypeArgumentNullableAnnotations,
                    resultSelector: static (argument, annotation) => CreateTypeReference(symbol: argument, nullableAnnotation: annotation))
                .ToArray(),
            NestedClasses = nestedTypes.Where(predicate: type => type.Kind == TypeMetadataKind.Class).ToArray(),
            NestedRecords = nestedTypes.Where(predicate: type => type.Kind == TypeMetadataKind.Record).ToArray(),
            NestedStructs = nestedTypes.Where(predicate: type => type.Kind == TypeMetadataKind.Struct).ToArray(),
            NestedEnums = nestedTypes.Where(predicate: type => type.Kind == TypeMetadataKind.Enum).ToArray(),
            NestedInterfaces = nestedTypes.Where(predicate: type => type.Kind == TypeMetadataKind.Interface).ToArray(),
        };
        return true;
    }

    private static IEnumerable<PropertyMetadata> GetProperties(
        INamedTypeSymbol symbol,
        IEnumerable<ISymbol> members)
    {
        return members.OfType<IPropertySymbol>()
            .Where(predicate: property => !property.IsStatic)
            .Where(predicate: property => property.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal)
            .Where(predicate: property => property.GetMethod is not null)
            .Select(
                selector: property =>
                {
                    var fullName = property.ToDisplayString(format: SymbolDisplayFormat.CSharpErrorMessageFormat);
                    var docComment = GetDocComment(symbol: property);
                    return new PropertyMetadata(
                        Name: property.Name,
                        FullName: fullName,
                        Type: CreateTypeReference(symbol: property.Type, nullableAnnotation: property.NullableAnnotation),
                        Accessibility: MapAccessibility(accessibility: property.DeclaredAccessibility),
                        HasGetter: property.GetMethod is not null,
                        HasSetter: property.SetMethod is not null,
                        IsRequired: property.IsRequired,
                        Attributes: GetAttributes(attributes: property.GetAttributes()).ToArray())
                    {
                        ParentTypeFullName = GetFullName(symbol: symbol),
                        AssemblyName = GetAssemblyName(symbol: property),
                        Location = GetSourceLocation(symbol: property),
                        Documentation = docComment?.Summary,
                        DocComment = docComment,
                        IsAbstract = property.IsAbstract,
                        IsIndexer = property.IsIndexer,
                        IsVirtual = property.IsVirtual,
                        Parameters = GetParameters(parameters: property.Parameters, parentFullName: fullName, parentPropertyFullName: fullName).ToArray(),
                        Value = GetPropertyInitializerValue(property: property),
                    };
                });
    }

    private static IEnumerable<MethodMetadata> GetMethods(
        INamedTypeSymbol symbol,
        IEnumerable<ISymbol> members)
    {
        return members.OfType<IMethodSymbol>()
            .Where(predicate: method => method.MethodKind == MethodKind.Ordinary)
            .Where(predicate: method => !method.IsImplicitlyDeclared)
            .Where(predicate: method => method.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal)
            .Select(
                selector: method =>
                {
                    var fullName = method.ToDisplayString(format: SymbolDisplayFormat.CSharpErrorMessageFormat);
                    var docComment = GetDocComment(symbol: method);
                    return new MethodMetadata(
                        Name: method.Name,
                        FullName: fullName,
                        ReturnType: CreateTypeReference(symbol: method.ReturnType, nullableAnnotation: method.ReturnNullableAnnotation),
                        Accessibility: MapAccessibility(accessibility: method.DeclaredAccessibility),
                        IsStatic: method.IsStatic,
                        IsAbstract: method.IsAbstract,
                        IsGeneric: method.IsGenericMethod,
                        Parameters: GetParameters(method: method, methodFullName: fullName).ToArray(),
                        Attributes: GetAttributes(attributes: method.GetAttributes()).ToArray(),
                        ParentTypeFullName: GetFullName(symbol: symbol))
                    {
                        AssemblyName = GetAssemblyName(symbol: method),
                        Location = GetSourceLocation(symbol: method),
                        Documentation = docComment?.Summary,
                        DocComment = docComment,
                        TypeParameters = GetTypeParameters(symbols: method.TypeParameters).ToArray(),
                    };
                });
    }

    private static IEnumerable<ParameterMetadata> GetParameters(
        IMethodSymbol method,
        string methodFullName) =>
        GetParameters(parameters: method.Parameters, parentFullName: methodFullName, parentMethodFullName: methodFullName);

    private static IEnumerable<ParameterMetadata> GetParameters(
        IEnumerable<IParameterSymbol> parameters,
        string parentFullName,
        string parentMethodFullName = "",
        string parentPropertyFullName = "")
    {
        return parameters.Select(
            selector: parameter =>
            {
                var defaultValue = parameter.HasExplicitDefaultValue
                    ? FormatConstantValue(value: parameter.ExplicitDefaultValue)
                    : null;
                var docComment = GetDocComment(symbol: parameter);

                return new ParameterMetadata(
                    Name: parameter.Name,
                    FullName: parentFullName + "." + parameter.Name,
                    Type: CreateTypeReference(symbol: parameter.Type, nullableAnnotation: parameter.NullableAnnotation),
                    HasDefaultValue: parameter.HasExplicitDefaultValue,
                    DefaultValue: defaultValue,
                    Attributes: GetAttributes(attributes: parameter.GetAttributes()).ToArray(),
                    ParentMethodFullName: parentMethodFullName)
                {
                    AssemblyName = GetAssemblyName(symbol: parameter),
                    Location = GetSourceLocation(symbol: parameter),
                    Documentation = docComment?.Summary,
                    DocComment = docComment,
                    ParentPropertyFullName = parentPropertyFullName,
                };
            });
    }

    private static IEnumerable<ConstantMetadata> GetConstants(
        INamedTypeSymbol symbol,
        IEnumerable<ISymbol> members)
    {
        if (symbol.TypeKind == Microsoft.CodeAnalysis.TypeKind.Enum)
        {
            return [];
        }

        return members.OfType<IFieldSymbol>()
            .Where(predicate: field => field.HasConstantValue)
            .Where(predicate: field => field.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal)
            .Select(
                selector: field =>
                {
                    var docComment = GetDocComment(symbol: field);
                    return new ConstantMetadata(
                        Name: field.Name,
                        FullName: field.ToDisplayString(format: SymbolDisplayFormat.CSharpErrorMessageFormat),
                        Accessibility: MapAccessibility(accessibility: field.DeclaredAccessibility),
                        Type: CreateTypeReference(symbol: field.Type, nullableAnnotation: field.NullableAnnotation),
                        Value: FormatConstantValue(value: field.ConstantValue),
                        Attributes: GetAttributes(attributes: field.GetAttributes()).ToArray(),
                        ParentTypeFullName: GetFullName(symbol: symbol))
                    {
                        AssemblyName = GetAssemblyName(symbol: field),
                        Location = GetSourceLocation(symbol: field),
                        Documentation = docComment?.Summary,
                        DocComment = docComment,
                    };
                });
    }

    private static IEnumerable<FieldMetadata> GetFields(
        INamedTypeSymbol symbol,
        IEnumerable<ISymbol> members)
    {
        if (symbol.TypeKind == Microsoft.CodeAnalysis.TypeKind.Enum)
        {
            return [];
        }

        return members.OfType<IFieldSymbol>()
            .Where(predicate: field => !field.IsImplicitlyDeclared)
            .Where(predicate: field => !field.IsConst)
            .Where(predicate: field => !field.IsStatic)
            .Where(predicate: field => field.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal)
            .Select(
                selector: field =>
                {
                    var docComment = GetDocComment(symbol: field);
                    return new FieldMetadata(
                        Name: field.Name,
                        FullName: field.ToDisplayString(format: SymbolDisplayFormat.CSharpErrorMessageFormat),
                        Accessibility: MapAccessibility(accessibility: field.DeclaredAccessibility),
                        Type: CreateTypeReference(symbol: field.Type, nullableAnnotation: field.NullableAnnotation),
                        Attributes: GetAttributes(attributes: field.GetAttributes()).ToArray(),
                        ParentTypeFullName: GetFullName(symbol: symbol))
                    {
                        AssemblyName = GetAssemblyName(symbol: field),
                        Location = GetSourceLocation(symbol: field),
                        Documentation = docComment?.Summary,
                        DocComment = docComment,
                        Value = GetFieldInitializerValue(field: field),
                    };
                });
    }

    private static IEnumerable<StaticReadOnlyFieldMetadata> GetStaticReadOnlyFields(
        INamedTypeSymbol symbol,
        IEnumerable<ISymbol> members)
    {
        if (symbol.TypeKind == Microsoft.CodeAnalysis.TypeKind.Enum)
        {
            return [];
        }

        return members.OfType<IFieldSymbol>()
            .Where(predicate: field => !field.IsImplicitlyDeclared)
            .Where(predicate: field => field.IsStatic)
            .Where(predicate: field => field.IsReadOnly)
            .Where(predicate: field => !field.IsConst)
            .Where(predicate: field => field.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal)
            .Select(
                selector: field =>
                {
                    var docComment = GetDocComment(symbol: field);
                    return new StaticReadOnlyFieldMetadata(
                        Name: field.Name,
                        FullName: field.ToDisplayString(format: SymbolDisplayFormat.CSharpErrorMessageFormat),
                        Accessibility: MapAccessibility(accessibility: field.DeclaredAccessibility),
                        Type: CreateTypeReference(symbol: field.Type, nullableAnnotation: field.NullableAnnotation),
                        Value: GetStaticReadOnlyFieldValue(field: field),
                        Attributes: GetAttributes(attributes: field.GetAttributes()).ToArray(),
                        ParentTypeFullName: GetFullName(symbol: symbol))
                    {
                        AssemblyName = GetAssemblyName(symbol: field),
                        Location = GetSourceLocation(symbol: field),
                        Documentation = docComment?.Summary,
                        DocComment = docComment,
                    };
                });
    }

    private static IEnumerable<EventMetadata> GetEvents(
        INamedTypeSymbol symbol,
        IEnumerable<ISymbol> members)
    {
        return members.OfType<IEventSymbol>()
            .Where(predicate: @event => !@event.IsImplicitlyDeclared)
            .Where(predicate: @event => @event.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal)
            .Select(
                selector: @event =>
                {
                    var docComment = GetDocComment(symbol: @event);
                    return new EventMetadata(
                        Name: @event.Name,
                        FullName: @event.ToDisplayString(format: SymbolDisplayFormat.CSharpErrorMessageFormat),
                        Accessibility: MapAccessibility(accessibility: @event.DeclaredAccessibility),
                        Type: CreateTypeReference(symbol: @event.Type, nullableAnnotation: @event.NullableAnnotation),
                        Attributes: GetAttributes(attributes: @event.GetAttributes()).ToArray(),
                        ParentTypeFullName: GetFullName(symbol: symbol))
                    {
                        AssemblyName = GetAssemblyName(symbol: @event),
                        Location = GetSourceLocation(symbol: @event),
                        Documentation = docComment?.Summary,
                        DocComment = docComment,
                    };
                });
    }

    private static IEnumerable<DelegateMetadata> GetDelegates(
        IEnumerable<INamedTypeSymbol> members,
        Compilation compilation)
    {
        return members
            .Where(predicate: member => member.TypeKind == Microsoft.CodeAnalysis.TypeKind.Delegate)
            .Select(
                selector: member => new
                {
                    HasMetadata = TryCreateDelegate(symbol: member, compilation: compilation, metadata: out var metadata),
                    Metadata = metadata,
                })
            .Where(predicate: item => item.HasMetadata)
            .Select(selector: item => item.Metadata);
    }

    private static IEnumerable<TypeMetadata> GetNestedTypes(
        IEnumerable<INamedTypeSymbol> members,
        Compilation compilation)
    {
        return members
            .Select(
                selector: member => new
                {
                    HasMetadata = TryCreateType(symbol: member, compilation: compilation, metadata: out var metadata),
                    Metadata = metadata,
                })
            .Where(predicate: item => item.HasMetadata)
            .Select(selector: item => item.Metadata);
    }

    private static bool TryCreateDelegate(
        INamedTypeSymbol symbol,
        Compilation compilation,
        out DelegateMetadata metadata)
    {
        metadata = null!;
        if (symbol.TypeKind != Microsoft.CodeAnalysis.TypeKind.Delegate
            || symbol.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Internal)
            || symbol.IsImplicitlyDeclared)
        {
            return false;
        }

        var invokeMethod = symbol.DelegateInvokeMethod;
        var fullName = GetFullName(symbol: symbol);
        var docComment = GetDocComment(symbol: symbol);
        var returnType = invokeMethod is null
            ? CreateVoidTypeReference(compilation: compilation)
            : CreateTypeReference(symbol: invokeMethod.ReturnType, nullableAnnotation: invokeMethod.ReturnNullableAnnotation);
        metadata = new DelegateMetadata(
            Name: symbol.Name,
            FullName: fullName,
            Accessibility: MapAccessibility(accessibility: symbol.DeclaredAccessibility),
            ReturnType: returnType,
            IsGeneric: symbol.IsGenericType,
            Parameters: invokeMethod is null ? [] : GetParameters(method: invokeMethod, methodFullName: fullName).ToArray(),
            Attributes: GetAttributes(attributes: symbol.GetAttributes()).ToArray(),
            ParentTypeFullName: symbol.ContainingType is null ? string.Empty : GetFullName(symbol: symbol.ContainingType))
        {
            AssemblyName = GetAssemblyName(symbol: symbol),
            Location = GetSourceLocation(symbol: symbol),
            Documentation = docComment?.Summary,
            DocComment = docComment,
            TypeParameters = GetTypeParameters(symbols: symbol.TypeParameters).ToArray(),
        };
        return true;
    }

    private static IEnumerable<TypeMetadataReference> GetBaseTypes(INamedTypeSymbol symbol)
    {
        if (symbol.BaseType is not null
            && symbol.BaseType.SpecialType is not SpecialType.System_Object
                and not SpecialType.System_ValueType
                and not SpecialType.System_Enum)
        {
            yield return CreateTypeReference(symbol: symbol.BaseType, nullableAnnotation: NullableAnnotation.NotAnnotated);
        }

        foreach (var interfaceSymbol in symbol.Interfaces)
        {
            yield return CreateTypeReference(symbol: interfaceSymbol, nullableAnnotation: NullableAnnotation.NotAnnotated);
        }
    }

    private static IEnumerable<EnumValueMetadata> GetEnumValues(INamedTypeSymbol symbol)
    {
        if (symbol.TypeKind != Microsoft.CodeAnalysis.TypeKind.Enum)
        {
            return [];
        }

        return symbol.GetMembers()
            .OfType<IFieldSymbol>()
            .Where(predicate: field => field.HasConstantValue)
            .Select(
                selector: field =>
                {
                    var docComment = GetDocComment(symbol: field);
                    return new EnumValueMetadata(
                        Name: field.Name,
                        Value: Convert.ToInt64(value: field.ConstantValue, provider: CultureInfo.InvariantCulture))
                    {
                        AssemblyName = GetAssemblyName(symbol: field),
                        Attributes = GetAttributes(attributes: field.GetAttributes()).ToArray(),
                        ParentTypeFullName = GetFullName(symbol: symbol),
                        Location = GetSourceLocation(symbol: field),
                        Documentation = docComment?.Summary,
                        DocComment = docComment,
                    };
                });
    }

    private static IEnumerable<AttributeMetadata> GetAttributes(IEnumerable<AttributeData> attributes)
    {
        return attributes.Select(
            selector: attribute =>
            {
                var attributeType = attribute.AttributeClass is null
                    ? null
                    : CreateTypeReference(symbol: attribute.AttributeClass, nullableAnnotation: NullableAnnotation.NotAnnotated);
                return new AttributeMetadata(
                    Name: TrimAttributeSuffix(name: attribute.AttributeClass?.Name ?? string.Empty),
                    FullName: GetFullName(symbol: attribute.AttributeClass),
                    Arguments: GetAttributeArguments(attribute: attribute).ToArray())
                {
                    AssemblyName = GetAssemblyName(symbol: attribute.AttributeClass),
                    Type = attributeType,
                };
            });
    }

    private static IEnumerable<AttributeArgumentMetadata> GetAttributeArguments(AttributeData attribute)
    {
        foreach (var argument in attribute.ConstructorArguments)
        {
            yield return CreateAttributeArgument(name: null, constant: argument);
        }

        foreach (var argument in attribute.NamedArguments)
        {
            yield return CreateAttributeArgument(name: argument.Key, constant: argument.Value);
        }
    }

    private static AttributeArgumentMetadata CreateAttributeArgument(
        string? name,
        TypedConstant constant)
    {
        return new AttributeArgumentMetadata(Name: name, Value: FormatTypedConstant(constant: constant))
        {
            AssemblyName = GetAssemblyName(symbol: constant.Type),
            Type = constant.Type is null
                ? null
                : CreateTypeReference(symbol: constant.Type, nullableAnnotation: NullableAnnotation.NotAnnotated),
            TypeValue = constant.Kind == TypedConstantKind.Type
                && constant.Value is ITypeSymbol typeSymbol
                    ? CreateTypeReference(symbol: typeSymbol, nullableAnnotation: NullableAnnotation.NotAnnotated)
                    : null,
        };
    }

    private static string? FormatTypedConstant(TypedConstant constant)
    {
        if (constant.IsNull)
        {
            return null;
        }

        if (constant.Kind == TypedConstantKind.Type
            && constant.Value is ITypeSymbol typeSymbol)
        {
            return "typeof(" + GetTypeFullName(symbol: typeSymbol) + ")";
        }

        if (constant.Kind == TypedConstantKind.Array)
        {
            return string.Join(
                separator: ", ",
                values: constant.Values.Select(selector: FormatTypedConstant));
        }

        return constant.Value?.ToString();
    }

    private static string GetTypeFullName(ITypeSymbol symbol)
    {
        if (symbol is IArrayTypeSymbol arrayType)
        {
            return GetTypeFullName(symbol: arrayType.ElementType) + "[]";
        }

        if (symbol is INamedTypeSymbol { IsGenericType: true } namedType)
        {
            var arguments = string.Join(separator: ", ", values: namedType.TypeArguments.Select(selector: GetTypeFullName));
            return GetFullName(symbol: namedType) + "<" + arguments + ">";
        }

        return GetFullName(symbol: symbol);
    }

    private static string? FormatConstantValue(object? value)
    {
        return value switch
        {
            null => "null",
            bool boolValue => boolValue ? "true" : "false",
            IFormattable formattable => formattable.ToString(format: null, formatProvider: CultureInfo.InvariantCulture),
            _ => value.ToString(),
        };
    }

    private static TypeMetadataReference CreateTypeReference(
        ITypeSymbol symbol,
        NullableAnnotation nullableAnnotation)
    {
        var visitedSymbols = RentVisitedSymbols();
        try
        {
            return CreateTypeReference(
                symbol: symbol,
                nullableAnnotation: nullableAnnotation,
                visitedSymbols: visitedSymbols);
        }
        finally
        {
            ReturnVisitedSymbols(visitedSymbols: visitedSymbols);
        }
    }

    private static TypeMetadataReference CreateTypeReference(
        ITypeSymbol symbol,
        NullableAnnotation nullableAnnotation,
        ISet<ITypeSymbol> visitedSymbols)
    {
        if (!visitedSymbols.Add(item: symbol))
        {
            return CreateShallowTypeReference(symbol: symbol, nullableAnnotation: nullableAnnotation);
        }

        try
        {
            return CreateTypeReferenceCore(symbol: symbol, nullableAnnotation: nullableAnnotation, visitedSymbols: visitedSymbols);
        }
        finally
        {
            visitedSymbols.Remove(item: symbol);
        }
    }

    private static HashSet<ITypeSymbol> RentVisitedSymbols() =>
        TypeReferenceVisitedSymbolSets.TryTake(result: out var visitedSymbols)
            ? visitedSymbols
            : new HashSet<ITypeSymbol>(comparer: SymbolEqualityComparer.Default);

    private static void ReturnVisitedSymbols(HashSet<ITypeSymbol> visitedSymbols)
    {
        visitedSymbols.Clear();
        TypeReferenceVisitedSymbolSets.Add(item: visitedSymbols);
    }

    private static TypeMetadataReference CreateTypeReferenceCore(
        ITypeSymbol symbol,
        NullableAnnotation nullableAnnotation,
        ISet<ITypeSymbol> visitedSymbols)
    {
        if (symbol is INamedTypeSymbol namedType
            && IsNullableValueType(symbol: namedType)
            && namedType.TypeArguments.Length == 1)
        {
            var inner = CreateTypeReference(
                symbol: namedType.TypeArguments[index: 0],
                nullableAnnotation: NullableAnnotation.Annotated,
                visitedSymbols: visitedSymbols);
            return inner with
            {
                IsNullable = true,
            };
        }

        var elementType = GetElementType(symbol: symbol, visitedSymbols: visitedSymbols);
        var typeArguments = symbol is INamedTypeSymbol genericType
            ? genericType.TypeArguments
                .Zip(
                    second: genericType.TypeArgumentNullableAnnotations,
                    resultSelector: (argument, annotation) => CreateTypeReference(
                        symbol: argument,
                        nullableAnnotation: annotation,
                        visitedSymbols: visitedSymbols))
                .ToArray()
            : [];

        return new TypeMetadataReference(
            Name: GetDisplayName(symbol: symbol),
            FullName: GetFullName(symbol: symbol),
            Namespace: GetNamespace(symbol: symbol),
            IsNullable: nullableAnnotation == NullableAnnotation.Annotated,
            IsCollection: elementType is not null,
            IsDictionary: IsDictionary(symbol: symbol),
            IsEnum: IsEnum(symbol: symbol),
            IsPrimitive: IsPrimitive(symbol: symbol),
            IsDateLike: IsDateLike(symbol: symbol),
            ElementType: elementType,
            TypeArguments: typeArguments)
        {
            AssemblyName = GetAssemblyName(symbol: symbol),
            IsValueTuple = IsValueTuple(symbol: symbol),
            TupleElements = GetTupleElements(symbol: symbol, visitedSymbols: visitedSymbols).ToArray(),
            EnumValues = symbol is INamedTypeSymbol enumType
                ? GetEnumValues(symbol: enumType).ToArray()
                : [],
        };
    }

    private static TypeMetadataReference CreateShallowTypeReference(
        ITypeSymbol symbol,
        NullableAnnotation nullableAnnotation) =>
        new(
            Name: GetDisplayName(symbol: symbol),
            FullName: GetFullName(symbol: symbol),
            Namespace: GetNamespace(symbol: symbol),
            IsNullable: nullableAnnotation == NullableAnnotation.Annotated,
            IsCollection: false,
            IsDictionary: IsDictionary(symbol: symbol),
            IsEnum: IsEnum(symbol: symbol),
            IsPrimitive: IsPrimitive(symbol: symbol),
            IsDateLike: IsDateLike(symbol: symbol),
            ElementType: null,
            TypeArguments: [])
        {
            AssemblyName = GetAssemblyName(symbol: symbol),
            IsValueTuple = IsValueTuple(symbol: symbol),
            EnumValues = symbol is INamedTypeSymbol enumType
                ? GetEnumValues(symbol: enumType).ToArray()
                : [],
        };

    private static TypeMetadataReference? GetElementType(
        ITypeSymbol symbol,
        ISet<ITypeSymbol> visitedSymbols)
    {
        if (symbol.SpecialType == SpecialType.System_String)
        {
            return null;
        }

        if (symbol is IArrayTypeSymbol arrayType)
        {
            return CreateTypeReference(symbol: arrayType.ElementType, nullableAnnotation: arrayType.ElementNullableAnnotation, visitedSymbols: visitedSymbols);
        }

        if (symbol is not INamedTypeSymbol namedType)
        {
            return null;
        }

        var enumerable = namedType.AllInterfaces
            .Concat(second: [namedType])
            .FirstOrDefault(
                predicate: candidate => GetFullName(symbol: candidate.OriginalDefinition)
                                            .Equals(value: "System.Collections.Generic.IEnumerable", comparisonType: StringComparison.Ordinal)
                                        && candidate.TypeArguments.Length == 1);

        if (enumerable is null)
        {
            return null;
        }

        var annotation = enumerable.TypeArgumentNullableAnnotations.Length > 0
            ? enumerable.TypeArgumentNullableAnnotations[index: 0]
            : NullableAnnotation.NotAnnotated;
        return CreateTypeReference(symbol: enumerable.TypeArguments[index: 0], nullableAnnotation: annotation, visitedSymbols: visitedSymbols);
    }

    private static bool IsDictionary(ITypeSymbol symbol)
    {
        if (symbol is not INamedTypeSymbol namedType)
        {
            return false;
        }

        return namedType.AllInterfaces
            .Concat(second: [namedType])
            .Any(
                predicate: candidate =>
                {
                    var fullName = GetFullName(symbol: candidate.OriginalDefinition);
                    return (fullName.Equals(value: "System.Collections.Generic.IDictionary", comparisonType: StringComparison.Ordinal)
                            || fullName.Equals(value: "System.Collections.Generic.IReadOnlyDictionary", comparisonType: StringComparison.Ordinal)
                            || fullName.Equals(value: "System.Collections.Generic.Dictionary", comparisonType: StringComparison.Ordinal))
                        && candidate.TypeArguments.Length == 2;
                });
    }

    private static bool IsEnum(ITypeSymbol symbol)
    {
        return symbol.TypeKind == Microsoft.CodeAnalysis.TypeKind.Enum;
    }

    private static bool IsValueTuple(ITypeSymbol symbol)
    {
        return symbol is INamedTypeSymbol { IsTupleType: true };
    }

    private static IEnumerable<FieldMetadata> GetTupleElements(
        ITypeSymbol symbol,
        ISet<ITypeSymbol> visitedSymbols)
    {
        if (symbol is not INamedTypeSymbol { IsTupleType: true } tupleType)
        {
            return [];
        }

        return tupleType.TupleElements.Select(
            selector: field => new FieldMetadata(
                Name: field.Name,
                FullName: field.ToDisplayString(format: SymbolDisplayFormat.CSharpErrorMessageFormat),
                Accessibility: MapAccessibility(accessibility: field.DeclaredAccessibility),
                Type: CreateTypeReference(symbol: field.Type, nullableAnnotation: field.NullableAnnotation, visitedSymbols: visitedSymbols),
                Attributes: GetAttributes(attributes: field.GetAttributes()).ToArray(),
                ParentTypeFullName: GetFullName(symbol: tupleType))
            {
                AssemblyName = GetAssemblyName(symbol: field),
            });
    }

    private static bool IsPrimitive(ITypeSymbol symbol)
    {
        return symbol.SpecialType is SpecialType.System_Boolean
            or SpecialType.System_Byte
            or SpecialType.System_SByte
            or SpecialType.System_Int16
            or SpecialType.System_UInt16
            or SpecialType.System_Int32
            or SpecialType.System_UInt32
            or SpecialType.System_Int64
            or SpecialType.System_UInt64
            or SpecialType.System_Single
            or SpecialType.System_Double
            or SpecialType.System_Decimal
            or SpecialType.System_String
            or SpecialType.System_Char;
    }

    private static bool IsDateLike(ITypeSymbol symbol)
    {
        var fullName = GetFullName(symbol: symbol);
        return fullName.Equals(value: "System.DateTime", comparisonType: StringComparison.Ordinal)
            || fullName.Equals(value: "System.DateTimeOffset", comparisonType: StringComparison.Ordinal)
            || fullName.Equals(value: "System.DateOnly", comparisonType: StringComparison.Ordinal)
            || fullName.Equals(value: "System.TimeOnly", comparisonType: StringComparison.Ordinal)
            || fullName.Equals(value: "System.TimeSpan", comparisonType: StringComparison.Ordinal);
    }

    private static bool IsNullableValueType(INamedTypeSymbol symbol)
    {
        return GetFullName(symbol: symbol.OriginalDefinition)
            .Equals(value: "System.Nullable", comparisonType: StringComparison.Ordinal);
    }

    private static bool IsNullableAware(
        INamedTypeSymbol symbol,
        Compilation compilation)
    {
        return symbol.DeclaringSyntaxReferences.Any(
            predicate: reference =>
            {
                var semanticModel = compilation.GetSemanticModel(syntaxTree: reference.SyntaxTree);
                var nullableContext = semanticModel.GetNullableContext(position: reference.Span.Start);
                return (nullableContext & NullableContext.AnnotationsEnabled) == NullableContext.AnnotationsEnabled;
            });
    }

    private static string GetDisplayName(ITypeSymbol symbol)
    {
        return symbol switch
        {
            IArrayTypeSymbol arrayType => $"{GetDisplayName(symbol: arrayType.ElementType)}[]",
            INamedTypeSymbol { IsGenericType: true } namedType => namedType.Name,
            _ => symbol.Name,
        };
    }

    private static string GetNamespace(ISymbol? symbol)
    {
        if (symbol?.ContainingNamespace is null || symbol.ContainingNamespace.IsGlobalNamespace)
        {
            return string.Empty;
        }

        return symbol.ContainingNamespace.ToDisplayString();
    }

    private static string GetFullName(ISymbol? symbol)
    {
        if (symbol is null)
        {
            return string.Empty;
        }

        if (symbol is ITypeSymbol typeSymbol)
        {
            var specialTypeName = GetSpecialTypeName(specialType: typeSymbol.SpecialType);
            if (specialTypeName is not null)
            {
                return specialTypeName;
            }
        }

        if (symbol.ContainingType is not null)
        {
            return $"{GetFullName(symbol: symbol.ContainingType)}.{symbol.Name}";
        }

        var namespaceName = GetNamespace(symbol: symbol);
        return string.IsNullOrEmpty(value: namespaceName)
            ? symbol.Name
            : $"{namespaceName}.{symbol.Name}";
    }

    private static string GetAssemblyName(ISymbol? symbol)
    {
        return symbol?.ContainingAssembly?.Name ?? string.Empty;
    }

    private static string? GetSpecialTypeName(SpecialType specialType)
    {
        return specialType switch
        {
            SpecialType.System_Boolean => "System.Boolean",
            SpecialType.System_Byte => "System.Byte",
            SpecialType.System_SByte => "System.SByte",
            SpecialType.System_Int16 => "System.Int16",
            SpecialType.System_UInt16 => "System.UInt16",
            SpecialType.System_Int32 => "System.Int32",
            SpecialType.System_UInt32 => "System.UInt32",
            SpecialType.System_Int64 => "System.Int64",
            SpecialType.System_UInt64 => "System.UInt64",
            SpecialType.System_Single => "System.Single",
            SpecialType.System_Double => "System.Double",
            SpecialType.System_Decimal => "System.Decimal",
            SpecialType.System_String => "System.String",
            SpecialType.System_Char => "System.Char",
            SpecialType.System_Object => "System.Object",
            SpecialType.System_Void => "System.Void",
            _ => null,
        };
    }

    private static MetadataAccessibility MapAccessibility(Accessibility accessibility)
    {
        return accessibility switch
        {
            Accessibility.Public => MetadataAccessibility.Public,
            Accessibility.Internal => MetadataAccessibility.Internal,
            Accessibility.Protected => MetadataAccessibility.Protected,
            Accessibility.ProtectedOrInternal => MetadataAccessibility.ProtectedInternal,
            Accessibility.ProtectedAndInternal => MetadataAccessibility.PrivateProtected,
            _ => MetadataAccessibility.Private,
        };
    }

    private static SourceLocation? GetSourceLocation(ISymbol symbol)
    {
        var location = symbol.Locations.FirstOrDefault(predicate: candidate => candidate.IsInSource);
        if (location is null)
        {
            return null;
        }

        var lineSpan = location.GetLineSpan();
        if (string.IsNullOrWhiteSpace(value: lineSpan.Path))
        {
            return null;
        }

        return new SourceLocation(
            Path: Path.GetFullPath(path: lineSpan.Path),
            Line: lineSpan.StartLinePosition.Line + 1,
            Column: lineSpan.StartLinePosition.Character + 1);
    }

    private static DocCommentMetadata? GetDocComment(ISymbol symbol) =>
        DocCommentCache.GetValue(key: symbol, createValueCallback: static cachedSymbol => new CachedDocComment(ReadDocComment(symbol: cachedSymbol))).Value;

    private static DocCommentMetadata? ReadDocComment(ISymbol symbol)
    {
        var documentation = symbol.GetDocumentationCommentXml(
            preferredCulture: CultureInfo.InvariantCulture,
            expandIncludes: true);
        if (string.IsNullOrWhiteSpace(value: documentation))
        {
            return null;
        }

        try
        {
            var document = XDocument.Parse(text: "<doc>" + documentation + "</doc>");
            return new DocCommentMetadata(
                Summary: NormalizeDocumentation(documentation: GetDocCommentElementContent(element: document.Descendants(name: "summary").FirstOrDefault())) ?? string.Empty,
                Returns: NormalizeDocumentation(documentation: GetDocCommentElementContent(element: document.Descendants(name: "returns").FirstOrDefault())) ?? string.Empty,
                Parameters: document.Descendants(name: "param")
                    .Select(
                        selector: element => new ParameterCommentMetadata(
                            Name: element.Attribute(name: "name")?.Value.Trim() ?? string.Empty,
                            Description: NormalizeDocumentation(documentation: GetDocCommentElementContent(element: element)) ?? string.Empty))
                    .ToArray());
        }
        catch (System.Xml.XmlException)
        {
            return new DocCommentMetadata(Summary: NormalizeDocumentation(documentation: documentation) ?? string.Empty, Returns: string.Empty, Parameters: []);
        }
    }

    private static IEnumerable<TypeParameterMetadata> GetTypeParameters(IEnumerable<ITypeParameterSymbol> symbols)
    {
        return symbols.Select(selector: symbol => new TypeParameterMetadata(Name: symbol.Name)
        {
            FullName = symbol.ToDisplayString(format: SymbolDisplayFormat.CSharpErrorMessageFormat),
            Documentation = GetDocComment(symbol: symbol)?.Summary,
        });
    }

    private static TypeMetadataReference CreateVoidTypeReference(Compilation compilation)
    {
        var voidType = compilation.GetSpecialType(specialType: SpecialType.System_Void);
        return CreateTypeReference(symbol: voidType, nullableAnnotation: NullableAnnotation.NotAnnotated);
    }

    private static string? GetStaticReadOnlyFieldValue(IFieldSymbol field)
    {
        var syntax = field.DeclaringSyntaxReferences
            .Select(selector: reference => reference.GetSyntax())
            .OfType<VariableDeclaratorSyntax>()
            .FirstOrDefault();
        var initializer = syntax?.Initializer?.Value;
        return initializer switch
        {
            null => "The field might be initialized elsewhere (like in a static constructor) or not initialized at all. Typewriter is not able to process such static readonly fields",
            LiteralExpressionSyntax literal when literal.IsKind(kind: SyntaxKind.NullLiteralExpression) => string.Empty,
            LiteralExpressionSyntax literal => literal.Token.ValueText,
            _ => initializer.ToString(),
        };
    }

    private static string? GetPropertyInitializerValue(IPropertySymbol property)
    {
        var syntax = property.DeclaringSyntaxReferences
            .Select(selector: reference => reference.GetSyntax())
            .OfType<PropertyDeclarationSyntax>()
            .FirstOrDefault();
        return GetInitializerValueText(initializer: syntax?.Initializer);
    }

    private static string? GetFieldInitializerValue(IFieldSymbol field)
    {
        var syntax = field.DeclaringSyntaxReferences
            .Select(selector: reference => reference.GetSyntax())
            .OfType<VariableDeclaratorSyntax>()
            .FirstOrDefault();
        return GetInitializerValueText(initializer: syntax?.Initializer);
    }

    private static string? GetInitializerValueText(EqualsValueClauseSyntax? initializer)
    {
        var value = initializer?.Value;
        return value switch
        {
            null => null,
            LiteralExpressionSyntax literal when literal.IsKind(kind: SyntaxKind.NullLiteralExpression) => "null",
            LiteralExpressionSyntax literal => literal.Token.ValueText,
            _ => value.ToString(),
        };
    }

    private static string? GetDocCommentElementContent(XElement? element)
    {
        return element is null
            ? null
            : string.Concat(values: element.Nodes());
    }

    private static string? NormalizeDocumentation(string? documentation)
    {
        if (string.IsNullOrWhiteSpace(value: documentation))
        {
            return null;
        }

        var parts = documentation
            .Split(
                separator: [' ', '\t', '\r', '\n'],
                options: StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 0
            ? null
            : string.Join(separator: ' ', value: parts);
    }

    private static string TrimAttributeSuffix(string name)
    {
        const string suffix = "Attribute";
        return name.EndsWith(value: suffix, comparisonType: StringComparison.Ordinal)
            ? name[..^suffix.Length]
            : name;
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

    private sealed record ProjectMetadataCacheEntry(
        ProjectMetadataBuildResult Result,
        ProjectMetadataFingerprint Fingerprint);

    private sealed record ProjectMetadataFingerprint(
        IReadOnlyList<string> Paths,
        IReadOnlyList<string> Directories,
        string Hash)
    {
        public static ProjectMetadataFingerprint Create(ProjectMetadataBuildResult result)
        {
            var paths = new SortedSet<string>(comparer: StringComparer.OrdinalIgnoreCase);
            AddPath(paths: paths, path: result.Metadata.ProjectPath);
            foreach (var sourceFile in result.Metadata.SourceFiles)
            {
                AddPath(paths: paths, path: sourceFile.Path);
            }

            foreach (var reference in result.CompilationReferences)
            {
                AddPath(paths: paths, path: reference.ProjectPath);
            }

            if (result.Compilation is not null)
            {
                foreach (var referencePath in result.Compilation.References.Select(selector: reference => reference.Display))
                {
                    AddPath(paths: paths, path: referencePath);
                }
            }

            var directories = new SortedSet<string>(comparer: StringComparer.OrdinalIgnoreCase);
            foreach (var projectPath in result.CompilationReferences.Select(selector: reference => reference.ProjectPath).Append(element: result.Metadata.ProjectPath))
            {
                var projectDirectory = Path.GetDirectoryName(path: projectPath);
                if (!string.IsNullOrWhiteSpace(value: projectDirectory) && Directory.Exists(path: projectDirectory))
                {
                    foreach (var directory in EnumerateDirectoriesForFingerprint(root: projectDirectory))
                    {
                        directories.Add(item: directory);
                    }
                }
            }

            foreach (var sourceDirectory in result.Metadata.SourceFiles
                         .Select(selector: sourceFile => Path.GetDirectoryName(path: sourceFile.Path))
                         .Where(predicate: sourceDirectory => !string.IsNullOrWhiteSpace(value: sourceDirectory)))
            {
                directories.Add(item: Path.GetFullPath(path: sourceDirectory!));
            }

            var pathList = paths.ToArray();
            var directoryList = directories.ToArray();
            return new ProjectMetadataFingerprint(
                Paths: pathList,
                Directories: directoryList,
                Hash: ComputeFingerprintHash(paths: pathList, directories: directoryList));
        }

        public bool IsCurrent() =>
            Hash.Equals(value: ComputeFingerprintHash(paths: Paths, directories: Directories), comparisonType: StringComparison.Ordinal);

        private static void AddPath(
            ISet<string> paths,
            string? path)
        {
            if (string.IsNullOrWhiteSpace(value: path))
            {
                return;
            }

            try
            {
                paths.Add(item: Path.GetFullPath(path: path));
            }
            catch (ArgumentException ex)
            {
                _ = ex;
            }
            catch (NotSupportedException ex)
            {
                _ = ex;
            }
        }

        private static IEnumerable<string> EnumerateDirectoriesForFingerprint(string root)
        {
            var pending = new Stack<string>();
            pending.Push(item: Path.GetFullPath(path: root));
            while (pending.Count > 0)
            {
                var directory = pending.Pop();
                yield return directory;
                foreach (var childDirectory in Directory.EnumerateDirectories(path: directory)
                             .Where(predicate: childDirectory => !IsIgnoredDirectoryForFingerprint(name: Path.GetFileName(path: childDirectory))))
                {
                    pending.Push(item: childDirectory);
                }
            }
        }

        private static bool IsIgnoredDirectoryForFingerprint(string name) =>
            name.Equals(value: ".git", comparisonType: StringComparison.OrdinalIgnoreCase)
            || name.Equals(value: "artifacts", comparisonType: StringComparison.OrdinalIgnoreCase)
            || name.Equals(value: "bin", comparisonType: StringComparison.OrdinalIgnoreCase)
            || name.Equals(value: "node_modules", comparisonType: StringComparison.OrdinalIgnoreCase)
            || name.Equals(value: "obj", comparisonType: StringComparison.OrdinalIgnoreCase);

        private static string ComputeFingerprintHash(
            IEnumerable<string> paths,
            IEnumerable<string> directories)
        {
            using var hash = IncrementalHash.CreateHash(hashAlgorithm: HashAlgorithmName.SHA256);
            foreach (var path in paths)
            {
                AppendHashValue(hash: hash, value: "file");
                AppendPathFingerprint(hash: hash, path: path);
            }

            foreach (var directory in directories)
            {
                AppendHashValue(hash: hash, value: nameof(directory));
                AppendDirectoryFingerprint(hash: hash, directory: directory);
            }

            return Convert.ToHexString(inArray: hash.GetHashAndReset());
        }

        private static void AppendPathFingerprint(
            IncrementalHash hash,
            string path)
        {
            AppendHashValue(hash: hash, value: path);
            try
            {
                if (!File.Exists(path: path))
                {
                    AppendHashValue(hash: hash, value: "missing");
                    return;
                }

#pragma warning disable SCS0018
                var file = new FileInfo(fileName: path);
#pragma warning restore SCS0018
                AppendHashValue(hash: hash, value: file.Length.ToString(provider: CultureInfo.InvariantCulture));
                AppendHashValue(hash: hash, value: file.LastWriteTimeUtc.Ticks.ToString(provider: CultureInfo.InvariantCulture));
            }
            catch (IOException)
            {
                AppendHashValue(hash: hash, value: "unreadable");
            }
            catch (UnauthorizedAccessException)
            {
                AppendHashValue(hash: hash, value: "unreadable");
            }
        }

        private static void AppendDirectoryFingerprint(
            IncrementalHash hash,
            string directory)
        {
            AppendHashValue(hash: hash, value: directory);
            try
            {
                if (!Directory.Exists(path: directory))
                {
                    AppendHashValue(hash: hash, value: "missing");
                    return;
                }

                var info = new DirectoryInfo(path: directory);
                AppendHashValue(hash: hash, value: info.LastWriteTimeUtc.Ticks.ToString(provider: CultureInfo.InvariantCulture));
            }
            catch (IOException)
            {
                AppendHashValue(hash: hash, value: "unreadable");
            }
            catch (UnauthorizedAccessException)
            {
                AppendHashValue(hash: hash, value: "unreadable");
            }
        }

        private static void AppendHashValue(
            IncrementalHash hash,
            string value)
        {
            var bytes = Encoding.UTF8.GetBytes(s: value);
            hash.AppendData(data: bytes);
            hash.AppendData(data: [0]);
        }
    }

    private sealed record CachedDocComment(DocCommentMetadata? Value);

    private sealed record ProjectMetadataBuildResult(
        ProjectMetadata Metadata,
        CSharpCompilation? Compilation,
        IReadOnlyList<ProjectCompilationReference> CompilationReferences);

    private sealed record ProjectCompilationReference(
        string ProjectPath,
        CSharpCompilation Compilation);

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
