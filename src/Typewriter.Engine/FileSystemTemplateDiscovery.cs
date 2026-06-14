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
        var files = Directory.EnumerateFiles(path: root, searchPattern: "*", searchOption: SearchOption.AllDirectories)
            .Where(predicate: path => includePatterns.Any(predicate: pattern => PathGlob.IsMatch(root: root, path: path, pattern: pattern)))
            .Where(predicate: path => !IsExcluded(root: root, path: path, excludePatterns: request.Configuration.Exclude))
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

    private static bool IsExcluded(
        string root,
        string path,
        IReadOnlyList<string> excludePatterns)
    {
        var segments = Path.GetFullPath(path: path)
            .Split(
                separator: [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                options: StringSplitOptions.RemoveEmptyEntries);

        return segments.Any(
            predicate: segment => segment.Equals(value: "bin", comparisonType: StringComparison.OrdinalIgnoreCase)
                                  || segment.Equals(value: "obj", comparisonType: StringComparison.OrdinalIgnoreCase)
                                  || segment.Equals(value: "node_modules", comparisonType: StringComparison.OrdinalIgnoreCase))
            || excludePatterns.Any(predicate: pattern => PathGlob.IsMatch(root: root, path: path, pattern: pattern));
    }
}
