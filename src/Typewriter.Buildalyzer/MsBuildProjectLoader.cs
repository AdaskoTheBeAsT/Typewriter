using Typewriter.Abstractions;

namespace Typewriter.Buildalyzer;

public sealed class MsBuildProjectLoader : IProjectWorkspaceLoader
{
    private static readonly StringComparer PathComparer = StringComparer.OrdinalIgnoreCase;

    public Task<ProjectLoadResult> LoadAsync(
        ProjectContext project,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(argument: project);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(result: LoadCore(project: project, cancellationToken: cancellationToken));
    }

    private static ProjectLoadResult LoadCore(
        ProjectContext project,
        CancellationToken cancellationToken)
    {
        var projectPath = Path.GetFullPath(path: project.ProjectPath);
        if (!File.Exists(path: projectPath))
        {
            return CreateErrorResult(
                projectPath: projectPath,
                message: $"Project file does not exist: {projectPath}.");
        }

        var state = new LoadState(projectPath: projectPath);
        try
        {
            var workspacePath = Path.GetFullPath(path: project.WorkspacePath);
            var manager = CreateAnalyzerManager(workspacePath: workspacePath);
            var solutionProperties = CreateSolutionProperties(workspacePath: workspacePath);
            LoadProject(
                manager: manager,
                projectPath: projectPath,
                requestedTargetFramework: project.TargetFramework,
                isRootProject: true,
                state: state,
                solutionProperties: solutionProperties,
                cancellationToken: cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            state.Diagnostics.Add(
                item: new GenerationDiagnostic(
                    File: projectPath,
                    Line: null,
                    Column: null,
                    Severity: DiagnosticSeverity.Error,
                    Message: $"Buildalyzer failed to load project metadata: {exception.Message}",
                    Code: "TW0003"));
        }

        return state.ToResult();
    }

#pragma warning disable MA0051 // Method is too long
    private static void LoadProject(
        global::Buildalyzer.AnalyzerManager manager,
        string projectPath,
        string? requestedTargetFramework,
        bool isRootProject,
        LoadState state,
        IReadOnlyDictionary<string, string> solutionProperties,
        CancellationToken cancellationToken)
#pragma warning restore MA0051 // Method is too long
    {
        cancellationToken.ThrowIfCancellationRequested();
        projectPath = Path.GetFullPath(path: projectPath);
        if (!state.VisitedProjects.Add(item: projectPath))
        {
            return;
        }

        if (!File.Exists(path: projectPath))
        {
            state.Diagnostics.Add(
                item: new GenerationDiagnostic(
                    File: projectPath,
                    Line: null,
                    Column: null,
                    Severity: DiagnosticSeverity.Error,
                    Message: $"Project file does not exist: {projectPath}.",
                    Code: "TW0003"));
            return;
        }

        var analyzer = manager.GetProject(projectFilePath: projectPath);
        ApplyGlobalProperties(analyzer: analyzer, properties: solutionProperties);

        var environmentOptions = CreateEnvironmentOptions();
        var results = string.IsNullOrWhiteSpace(value: requestedTargetFramework)
            ? analyzer.Build(environmentOptions: environmentOptions)
            : analyzer.Build(targetFramework: requestedTargetFramework, environmentOptions: environmentOptions);
        var result = SelectResult(results: results, requestedTargetFramework: requestedTargetFramework);
        if (result is null)
        {
            state.Diagnostics.Add(
                item: new GenerationDiagnostic(
                    File: projectPath,
                    Line: null,
                    Column: null,
                    Severity: DiagnosticSeverity.Error,
                    Message: $"Buildalyzer did not return metadata for project: {projectPath}.",
                    Code: "TW0003"));
            return;
        }

        if (!result.Succeeded)
        {
            state.Diagnostics.Add(
                item: new GenerationDiagnostic(
                    File: projectPath,
                    Line: null,
                    Column: null,
                    Severity: DiagnosticSeverity.Error,
                    Message: $"Buildalyzer could not evaluate project: {projectPath}.",
                    Code: "TW0003"));
            return;
        }

        if (isRootProject)
        {
            state.ProjectDirectory = Path.GetDirectoryName(path: result.ProjectFilePath) ?? Environment.CurrentDirectory;
            state.TargetFramework = result.TargetFramework;
            state.NullableEnabled = IsEnabled(value: result.GetProperty(name: "Nullable"));
            state.ImplicitUsingsEnabled = IsEnabled(value: result.GetProperty(name: "ImplicitUsings"));
            state.GlobalUsings.UnionWith(other: GetImplicitUsings(enabled: state.ImplicitUsingsEnabled));
            state.PreprocessorSymbols.UnionWith(other: result.PreprocessorSymbols);
        }

        var currentProjectDirectory = Path.GetDirectoryName(path: result.ProjectFilePath) ?? Environment.CurrentDirectory;
        currentProjectDirectory = Path.GetFullPath(path: currentProjectDirectory);
        var projectReferences = result.ProjectReferences
            .Select(selector: Path.GetFullPath)
            .Where(predicate: File.Exists)
            .ToArray();
        var projectReferenceDirectories = projectReferences
            .Select(selector: Path.GetDirectoryName)
            .Where(predicate: directory => !string.IsNullOrWhiteSpace(value: directory))
            .Select(selector: directory => Path.GetFullPath(path: directory!))
            .Where(predicate: directory => !PathEquals(left: directory, right: currentProjectDirectory))
            .Distinct(comparer: PathComparer)
            .ToArray();

        state.SourceFiles.UnionWith(
            other: result.SourceFiles
                .Where(predicate: File.Exists)
                .Where(predicate: IsMetadataSourceFile)
                .Select(selector: Path.GetFullPath)
                .Where(predicate: path => !IsInAnyDirectory(path: path, directories: projectReferenceDirectories)));
        state.ReferencePaths.UnionWith(
            other: result.References
                .Where(predicate: File.Exists)
                .Select(selector: Path.GetFullPath));

        foreach (var projectReference in projectReferences)
        {
            state.ProjectReferences.Add(item: projectReference);
        }
    }

    private static global::Buildalyzer.IAnalyzerResult? SelectResult(
        global::Buildalyzer.IAnalyzerResults results,
        string? requestedTargetFramework)
    {
        if (!string.IsNullOrWhiteSpace(value: requestedTargetFramework)
            && results.TryGetTargetFramework(targetFramework: requestedTargetFramework, result: out var requestedResult))
        {
            return requestedResult;
        }

        return results.Results.FirstOrDefault(predicate: result => result.Succeeded && result.SourceFiles.Length > 0)
            ?? results.Results.FirstOrDefault();
    }

    private static global::Buildalyzer.AnalyzerManager CreateAnalyzerManager(string workspacePath)
    {
        var fullPath = Path.GetFullPath(path: workspacePath);
        return fullPath.EndsWith(value: ".sln", comparisonType: StringComparison.OrdinalIgnoreCase) && File.Exists(path: fullPath)
            ? new global::Buildalyzer.AnalyzerManager(solutionFilePath: fullPath)
            : new global::Buildalyzer.AnalyzerManager();
    }

    private static IReadOnlyDictionary<string, string> CreateSolutionProperties(string workspacePath)
    {
        var fullPath = Path.GetFullPath(path: workspacePath);
        if (!IsSolutionFile(path: fullPath) || !File.Exists(path: fullPath))
        {
            return new Dictionary<string, string>(comparer: StringComparer.OrdinalIgnoreCase);
        }

        var solutionDirectory = Path.GetDirectoryName(path: fullPath) ?? Environment.CurrentDirectory;
        if (!solutionDirectory.EndsWith(value: Path.DirectorySeparatorChar))
        {
            solutionDirectory += Path.DirectorySeparatorChar;
        }

        return new Dictionary<string, string>(comparer: StringComparer.OrdinalIgnoreCase)
        {
            [key: "SolutionDir"] = solutionDirectory,
            [key: "SolutionPath"] = fullPath,
            [key: "SolutionFileName"] = Path.GetFileName(path: fullPath),
            [key: "SolutionName"] = Path.GetFileNameWithoutExtension(path: fullPath),
            [key: "SolutionExt"] = Path.GetExtension(path: fullPath),
        };
    }

    private static void ApplyGlobalProperties(
        global::Buildalyzer.IProjectAnalyzer analyzer,
        IReadOnlyDictionary<string, string> properties)
    {
        foreach (var property in properties)
        {
            analyzer.SetGlobalProperty(key: property.Key, value: property.Value);
        }
    }

    private static bool IsSolutionFile(string path) =>
        path.EndsWith(value: ".sln", comparisonType: StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(value: ".slnx", comparisonType: StringComparison.OrdinalIgnoreCase);

    private static global::Buildalyzer.Environment.EnvironmentOptions CreateEnvironmentOptions()
    {
        var environmentOptions = new global::Buildalyzer.Environment.EnvironmentOptions();
        environmentOptions.Arguments.Add(item: "/nodeReuse:false");
        environmentOptions.GlobalProperties[key: "UseSharedCompilation"] = "false";
        environmentOptions.EnvironmentVariables[key: "MSBUILDDISABLENODEREUSE"] = "1";

        return environmentOptions;
    }

    private static ProjectLoadResult CreateErrorResult(
        string projectPath,
        string message) =>
        new(
            ProjectPath: projectPath,
            ProjectDirectory: Path.GetDirectoryName(path: projectPath) ?? Environment.CurrentDirectory,
            TargetFramework: null,
            NullableEnabled: false,
            ImplicitUsingsEnabled: false,
            SourceFiles: [],
            PreprocessorSymbols: [],
            GlobalUsings: [],
            ProjectReferences: [],
            ReferencePaths: [],
            Diagnostics:
            [
                new GenerationDiagnostic(
                    File: projectPath,
                    Line: null,
                    Column: null,
                    Severity: DiagnosticSeverity.Error,
                    Message: message,
                    Code: "TW0003"),
            ]);

    private static IEnumerable<string> GetImplicitUsings(bool enabled)
    {
        if (!enabled)
        {
            return [];
        }

#pragma warning disable CC0021 // Use nameof
        return
        [
            "System",
            "System.Collections.Generic",
            "System.IO",
            "System.Linq",
            "System.Net.Http",
            "System.Threading",
            "System.Threading.Tasks",
        ];
#pragma warning restore CC0021 // Use nameof
    }

    private static bool IsEnabled(string? value) =>
        value is not null
        && (value.Equals(value: "enable", comparisonType: StringComparison.OrdinalIgnoreCase)
            || value.Equals(value: "true", comparisonType: StringComparison.OrdinalIgnoreCase));

    private static bool IsMetadataSourceFile(string path)
    {
        var fullPath = Path.GetFullPath(path: path);
        if (!fullPath.Contains(value: $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var fileName = Path.GetFileName(path: fullPath);
        return !fileName.EndsWith(value: ".AssemblyInfo.cs", comparisonType: StringComparison.OrdinalIgnoreCase)
            && !fileName.EndsWith(value: ".AssemblyAttributes.cs", comparisonType: StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsInAnyDirectory(
        string path,
        IEnumerable<string> directories) =>
        directories.Any(predicate: directory => IsSameOrChildPath(path: path, root: directory));

    private static bool IsSameOrChildPath(
        string path,
        string root)
    {
        var relativePath = Path.GetRelativePath(relativeTo: root, path: path);
        return string.Equals(a: relativePath, b: ".", comparisonType: StringComparison.Ordinal)
            || (!relativePath.StartsWith(value: "..", comparisonType: StringComparison.Ordinal)
                && !Path.IsPathRooted(path: relativePath));
    }

    private static bool PathEquals(
        string left,
        string right) =>
        string.Equals(
            a: Path.TrimEndingDirectorySeparator(path: Path.GetFullPath(path: left)),
            b: Path.TrimEndingDirectorySeparator(path: Path.GetFullPath(path: right)),
            comparisonType: StringComparison.OrdinalIgnoreCase);

    private sealed class LoadState
    {
        public LoadState(string projectPath)
        {
            ProjectPath = projectPath;
            ProjectDirectory = Path.GetDirectoryName(path: projectPath) ?? Environment.CurrentDirectory;
        }

        public string ProjectPath { get; }

        public string ProjectDirectory { get; set; }

        public string? TargetFramework { get; set; }

        public bool NullableEnabled { get; set; }

        public bool ImplicitUsingsEnabled { get; set; }

        public HashSet<string> VisitedProjects { get; } = new(comparer: PathComparer);

        public HashSet<string> SourceFiles { get; } = new(comparer: PathComparer);

        public HashSet<string> PreprocessorSymbols { get; } = new(comparer: StringComparer.Ordinal);

        public HashSet<string> GlobalUsings { get; } = new(comparer: StringComparer.Ordinal);

        public HashSet<string> ProjectReferences { get; } = new(comparer: PathComparer);

        public HashSet<string> ReferencePaths { get; } = new(comparer: PathComparer);

        public List<GenerationDiagnostic> Diagnostics { get; } = [];

        public ProjectLoadResult ToResult() =>
            new(
                ProjectPath: ProjectPath,
                ProjectDirectory: ProjectDirectory,
                TargetFramework: TargetFramework,
                NullableEnabled: NullableEnabled,
                ImplicitUsingsEnabled: ImplicitUsingsEnabled,
                SourceFiles: SourceFiles.Order(comparer: StringComparer.OrdinalIgnoreCase).ToArray(),
                PreprocessorSymbols: PreprocessorSymbols.Order(comparer: StringComparer.Ordinal).ToArray(),
                GlobalUsings: GlobalUsings.Order(comparer: StringComparer.Ordinal).ToArray(),
                ProjectReferences: ProjectReferences.Order(comparer: StringComparer.OrdinalIgnoreCase).ToArray(),
                ReferencePaths: ReferencePaths.Order(comparer: StringComparer.OrdinalIgnoreCase).ToArray(),
                Diagnostics: Diagnostics.ToArray());
    }
}
