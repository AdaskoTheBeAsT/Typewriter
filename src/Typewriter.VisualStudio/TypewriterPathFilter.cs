using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Typewriter.VisualStudio;

internal static class TypewriterPathFilter
{
    private static readonly string[] ConfigurationFileNames =
    [
        "typewriter.json",
        "typewriter.config.json",
        ".typewriterrc.json",
    ];

    private static readonly IReadOnlyList<string> DefaultInputExtensions =
    [
        ".cs",
        ".csproj",
        ".json",
        ".props",
        ".sln",
        ".slnx",
        ".targets",
        ".tst",
    ];

    private static readonly IReadOnlyList<string> DefaultExcludePatterns =
    [
        "**/bin/**",
        "**/obj/**",
        "**/node_modules/**",
    ];

    private static readonly Regex SlnxPathRegex = new(
        pattern: @"<\s*(?:Project|File)\b[^>]*\bPath\s*=\s*[""']([^""']+)[""']",
        options: RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static IReadOnlyCollection<string> ReadConfiguredInputExtensions(string path)
    {
        var extensions = DefaultInputExtensions;
        foreach (var configurationPath in FindConfigurationFiles(path: path))
        {
            var configuredExtensions = ReadInputExtensions(configurationPath: configurationPath);
            if (configuredExtensions is not null)
            {
                extensions = configuredExtensions;
            }
        }

        var envExtensions = ReadEnvironmentList(variableName: "TYPEWRITER_INPUT_EXTENSIONS", normalizeExtension: true);
        if (envExtensions is not null)
        {
            extensions = envExtensions;
        }

        return extensions;
    }

    public static IReadOnlyList<string> ReadConfiguredExcludePatterns(string path)
    {
        var patterns = DefaultExcludePatterns;
        foreach (var configurationPath in FindConfigurationFiles(path: path))
        {
            var configuredPatterns = ReadExcludePatterns(configurationPath: configurationPath);
            if (configuredPatterns is not null)
            {
                patterns = configuredPatterns;
            }
        }

        var envPatterns = ReadEnvironmentList(variableName: "TYPEWRITER_EXCLUDE", normalizeExtension: false);
        if (envPatterns is not null)
        {
            patterns = envPatterns;
        }

        return patterns;
    }

    public static bool IsIgnoredInputPath(
        string path,
        string? rootDirectory = null,
        IReadOnlyCollection<string>? excludePatterns = null)
    {
        if (string.IsNullOrWhiteSpace(value: path))
        {
            return true;
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path: path);
        }
        catch (ArgumentException)
        {
            return true;
        }
        catch (IOException)
        {
            return true;
        }
        catch (NotSupportedException)
        {
            return true;
        }

        var effectiveRoot = string.IsNullOrWhiteSpace(value: rootDirectory)
            ? ResolveRootDirectory(fullPath: fullPath)
            : Path.GetFullPath(path: rootDirectory!);
        var effectiveExclude = excludePatterns ?? ReadConfiguredExcludePatterns(path: fullPath);
        return IsPathExcluded(
            rootDirectory: effectiveRoot,
            path: fullPath,
            excludePatterns: effectiveExclude);
    }

    public static bool IsPathExcluded(
        string rootDirectory,
        string path,
        IEnumerable<string> excludePatterns)
    {
        if (!TryGetRelativePath(rootDirectory: rootDirectory, path: path, relativePath: out var relativePath)
            || string.Equals(a: relativePath, b: ".", comparisonType: StringComparison.Ordinal))
        {
            return false;
        }

        var segments = relativePath.Split(separator: ['/'], options: StringSplitOptions.RemoveEmptyEntries);
        if (Array.Exists(array: segments, match: IsAlwaysIgnoredDirectoryName))
        {
            return true;
        }

        foreach (var pattern in excludePatterns)
        {
            var normalizedPattern = NormalizeExcludePattern(pattern: pattern);
            if (string.IsNullOrWhiteSpace(value: normalizedPattern))
            {
                continue;
            }

            if (IsGlobMatch(relativePath: relativePath, normalizedPattern: normalizedPattern))
            {
                return true;
            }
        }

        return false;
    }

    public static bool TryGetTemplateSearchDirectory(
        string? path,
        out string directory)
    {
        directory = string.Empty;
        if (string.IsNullOrWhiteSpace(value: path))
        {
            return false;
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path: path!);
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }

        var candidateDirectory = Directory.Exists(path: fullPath)
            ? fullPath
            : Path.GetDirectoryName(path: fullPath);
        if (string.IsNullOrWhiteSpace(value: candidateDirectory) || !Directory.Exists(path: candidateDirectory!))
        {
            return false;
        }

        directory = candidateDirectory!;
        return true;
    }

    public static bool ContainsTemplateFile(
        string directory,
        string? rootDirectory = null,
        IReadOnlyCollection<string>? excludePatterns = null)
    {
        var effectiveRoot = string.IsNullOrWhiteSpace(value: rootDirectory)
            ? directory
            : rootDirectory!;
        var effectiveExclude = excludePatterns ?? ReadConfiguredExcludePatterns(path: effectiveRoot);
        var pending = new Stack<string>();
        pending.Push(item: directory);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            try
            {
                if (Directory.EnumerateFiles(path: current, searchPattern: "*.tst", searchOption: SearchOption.TopDirectoryOnly)
                    .Any(predicate: file => !IsPathExcluded(rootDirectory: effectiveRoot, path: file, excludePatterns: effectiveExclude)))
                {
                    return true;
                }

                foreach (var childDirectory in Directory.EnumerateDirectories(path: current)
                             .Where(predicate: childDirectory => !IsExcludedDirectory(
                                 rootDirectory: effectiveRoot,
                                 directory: childDirectory,
                                 excludePatterns: effectiveExclude)))
                {
                    pending.Push(item: childDirectory);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        return false;
    }

    public static bool TryGetScopeKeyAndContainsTemplates(
        string? path,
        out string scopeKey,
        out bool containsTemplates)
    {
        scopeKey = string.Empty;
        containsTemplates = false;
        if (TryResolveSolutionFile(path: path, solutionPath: out var solutionPath))
        {
            scopeKey = solutionPath;
            containsTemplates = ContainsSolutionTemplateFile(solutionPath: solutionPath);
            return true;
        }

        if (!TryGetTemplateSearchDirectory(path: path, directory: out var searchDirectory))
        {
            return false;
        }

        scopeKey = searchDirectory;
        containsTemplates = ContainsTemplateFile(directory: searchDirectory);
        return true;
    }

    internal static string NormalizeExcludePattern(string pattern)
    {
        if (string.IsNullOrWhiteSpace(value: pattern))
        {
            return string.Empty;
        }

        var trimmed = pattern.Trim();
        var endsWithSeparator = trimmed.EndsWith(value: "/", comparisonType: StringComparison.Ordinal)
            || trimmed.EndsWith(value: "\\", comparisonType: StringComparison.Ordinal);
        var normalized = trimmed.Replace(oldChar: '\\', newChar: '/').TrimEnd(trimChars: ['/']);
        if (string.IsNullOrWhiteSpace(value: normalized))
        {
            return string.Empty;
        }

        if (normalized.IndexOf(value: '/') < 0)
        {
            return IsDirectorySegmentPattern(segment: normalized)
                ? "**/" + normalized + "/**"
                : normalized;
        }

        if (endsWithSeparator)
        {
            return normalized.EndsWith(value: "/**", comparisonType: StringComparison.Ordinal)
                ? normalized
                : normalized + "/**";
        }

        var lastSlashIndex = normalized.LastIndexOf(value: '/');
        var lastSegment = normalized.Substring(startIndex: lastSlashIndex + 1);
        return IsDirectorySegmentPattern(segment: lastSegment)
            ? normalized + "/**"
            : normalized;
    }

    private static bool ContainsSolutionTemplateFile(string solutionPath)
    {
        var solutionDirectory = Path.GetDirectoryName(path: solutionPath);
        if (string.IsNullOrWhiteSpace(value: solutionDirectory) || !Directory.Exists(path: solutionDirectory!))
        {
            return false;
        }

        var excludePatterns = ReadConfiguredExcludePatterns(path: solutionDirectory!);
        try
        {
            if (Directory.EnumerateFiles(path: solutionDirectory!, searchPattern: "*.tst", searchOption: SearchOption.TopDirectoryOnly)
                .Any(predicate: file => !IsPathExcluded(rootDirectory: solutionDirectory!, path: file, excludePatterns: excludePatterns)))
            {
                return true;
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        foreach (var referencedPath in GetSolutionReferencedPaths(solutionPath: solutionPath))
        {
            if (File.Exists(path: referencedPath))
            {
                if (referencedPath.EndsWith(value: ".tst", comparisonType: StringComparison.OrdinalIgnoreCase)
                    && !IsPathExcluded(rootDirectory: solutionDirectory!, path: referencedPath, excludePatterns: excludePatterns))
                {
                    return true;
                }

                if (referencedPath.EndsWith(value: ".csproj", comparisonType: StringComparison.OrdinalIgnoreCase))
                {
                    var projectDirectory = Path.GetDirectoryName(path: referencedPath);
                    if (!string.IsNullOrWhiteSpace(value: projectDirectory)
                        && Directory.Exists(path: projectDirectory!)
                        && !IsExcludedDirectory(rootDirectory: solutionDirectory!, directory: projectDirectory!, excludePatterns: excludePatterns)
                        && ContainsTemplateFile(directory: projectDirectory!, rootDirectory: solutionDirectory!, excludePatterns: excludePatterns))
                    {
                        return true;
                    }
                }
            }
            else if (Directory.Exists(path: referencedPath)
                     && !IsExcludedDirectory(rootDirectory: solutionDirectory!, directory: referencedPath, excludePatterns: excludePatterns)
                     && ContainsTemplateFile(directory: referencedPath, rootDirectory: solutionDirectory!, excludePatterns: excludePatterns))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryResolveSolutionFile(
        string? path,
        out string solutionPath)
    {
        solutionPath = string.Empty;
        if (string.IsNullOrWhiteSpace(value: path))
        {
            return false;
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path: path!);
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }

        if (IsSolutionFile(path: fullPath) && File.Exists(path: fullPath))
        {
            solutionPath = fullPath;
            return true;
        }

        if (!Directory.Exists(path: fullPath))
        {
            return false;
        }

        try
        {
            var solutions = Directory.EnumerateFiles(path: fullPath, searchPattern: "*", searchOption: SearchOption.TopDirectoryOnly)
                .Where(predicate: IsSolutionFile)
                .Select(selector: Path.GetFullPath)
                .ToArray();
            if (solutions.Length == 1)
            {
                solutionPath = solutions[0];
                return true;
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        return false;
    }

    private static bool IsSolutionFile(string path) =>
        path.EndsWith(value: ".sln", comparisonType: StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(value: ".slnx", comparisonType: StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<string> GetSolutionReferencedPaths(string solutionPath)
    {
        var solutionDirectory = Path.GetDirectoryName(path: solutionPath) ?? Environment.CurrentDirectory;
        var results = new List<string>();
        try
        {
            if (solutionPath.EndsWith(value: ".slnx", comparisonType: StringComparison.OrdinalIgnoreCase))
            {
                var content = File.ReadAllText(path: solutionPath);
                foreach (Match match in SlnxPathRegex.Matches(input: content))
                {
                    var candidate = match.Groups[1].Value;
                    if (!string.IsNullOrWhiteSpace(value: candidate))
                    {
                        results.Add(item: ResolveSolutionRelativePath(solutionDirectory: solutionDirectory, relativePath: candidate));
                    }
                }
            }
            else
            {
                var inSolutionItems = false;
                foreach (var rawLine in File.ReadLines(path: solutionPath))
                {
                    var line = rawLine.Trim();
                    if (line.StartsWith(value: "Project(", comparisonType: StringComparison.Ordinal))
                    {
                        var parts = line.Split(separator: ['"']);
                        if (parts.Length > 5 && !string.IsNullOrWhiteSpace(value: parts[5]))
                        {
                            results.Add(item: ResolveSolutionRelativePath(solutionDirectory: solutionDirectory, relativePath: parts[5]));
                        }
                    }
                    else if (line.StartsWith(value: "ProjectSection(SolutionItems)", comparisonType: StringComparison.Ordinal))
                    {
                        inSolutionItems = true;
                    }
                    else if (inSolutionItems && line.StartsWith(value: "EndProjectSection", comparisonType: StringComparison.Ordinal))
                    {
                        inSolutionItems = false;
                    }
                    else if (inSolutionItems)
                    {
                        var separatorIndex = line.IndexOf(value: '=');
                        var candidate = (separatorIndex >= 0 ? line.Substring(startIndex: 0, length: separatorIndex) : line).Trim().Trim(trimChars: ['"']);
                        if (!string.IsNullOrWhiteSpace(value: candidate))
                        {
                            results.Add(item: ResolveSolutionRelativePath(solutionDirectory: solutionDirectory, relativePath: candidate));
                        }
                    }
                }
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        return results
            .Distinct(comparer: StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string ResolveSolutionRelativePath(
        string solutionDirectory,
        string relativePath)
    {
        var normalized = relativePath
            .Replace(oldChar: '\\', newChar: Path.DirectorySeparatorChar)
            .Replace(oldChar: '/', newChar: Path.DirectorySeparatorChar);
        return Path.GetFullPath(path: Path.Combine(path1: solutionDirectory, path2: normalized));
    }

    private static bool IsExcludedDirectory(
        string rootDirectory,
        string directory,
        IReadOnlyCollection<string> excludePatterns)
    {
        var name = Path.GetFileName(path: directory);
        if (IsAlwaysIgnoredDirectoryName(name: name))
        {
            return true;
        }

        var probePath = Path.Combine(path1: directory, path2: "_");
        return IsPathExcluded(
            rootDirectory: rootDirectory,
            path: probePath,
            excludePatterns: excludePatterns);
    }

    private static bool IsAlwaysIgnoredDirectoryName(string name) =>
        name.Equals(value: ".git", comparisonType: StringComparison.OrdinalIgnoreCase)
        || name.Equals(value: ".gradle", comparisonType: StringComparison.OrdinalIgnoreCase)
        || name.Equals(value: ".idea", comparisonType: StringComparison.OrdinalIgnoreCase)
        || name.Equals(value: ".vs", comparisonType: StringComparison.OrdinalIgnoreCase)
        || name.Equals(value: ".vscode", comparisonType: StringComparison.OrdinalIgnoreCase);

    private static bool IsDirectorySegmentPattern(string segment) =>
        !string.IsNullOrWhiteSpace(value: segment)
        && segment.IndexOf(value: '*') < 0
        && segment.IndexOf(value: '?') < 0
        && segment.IndexOf(value: '.', startIndex: 1) < 0;

    private static bool TryGetRelativePath(
        string rootDirectory,
        string path,
        out string relativePath)
    {
        relativePath = string.Empty;
        if (string.IsNullOrWhiteSpace(value: rootDirectory) || string.IsNullOrWhiteSpace(value: path))
        {
            return false;
        }

        var fullRoot = Path.GetFullPath(path: rootDirectory)
            .TrimEnd(trimChars: [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]);
        var fullPath = Path.GetFullPath(path: path);
        if (string.Equals(a: fullPath, b: fullRoot, comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            relativePath = ".";
            return true;
        }

        var prefix = fullRoot + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(value: prefix, comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        relativePath = fullPath.Substring(startIndex: prefix.Length).Replace(oldChar: '\\', newChar: '/');
        return true;
    }

    private static bool IsGlobMatch(
        string relativePath,
        string normalizedPattern)
    {
        var regexPattern = ConvertGlobToRegex(normalizedPattern: normalizedPattern);
        return Regex.IsMatch(
            input: relativePath,
            pattern: regexPattern,
            options: RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static string ConvertGlobToRegex(string normalizedPattern)
    {
        var builder = new StringBuilder(value: "^");
        for (var index = 0; index < normalizedPattern.Length; index++)
        {
            var current = normalizedPattern[index];
            if (current == '*')
            {
                var isDoubleStar = index + 1 < normalizedPattern.Length && normalizedPattern[index + 1] == '*';
                if (isDoubleStar)
                {
                    var hasSlashAfter = index + 2 < normalizedPattern.Length && normalizedPattern[index + 2] == '/';
                    if (hasSlashAfter)
                    {
                        builder.Append(value: "(?:.*/)?");
                        index += 2;
                        continue;
                    }

                    builder.Append(value: ".*");
                    index++;
                    continue;
                }

                builder.Append(value: "[^/]*");
                continue;
            }

            if (current == '?')
            {
                builder.Append(value: "[^/]");
                continue;
            }

            builder.Append(value: Regex.Escape(str: current.ToString()));
        }

        builder.Append(value: "$");
        return builder.ToString();
    }

    private static string ResolveRootDirectory(string fullPath)
    {
        var startDirectory = File.Exists(path: fullPath) || !Directory.Exists(path: fullPath)
            ? Path.GetDirectoryName(path: fullPath)
            : fullPath;
        if (string.IsNullOrWhiteSpace(value: startDirectory))
        {
            return Path.GetPathRoot(path: fullPath) ?? fullPath;
        }

        string? nearestProjectDirectory = null;
        string? nearestWorkspaceDirectory = null;
        var current = startDirectory;
        while (!string.IsNullOrWhiteSpace(value: current))
        {
            if (Directory.Exists(path: current!))
            {
                try
                {
                    if (nearestProjectDirectory is null
                        && Directory.EnumerateFiles(path: current!, searchPattern: "*.csproj", searchOption: SearchOption.TopDirectoryOnly).Any())
                    {
                        nearestProjectDirectory = current;
                    }

                    var hasConfig = ConfigurationFileNames.Any(predicate: name => File.Exists(path: Path.Combine(path1: current!, path2: name)));
                    var hasSolution = Directory.EnumerateFiles(path: current!, searchPattern: "*", searchOption: SearchOption.TopDirectoryOnly)
                        .Any(predicate: IsSolutionFile);
                    var hasGit = Directory.Exists(path: Path.Combine(path1: current!, path2: ".git"))
                        || File.Exists(path: Path.Combine(path1: current!, path2: ".git"));
                    if (hasConfig || hasSolution || hasGit)
                    {
                        nearestWorkspaceDirectory = current;
                        if (hasSolution || hasGit)
                        {
                            break;
                        }
                    }
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }

            current = Directory.GetParent(path: current!)?.FullName;
        }

        return nearestWorkspaceDirectory
            ?? nearestProjectDirectory
            ?? Path.GetPathRoot(path: fullPath)
            ?? startDirectory!;
    }

    private static IEnumerable<string> FindConfigurationFiles(string path)
    {
        if (string.IsNullOrWhiteSpace(value: path))
        {
            yield break;
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path: path);
        }
        catch (ArgumentException)
        {
            yield break;
        }
        catch (IOException)
        {
            yield break;
        }
        catch (NotSupportedException)
        {
            yield break;
        }

        var directory = Directory.Exists(path: fullPath)
            ? fullPath
            : Path.GetDirectoryName(path: fullPath);
        var directories = new List<string>();
        while (!string.IsNullOrWhiteSpace(value: directory))
        {
            directories.Add(item: directory!);
            directory = Directory.GetParent(path: directory!)?.FullName;
        }

        directories.Reverse();
        foreach (var candidateDirectory in directories)
        {
            foreach (var configurationName in ConfigurationFileNames)
            {
                var configurationPath = Path.Combine(path1: candidateDirectory, path2: configurationName);
                if (File.Exists(path: configurationPath))
                {
                    yield return configurationPath;
                }
            }
        }
    }

    private static IReadOnlyList<string>? ReadInputExtensions(string configurationPath)
    {
        try
        {
            using var document = JsonDocument.Parse(json: File.ReadAllText(path: configurationPath));
            if (!TryGetProperty(element: document.RootElement, name: "inputExtensions", value: out var inputExtensions)
                || inputExtensions.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var extensions = inputExtensions
                .EnumerateArray()
                .Where(predicate: element => element.ValueKind == JsonValueKind.String)
                .Select(selector: element => element.GetString() ?? string.Empty)
                .Select(selector: NormalizeExtension)
                .Where(predicate: extension => !string.IsNullOrWhiteSpace(value: extension))
                .Distinct(comparer: StringComparer.OrdinalIgnoreCase)
                .ToArray();
            return extensions.Length == 0 ? DefaultInputExtensions : extensions;
        }
        catch (IOException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static IReadOnlyList<string>? ReadExcludePatterns(string configurationPath)
    {
        try
        {
            using var document = JsonDocument.Parse(json: File.ReadAllText(path: configurationPath));
            if (!TryGetProperty(element: document.RootElement, name: "exclude", value: out var excludeElement)
                || excludeElement.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            return excludeElement
                .EnumerateArray()
                .Where(predicate: element => element.ValueKind == JsonValueKind.String)
                .Select(selector: element => (element.GetString() ?? string.Empty).Trim())
                .Where(predicate: pattern => !string.IsNullOrWhiteSpace(value: pattern))
                .Distinct(comparer: StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (IOException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static IReadOnlyList<string>? ReadEnvironmentList(
        string variableName,
        bool normalizeExtension)
    {
        var rawValue = Environment.GetEnvironmentVariable(variable: variableName);
        if (string.IsNullOrWhiteSpace(value: rawValue))
        {
            return null;
        }

        var items = rawValue!
            .Split(separator: [',', ';', ' '], options: StringSplitOptions.RemoveEmptyEntries)
            .Select(selector: item => normalizeExtension ? NormalizeExtension(extension: item) : item.Trim())
            .Where(predicate: item => !string.IsNullOrWhiteSpace(value: item))
            .Distinct(comparer: StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return items.Length == 0 ? null : items;
    }

    private static bool TryGetProperty(
        JsonElement element,
        string name,
        out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (property.NameEquals(text: name)
                || property.Name.Equals(value: name, comparisonType: StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static string NormalizeExtension(string extension)
    {
        var trimmed = extension.Trim();
        if (string.IsNullOrWhiteSpace(value: trimmed))
        {
            return string.Empty;
        }

        return trimmed.StartsWith(value: ".", comparisonType: StringComparison.Ordinal)
            ? trimmed
            : "." + trimmed;
    }
}
