using Typewriter.Abstractions;

namespace Typewriter.Engine;

public sealed class FileSystemTemplateDiscovery : ITemplateDiscovery
{
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
#pragma warning disable SCS0018 // Potential Path Traversal vulnerability was found where '{0}' in '{1}' may be tainted by user-controlled data from '{2}' in method '{3}'.
            var content = await File.ReadAllTextAsync(path: templatePath, cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
#pragma warning restore SCS0018 // Potential Path Traversal vulnerability was found where '{0}' in '{1}' may be tainted by user-controlled data from '{2}' in method '{3}'.

            return [new TemplateFile(Path: templatePath, Content: content)];
        }

        var root = Path.GetFullPath(path: workspace.RootPath);
        if (File.Exists(path: root))
        {
            root = Path.GetDirectoryName(path: root) ?? root;
        }

        var includePatterns = request.Configuration.Templates.Count == 0
            ? TypewriterConfiguration.Default.Templates
            : request.Configuration.Templates;
        var includeMatchers = includePatterns.Select(selector: pattern => PathGlob.CreateMatcher(root: root, pattern: pattern)).ToArray();
        var excludeMatchers = request.Configuration.Exclude.Select(selector: pattern => PathGlob.CreateMatcher(root: root, pattern: pattern)).ToArray();
        var files = EnumerateFiles(root: root, cancellationToken: cancellationToken)
            .Where(predicate: path => includeMatchers.Any(predicate: matcher => matcher.IsMatch(path: path)))
            .Where(predicate: path => !IsExcluded(path: path, excludeMatchers: excludeMatchers))
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

    private static IEnumerable<string> EnumerateFiles(
        string root,
        CancellationToken cancellationToken)
    {
        var pending = new Stack<string>();
        pending.Push(item: root);
        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var directory = pending.Pop();
            foreach (var childDirectory in Directory.EnumerateDirectories(path: directory)
                         .Where(predicate: childDirectory => !IsIgnoredDirectoryName(name: Path.GetFileName(path: childDirectory))))
            {
                pending.Push(item: childDirectory);
            }

            foreach (var file in Directory.EnumerateFiles(path: directory))
            {
                yield return file;
            }
        }
    }

    private static bool IsIgnoredDirectoryName(string name) =>
        name.Equals(value: ".git", comparisonType: StringComparison.OrdinalIgnoreCase)
        || name.Equals(value: "bin", comparisonType: StringComparison.OrdinalIgnoreCase)
        || name.Equals(value: "obj", comparisonType: StringComparison.OrdinalIgnoreCase)
        || name.Equals(value: "node_modules", comparisonType: StringComparison.OrdinalIgnoreCase);

    private static bool IsExcluded(
        string path,
        IReadOnlyList<PathGlobMatcher> excludeMatchers)
    {
        var segments = Path.GetFullPath(path: path)
            .Split(
                separator: [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                options: StringSplitOptions.RemoveEmptyEntries);

        return segments.Any(predicate: IsIgnoredDirectoryName)
            || excludeMatchers.Any(predicate: matcher => matcher.IsMatch(path: path));
    }
}
