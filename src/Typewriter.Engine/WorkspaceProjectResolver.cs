using System.Xml.Linq;
using Typewriter.Abstractions;

namespace Typewriter.Engine;

internal static class WorkspaceProjectResolver
{
    public static string ResolveWorkspacePath(GenerationRequest request)
    {
        if (!string.IsNullOrWhiteSpace(value: request.WorkspacePath))
        {
            return Path.GetFullPath(path: request.WorkspacePath);
        }

        if (!string.IsNullOrWhiteSpace(value: request.ProjectPath))
        {
            var projectPath = Path.GetFullPath(path: request.ProjectPath);
            return Path.GetDirectoryName(path: projectPath) ?? projectPath;
        }

        if (!string.IsNullOrWhiteSpace(value: request.TemplatePath))
        {
            var templatePath = Path.GetFullPath(path: request.TemplatePath);
            return Path.GetDirectoryName(path: templatePath) ?? templatePath;
        }

        return Environment.CurrentDirectory;
    }

    public static string? ResolveProjectPath(
        GenerationRequest request,
        string workspacePath,
        ICollection<GenerationDiagnostic> diagnostics) =>
        ResolveProjectPaths(request: request, workspacePath: workspacePath, diagnostics: diagnostics).FirstOrDefault();

#pragma warning disable MA0051 // Method is too long
    public static IReadOnlyList<string> ResolveProjectPaths(
        GenerationRequest request,
        string workspacePath,
        ICollection<GenerationDiagnostic> diagnostics)
    {
        if (!string.IsNullOrWhiteSpace(value: request.ProjectPath))
        {
            return [Path.GetFullPath(path: request.ProjectPath)];
        }

        var root = Path.GetFullPath(path: workspacePath);
        if (File.Exists(path: root) && root.EndsWith(value: ".csproj", comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            return [root];
        }

        string[] projects;
        if (IsSolutionFile(path: root) && File.Exists(path: root))
        {
            projects = GetProjectsFromSolution(solutionPath: root);
        }
        else
        {
            if (File.Exists(path: root))
            {
                root = Path.GetDirectoryName(path: root) ?? root;
            }

            projects = EnumerateProjectFiles(root: root)
                .Order(comparer: StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        if (projects.Length == 1)
        {
            return [Path.GetFullPath(path: projects[0])];
        }

        if (projects.Length > 1)
        {
            if (request.AllProjects)
            {
                return projects.Select(selector: Path.GetFullPath).ToArray();
            }

            var nearestProjectPath = FindNearestAncestorProjectPath(
                templatePath: request.TemplatePath,
                workspacePath: root,
                projectPaths: projects);
            if (!string.IsNullOrWhiteSpace(value: nearestProjectPath))
            {
                return [nearestProjectPath];
            }
        }
#pragma warning restore MA0051 // Method is too long

#pragma warning disable SA1118 // Parameter should not span multiple lines
        diagnostics.Add(
            item: new GenerationDiagnostic(
                File: workspacePath,
                Line: null,
                Column: null,
                Severity: DiagnosticSeverity.Error,
                Message: projects.Length == 0
                    ? "No .csproj file was found. Pass --project explicitly."
                    : "Multiple .csproj files were found. Pass --project explicitly.",
                Code: DiagnosticCodes.ProjectLoadFailed));
#pragma warning restore SA1118 // Parameter should not span multiple lines
        return [];
    }

    public static IReadOnlyList<string> ResolveProjectPathsByName(
        string workspacePath,
        IEnumerable<string> projectNames,
        ICollection<string> unresolvedNames)
    {
        ArgumentNullException.ThrowIfNull(argument: projectNames);
        ArgumentNullException.ThrowIfNull(argument: unresolvedNames);

        var workspaceProjects = GetWorkspaceProjects(workspacePath: workspacePath);
        var workspaceProjectSet = workspaceProjects.ToHashSet(comparer: StringComparer.OrdinalIgnoreCase);
        var resolvedPaths = new List<string>();
        var resolvedPathSet = new HashSet<string>(comparer: StringComparer.OrdinalIgnoreCase);
        foreach (var projectName in projectNames)
        {
            var resolvedPath = ResolveProjectPathByName(
                workspacePath: workspacePath,
                workspaceProjects: workspaceProjects,
                workspaceProjectSet: workspaceProjectSet,
                projectName: projectName);
            if (string.IsNullOrWhiteSpace(value: resolvedPath))
            {
                unresolvedNames.Add(item: projectName);
                continue;
            }

            if (resolvedPathSet.Add(item: resolvedPath))
            {
                resolvedPaths.Add(item: resolvedPath);
            }
        }

        return resolvedPaths;
    }

    private static string? ResolveProjectPathByName(
        string workspacePath,
        IReadOnlyList<string> workspaceProjects,
        IReadOnlySet<string> workspaceProjectSet,
        string projectName)
    {
        if (string.IsNullOrWhiteSpace(value: projectName))
        {
            return null;
        }

        var trimmedName = projectName.Trim();
        if (trimmedName.EndsWith(value: ".csproj", comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            var normalized = trimmedName.Replace(oldChar: '\\', newChar: Path.DirectorySeparatorChar);
            var candidate = Path.GetFullPath(
                path: Path.IsPathRooted(path: normalized)
                    ? normalized
                    : Path.Combine(path1: GetSearchRoot(workspacePath: Path.GetFullPath(path: workspacePath)), path2: normalized));
            if (workspaceProjectSet.Contains(item: candidate))
            {
                return candidate;
            }

            if (File.Exists(path: candidate))
            {
                return null;
            }

            trimmedName = Path.GetFileNameWithoutExtension(path: trimmedName);
        }

        return workspaceProjects.FirstOrDefault(
            predicate: projectPath => Path.GetFileNameWithoutExtension(path: projectPath)
                .Equals(value: trimmedName, comparisonType: StringComparison.OrdinalIgnoreCase));
    }

    private static string[] GetWorkspaceProjects(string workspacePath)
    {
        var root = Path.GetFullPath(path: workspacePath);
        if (IsSolutionFile(path: root) && File.Exists(path: root))
        {
            return GetProjectsFromSolution(solutionPath: root);
        }

        if (File.Exists(path: root))
        {
            root = Path.GetDirectoryName(path: root) ?? root;
        }

        if (!Directory.Exists(path: root))
        {
            return [];
        }

        return EnumerateProjectFiles(root: root)
            .Select(selector: Path.GetFullPath)
            .Order(comparer: StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string[] GetProjectsFromSolution(string solutionPath) =>
        solutionPath.EndsWith(value: ".slnx", comparisonType: StringComparison.OrdinalIgnoreCase)
            ? GetProjectsFromSlnx(solutionPath: solutionPath)
            : GetProjectsFromSln(solutionPath: solutionPath);

    private static string? FindNearestAncestorProjectPath(
        string? templatePath,
        string workspacePath,
        IEnumerable<string> projectPaths)
    {
        if (string.IsNullOrWhiteSpace(value: templatePath))
        {
            return null;
        }

        var projectPathSet = projectPaths
            .Select(selector: Path.GetFullPath)
            .ToHashSet(comparer: StringComparer.OrdinalIgnoreCase);
        var workspaceRoot = GetSearchRoot(workspacePath: workspacePath);
        var directory = Path.GetDirectoryName(path: Path.GetFullPath(path: templatePath));
        while (!string.IsNullOrWhiteSpace(value: directory)
            && IsSameOrChildPath(path: directory, root: workspaceRoot))
        {
            if (Directory.Exists(path: directory))
            {
                var projectPath = Directory
                    .EnumerateFiles(path: directory, searchPattern: "*.csproj", searchOption: SearchOption.TopDirectoryOnly)
                    .Select(selector: Path.GetFullPath)
                    .Where(predicate: projectPathSet.Contains)
                    .Order(comparer: StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(value: projectPath))
                {
                    return projectPath;
                }
            }

            if (PathEquals(left: directory, right: workspaceRoot))
            {
                return null;
            }

            directory = Directory.GetParent(path: directory)?.FullName;
        }

        return null;
    }

    private static string GetSearchRoot(string workspacePath)
    {
        var root = Path.GetFullPath(path: workspacePath);
        return File.Exists(path: root)
            ? Path.GetDirectoryName(path: root) ?? root
            : root;
    }

    private static IEnumerable<string> EnumerateProjectFiles(string root)
    {
        var pending = new Stack<string>();
        pending.Push(item: root);
        while (pending.Count > 0)
        {
            var directory = pending.Pop();
            foreach (var childDirectory in Directory.EnumerateDirectories(path: directory)
                         .Where(predicate: childDirectory => !IsIgnoredDirectory(name: Path.GetFileName(path: childDirectory))))
            {
                pending.Push(item: childDirectory);
            }

            foreach (var projectPath in Directory.EnumerateFiles(path: directory, searchPattern: "*.csproj", searchOption: SearchOption.TopDirectoryOnly))
            {
                yield return projectPath;
            }
        }
    }

    private static bool IsIgnoredDirectory(string name) =>
        name.Equals(value: ".git", comparisonType: StringComparison.OrdinalIgnoreCase)
        || name.Equals(value: "artifacts", comparisonType: StringComparison.OrdinalIgnoreCase)
        || name.Equals(value: "bin", comparisonType: StringComparison.OrdinalIgnoreCase)
        || name.Equals(value: "node_modules", comparisonType: StringComparison.OrdinalIgnoreCase)
        || name.Equals(value: "obj", comparisonType: StringComparison.OrdinalIgnoreCase);

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

    private static string[] GetProjectsFromSlnx(string solutionPath)
    {
        var solutionDirectory = Path.GetDirectoryName(path: solutionPath) ?? Environment.CurrentDirectory;
        var document = XDocument.Load(uri: solutionPath);
        return document
            .Descendants()
            .Where(predicate: element => element.Name.LocalName.Equals(value: "Project", comparisonType: StringComparison.Ordinal))
            .Select(selector: element => element.Attribute(name: "Path")?.Value)
            .Where(predicate: path => !string.IsNullOrWhiteSpace(value: path))
            .Select(selector: path => ResolveSolutionProjectPath(solutionDirectory: solutionDirectory, projectPath: path!))
            .Where(predicate: path => path.EndsWith(value: ".csproj", comparisonType: StringComparison.OrdinalIgnoreCase))
            .Where(predicate: File.Exists)
            .Order(comparer: StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string[] GetProjectsFromSln(string solutionPath)
    {
        var solutionDirectory = Path.GetDirectoryName(path: solutionPath) ?? Environment.CurrentDirectory;
#pragma warning disable SCS0018,SEC0116
        return File.ReadLines(path: solutionPath)
            .Select(selector: TryReadSlnProjectPath)
            .Where(predicate: path => !string.IsNullOrWhiteSpace(value: path))
            .Select(selector: path => ResolveSolutionProjectPath(solutionDirectory: solutionDirectory, projectPath: path!))
            .Where(predicate: path => path.EndsWith(value: ".csproj", comparisonType: StringComparison.OrdinalIgnoreCase))
            .Where(predicate: File.Exists)
            .Order(comparer: StringComparer.OrdinalIgnoreCase)
            .ToArray();
#pragma warning restore SCS0018,SEC0116
    }

    private static string? TryReadSlnProjectPath(string line)
    {
        if (!line.StartsWith(value: "Project(", comparisonType: StringComparison.Ordinal))
        {
            return null;
        }

        var parts = line.Split(separator: ',', options: StringSplitOptions.TrimEntries);
        return parts.Length >= 2
            ? parts[1].Trim(trimChar: '"')
            : null;
    }

    private static string ResolveSolutionProjectPath(
        string solutionDirectory,
        string projectPath)
    {
        var normalized = projectPath.Replace(oldChar: '\\', newChar: Path.DirectorySeparatorChar);
        return Path.GetFullPath(
            path: Path.IsPathRooted(path: normalized)
                ? normalized
                : Path.Combine(path1: solutionDirectory, path2: normalized));
    }

    private static bool IsSolutionFile(string path) =>
        path.EndsWith(value: ".sln", comparisonType: StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(value: ".slnx", comparisonType: StringComparison.OrdinalIgnoreCase);
}
