using Typewriter.Abstractions;

namespace Typewriter.Engine;

public sealed class FileSystemTemplateDiscovery : ITemplateDiscovery
{
    public static bool IsPathExcluded(
        string rootDirectory,
        string path,
        IEnumerable<string> excludePatterns)
    {
        ArgumentNullException.ThrowIfNull(argument: rootDirectory);
        ArgumentNullException.ThrowIfNull(argument: path);
        ArgumentNullException.ThrowIfNull(argument: excludePatterns);

        var fullRoot = Path.GetFullPath(path: rootDirectory);
        var fullPath = Path.GetFullPath(path: path);
        var relativePath = Path.GetRelativePath(relativeTo: fullRoot, path: fullPath);
        if (string.Equals(a: relativePath, b: ".", comparisonType: StringComparison.Ordinal)
            || relativePath.StartsWith(value: "..", comparisonType: StringComparison.Ordinal)
            || Path.IsPathRooted(path: relativePath))
        {
            return false;
        }

        var matchers = CreateFileExcludeMatchers(root: fullRoot, excludePatterns: excludePatterns);
        return IsExcluded(root: fullRoot, path: fullPath, excludeMatchers: matchers);
    }

    public async Task<IReadOnlyList<TemplateFile>> FindTemplatesAsync(
        WorkspaceContext workspace,
        GenerationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(argument: workspace);
        ArgumentNullException.ThrowIfNull(argument: request);

        if (!string.IsNullOrWhiteSpace(value: request.TemplatePath))
        {
            var templatePath = Path.GetFullPath(path: request.TemplatePath);
            if (Directory.Exists(path: templatePath))
            {
                return await FindTemplatesUnderRootAsync(
                    root: templatePath,
                    solutionPath: null,
                    request: request,
                    cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
            }

#pragma warning disable SCS0018 // Potential Path Traversal vulnerability was found where '{0}' in '{1}' may be tainted by user-controlled data from '{2}' in method '{3}'.
            var content = await File.ReadAllTextAsync(path: templatePath, cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
#pragma warning restore SCS0018 // Potential Path Traversal vulnerability was found where '{0}' in '{1}' may be tainted by user-controlled data from '{2}' in method '{3}'.

            return [new TemplateFile(Path: templatePath, Content: content)];
        }

        var root = Path.GetFullPath(path: workspace.RootPath);
        var solutionPath = WorkspaceProjectResolver.TryResolveSolutionFile(workspacePath: root, solutionPath: out var resolvedSolution)
            ? resolvedSolution
            : null;
        if (File.Exists(path: root))
        {
            root = Path.GetDirectoryName(path: root) ?? root;
        }

        return await FindTemplatesUnderRootAsync(
            root: root,
            solutionPath: solutionPath,
            request: request,
            cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
    }

    internal static bool HasTemplates(
        WorkspaceContext workspace,
        GenerationRequest request)
    {
        if (!string.IsNullOrWhiteSpace(value: request.TemplatePath))
        {
            var templatePath = Path.GetFullPath(path: request.TemplatePath);
            if (File.Exists(path: templatePath))
            {
                return true;
            }

            return Directory.Exists(path: templatePath)
                && EnumerateMatchingTemplatePaths(
                    root: templatePath,
                    solutionPath: null,
                    request: request,
                    cancellationToken: CancellationToken.None).Any();
        }

        var root = Path.GetFullPath(path: workspace.RootPath);
        var solutionPath = WorkspaceProjectResolver.TryResolveSolutionFile(workspacePath: root, solutionPath: out var resolvedSolution)
            ? resolvedSolution
            : null;
        if (File.Exists(path: root))
        {
            root = Path.GetDirectoryName(path: root) ?? root;
        }

        return Directory.Exists(path: root)
            && EnumerateMatchingTemplatePaths(
                root: root,
                solutionPath: solutionPath,
                request: request,
                cancellationToken: CancellationToken.None).Any();
    }

    internal static string NormalizeExcludePattern(string pattern)
    {
        if (string.IsNullOrWhiteSpace(value: pattern))
        {
            return string.Empty;
        }

        var trimmed = pattern.Trim();
        var endsWithSeparator = trimmed.EndsWith(value: '/')
            || trimmed.EndsWith(value: '\\');
        var normalized = trimmed.Replace(oldChar: '\\', newChar: '/').TrimEnd(trimChar: '/');
        if (string.IsNullOrWhiteSpace(value: normalized))
        {
            return string.Empty;
        }

        if (!normalized.Contains(value: '/', comparisonType: StringComparison.Ordinal))
        {
            return IsDirectorySegmentPattern(segment: normalized)
                ? $"**/{normalized}/**"
                : normalized;
        }

        if (endsWithSeparator)
        {
            return normalized.EndsWith(value: "/**", comparisonType: StringComparison.Ordinal)
                ? normalized
                : normalized + "/**";
        }

        var lastSlashIndex = normalized.LastIndexOf(value: '/');
        var lastSegment = normalized[(lastSlashIndex + 1)..];
        return IsDirectorySegmentPattern(segment: lastSegment)
            ? normalized + "/**"
            : normalized;
    }

    private static bool IsDirectorySegmentPattern(string segment) =>
        !string.IsNullOrWhiteSpace(value: segment)
        && !segment.Contains(value: '*', comparisonType: StringComparison.Ordinal)
        && !segment.Contains(value: '?', comparisonType: StringComparison.Ordinal)
        && segment.IndexOf(value: '.', startIndex: 1) < 0;

    private static async Task<IReadOnlyList<TemplateFile>> FindTemplatesUnderRootAsync(
        string root,
        string? solutionPath,
        GenerationRequest request,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(path: root))
        {
            return [];
        }

        var files = EnumerateMatchingTemplatePaths(
                root: root,
                solutionPath: solutionPath,
                request: request,
                cancellationToken: cancellationToken)
            .Order(comparer: StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var templates = new List<TemplateFile>(capacity: files.Length);
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
#pragma warning disable SCS0018 // Potential Path Traversal vulnerability was found where '{0}' in '{1}' may be tainted by user-controlled data from '{2}' in method '{3}'.
            var content = await File.ReadAllTextAsync(path: file, cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
#pragma warning restore SCS0018 // Potential Path Traversal vulnerability was found where '{0}' in '{1}' may be tainted by user-controlled data from '{2}' in method '{3}'.
            templates.Add(item: new TemplateFile(Path: Path.GetFullPath(path: file), Content: content));
        }

        return templates;
    }

    private static IEnumerable<string> EnumerateMatchingTemplatePaths(
        string root,
        string? solutionPath,
        GenerationRequest request,
        CancellationToken cancellationToken)
    {
        var includePatterns = request.Configuration.Templates.Count == 0
            ? TypewriterConfiguration.Default.Templates
            : request.Configuration.Templates;
        var includeMatchers = includePatterns
            .Select(selector: pattern => PathGlob.CreateMatcher(root: root, pattern: pattern))
            .ToArray();
        var fileExcludeMatchers = CreateFileExcludeMatchers(root: root, excludePatterns: request.Configuration.Exclude);
        var directoryExcludeMatchers = CreateDirectoryExcludeMatchers(root: root, excludePatterns: request.Configuration.Exclude);

        return EnumerateCandidateFiles(
                root: root,
                solutionPath: solutionPath,
                includePatterns: includePatterns,
                directoryExcludeMatchers: directoryExcludeMatchers,
                cancellationToken: cancellationToken)
            .Where(predicate: path => includeMatchers.Any(predicate: matcher => matcher.IsMatch(path: path)))
            .Where(predicate: path => !IsExcluded(root: root, path: path, excludeMatchers: fileExcludeMatchers));
    }

    private static PathGlobMatcher[] CreateFileExcludeMatchers(
        string root,
        IEnumerable<string> excludePatterns) =>
        excludePatterns
            .Select(selector: NormalizeExcludePattern)
            .Where(predicate: pattern => !string.IsNullOrWhiteSpace(value: pattern))
            .Distinct(comparer: StringComparer.OrdinalIgnoreCase)
            .Select(selector: pattern => PathGlob.CreateMatcher(root: root, pattern: pattern))
            .ToArray();

    private static PathGlobMatcher[] CreateDirectoryExcludeMatchers(
        string root,
        IEnumerable<string> excludePatterns) =>
        excludePatterns
            .Select(selector: NormalizeExcludePattern)
            .Where(predicate: pattern => pattern.EndsWith(value: "/**", comparisonType: StringComparison.Ordinal))
            .Distinct(comparer: StringComparer.OrdinalIgnoreCase)
            .Select(selector: pattern => PathGlob.CreateMatcher(root: root, pattern: pattern))
            .ToArray();

    private static IEnumerable<string> EnumerateCandidateFiles(
        string root,
        string? solutionPath,
        IReadOnlyList<string> includePatterns,
        IReadOnlyList<PathGlobMatcher> directoryExcludeMatchers,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(value: solutionPath))
        {
            return EnumerateFiles(
                root: root,
                directoryExcludeMatchers: directoryExcludeMatchers,
                cancellationToken: cancellationToken);
        }

        return EnumerateSolutionCandidateFiles(
                root: root,
                solutionPath: solutionPath,
                includePatterns: includePatterns,
                directoryExcludeMatchers: directoryExcludeMatchers,
                cancellationToken: cancellationToken)
            .Distinct(comparer: StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> EnumerateSolutionCandidateFiles(
        string root,
        string solutionPath,
        IReadOnlyList<string> includePatterns,
        IReadOnlyList<PathGlobMatcher> directoryExcludeMatchers,
        CancellationToken cancellationToken)
    {
        foreach (var file in Directory.EnumerateFiles(path: root, searchPattern: "*", searchOption: SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return Path.GetFullPath(path: file);
        }

        foreach (var itemPath in WorkspaceProjectResolver.GetSolutionItemsFromSolutionFile(solutionPath: solutionPath))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(path: itemPath))
            {
                yield return Path.GetFullPath(path: itemPath);
                continue;
            }

            if (Directory.Exists(path: itemPath)
                && !IsExcludedDirectory(directory: itemPath, directoryExcludeMatchers: directoryExcludeMatchers))
            {
                foreach (var file in EnumerateFiles(root: itemPath, directoryExcludeMatchers: directoryExcludeMatchers, cancellationToken: cancellationToken))
                {
                    yield return Path.GetFullPath(path: file);
                }
            }
        }

        var searchDirectories = new HashSet<string>(comparer: StringComparer.OrdinalIgnoreCase);
        foreach (var projectPath in WorkspaceProjectResolver.GetProjectsFromSolutionFile(solutionPath: solutionPath))
        {
            var projectDirectory = Path.GetDirectoryName(path: projectPath);
            if (!string.IsNullOrWhiteSpace(value: projectDirectory) && Directory.Exists(path: projectDirectory))
            {
                searchDirectories.Add(item: Path.GetFullPath(path: projectDirectory));
            }
        }

        foreach (var includeDirectory in GetExplicitIncludeDirectories(root: root, includePatterns: includePatterns))
        {
            searchDirectories.Add(item: includeDirectory);
        }

        foreach (var searchDirectory in searchDirectories.Order(comparer: StringComparer.OrdinalIgnoreCase))
        {
            if (IsExcludedDirectory(directory: searchDirectory, directoryExcludeMatchers: directoryExcludeMatchers))
            {
                continue;
            }

            foreach (var file in EnumerateFiles(root: searchDirectory, directoryExcludeMatchers: directoryExcludeMatchers, cancellationToken: cancellationToken))
            {
                yield return Path.GetFullPath(path: file);
            }
        }
    }

    private static IEnumerable<string> GetExplicitIncludeDirectories(
        string root,
        IReadOnlyList<string> includePatterns)
    {
        foreach (var pattern in includePatterns)
        {
            if (string.IsNullOrWhiteSpace(value: pattern))
            {
                continue;
            }

            var normalized = pattern.Trim().Replace(oldChar: '\\', newChar: '/').TrimStart(trimChar: '/');
            var slashIndex = normalized.IndexOf(value: '/', comparisonType: StringComparison.Ordinal);
            if (slashIndex <= 0)
            {
                continue;
            }

            var firstSegment = normalized[..slashIndex];
            if (firstSegment.Contains(value: '*', comparisonType: StringComparison.Ordinal)
                || firstSegment.Contains(value: '?', comparisonType: StringComparison.Ordinal)
                || firstSegment.Equals(value: ".", comparisonType: StringComparison.Ordinal)
                || firstSegment.Equals(value: "..", comparisonType: StringComparison.Ordinal))
            {
                continue;
            }

            var candidateDirectory = Path.GetFullPath(path: Path.Combine(path1: root, path2: firstSegment));
            if (Directory.Exists(path: candidateDirectory))
            {
                yield return candidateDirectory;
            }
        }
    }

    private static IEnumerable<string> EnumerateFiles(
        string root,
        IReadOnlyList<PathGlobMatcher> directoryExcludeMatchers,
        CancellationToken cancellationToken)
    {
        var pending = new Stack<string>();
        pending.Push(item: root);
        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var directory = pending.Pop();
            foreach (var childDirectory in Directory.EnumerateDirectories(path: directory)
                         .Where(predicate: childDirectory => !IsExcludedDirectory(directory: childDirectory, directoryExcludeMatchers: directoryExcludeMatchers)))
            {
                pending.Push(item: childDirectory);
            }

            foreach (var file in Directory.EnumerateFiles(path: directory))
            {
                yield return file;
            }
        }
    }

    private static bool IsExcludedDirectory(
        string directory,
        IReadOnlyList<PathGlobMatcher> directoryExcludeMatchers)
    {
        var name = Path.GetFileName(path: directory);
        if (IsAlwaysIgnoredDirectoryName(name: name))
        {
            return true;
        }

        var probePath = Path.Combine(path1: directory, path2: "_");
        return directoryExcludeMatchers.Any(predicate: matcher => matcher.IsMatch(path: probePath));
    }

    private static bool IsAlwaysIgnoredDirectoryName(string name) =>
        name.Equals(value: ".git", comparisonType: StringComparison.OrdinalIgnoreCase);

    private static bool IsExcluded(
        string root,
        string path,
        IReadOnlyList<PathGlobMatcher> excludeMatchers)
    {
        var relativePath = Path.GetRelativePath(relativeTo: root, path: Path.GetFullPath(path: path));
        if (!relativePath.StartsWith(value: "..", comparisonType: StringComparison.Ordinal)
            && !Path.IsPathRooted(path: relativePath))
        {
            var segments = relativePath.Split(
                separator: [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                options: StringSplitOptions.RemoveEmptyEntries);
            if (segments.Any(predicate: IsAlwaysIgnoredDirectoryName))
            {
                return true;
            }
        }

        return excludeMatchers.Any(predicate: matcher => matcher.IsMatch(path: path));
    }
}
