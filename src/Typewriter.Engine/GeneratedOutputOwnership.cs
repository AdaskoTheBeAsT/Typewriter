using System.Security.Cryptography;
using System.Text.Json;
using Typewriter.Abstractions;

namespace Typewriter.Engine;

internal sealed class GeneratedOutputOwnership : IDisposable
{
    internal const string StateDirectoryName = ".typewriter-outputs";
    private const string ManifestName = "ownership-v1.json";
    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    private readonly string _root;
    private readonly string _manifestPath;
    private readonly string _scope;
    private readonly FileStream _lockFile;
#pragma warning disable IDE0028 // Keep the explicit filesystem path comparer.
    private readonly Dictionary<string, bool> _observations = new(comparer: PathComparer);
    private readonly HashSet<string> _legacyPaths = new(comparer: PathComparer);
#pragma warning restore IDE0028

    private GeneratedOutputOwnership(string root, string manifestPath, string scope, FileStream lockFile)
    {
        _root = root;
        _manifestPath = manifestPath;
        _scope = scope;
        _lockFile = lockFile;
    }

    public static async Task<GeneratedOutputOwnership?> OpenAsync(
        string workspacePath,
        IReadOnlyList<string> projectPaths,
        GenerationRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var root = File.Exists(path: workspacePath) ? Path.GetDirectoryName(path: workspacePath) : workspacePath;
        if (root is null || !Directory.Exists(path: root))
        {
            return null;
        }

        root = Path.TrimEndingDirectorySeparator(path: root);
        var stateDirectory = Path.Combine(path1: root, path2: StateDirectoryName);
        var lockPath = Path.Combine(path1: stateDirectory, path2: "generation.lock");
        var scope = GetScope(workspacePath: workspacePath, root: root, projectPaths: projectPaths, request: request);
        try
        {
            // Writers in the same workspace must not render and prune concurrently.
            if (!HasNoReparsePoints(path: lockPath, root: root))
            {
                return null;
            }

            _ = Directory.CreateDirectory(path: stateDirectory);
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
#pragma warning disable SCS0018,SEC0112 // Fixed filename under the link-checked workspace state directory.
                    var lockFile = new FileStream(path: lockPath, mode: FileMode.OpenOrCreate, access: FileAccess.ReadWrite, share: FileShare.None);
#pragma warning restore SCS0018,SEC0112
                    return new GeneratedOutputOwnership(
                        root: root,
                        manifestPath: Path.Combine(path1: stateDirectory, path2: ManifestName),
                        scope: scope,
                        lockFile: lockFile);
                }
                catch (IOException ex) when (IsLockContention(exception: ex))
                {
                    await Task.Delay(millisecondsDelay: 100, cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
                }
            }
        }
        catch (Exception ex) when (IsFileSystemError(exception: ex))
        {
            return null;
        }
    }

    public static bool IsStateDirectoryName(string name) =>
        name.Equals(value: StateDirectoryName, comparisonType: StringComparison.OrdinalIgnoreCase);

    public void Dispose() => _lockFile.Dispose();

    public void ConsiderLegacyPath(string path, string templatePath)
    {
        var relativePath = Path.GetRelativePath(relativeTo: _root, path: path);
        if (!PathComparer.Equals(x: path, y: templatePath) && IsSafeRelativePath(path: relativePath))
        {
            _ = _legacyPaths.Add(item: relativePath);
        }
    }

    public void Observe(string path, bool perSourceFile)
    {
        var relativePath = Path.GetRelativePath(relativeTo: _root, path: path);
        if (!IsSafeRelativePath(path: relativePath))
        {
            return;
        }

        _observations[relativePath] = perSourceFile
            && (!_observations.TryGetValue(key: relativePath, value: out var earlierPerSourceFile) || earlierPerSourceFile);
    }

    public async Task<IReadOnlyList<string>> ReconcileAsync(bool canPrune, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entries = await LoadAsync(cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        if (entries is null)
        {
            return [];
        }

        var stale = new List<string>();
        var changed = MergeObservations(entries: entries);
        if (canPrune)
        {
            // For older outputs with no record, only consider paths derived from a
            // source and a template that just rendered no items without a filename factory.
            stale.AddRange(collection: _legacyPaths.Where(predicate: path => !entries.ContainsKey(key: path)));

            changed |= ReleaseStalePaths(entries: entries, stale: stale);
        }

        // No deletion if the ownership state cannot be committed.
        if (changed && !await SaveAsync(entries: entries, cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false))
        {
            return [];
        }

        var deleted = new List<string>();
        foreach (var relativePath in stale)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = Path.Combine(path1: _root, path2: relativePath);
            if (await TryDeleteAsync(path: path, cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false))
            {
                deleted.Add(item: path);
            }
        }

        return deleted;
    }

    private static string GetScope(string workspacePath, string root, IReadOnlyList<string> projectPaths, GenerationRequest request)
    {
        var templateSearchPath = string.IsNullOrWhiteSpace(value: request.TemplateSearchPath)
            ? null
            : Path.GetFullPath(path: request.TemplateSearchPath, basePath: root);
        var scope = new
        {
            Workspace = GetScopePath(root: root, path: workspacePath),
            Projects = projectPaths.Select(selector: path => GetScopePath(root: root, path: path)).Order(comparer: StringComparer.Ordinal),
            Template = string.IsNullOrWhiteSpace(value: request.TemplatePath)
                ? null
                : GetScopePath(root: root, path: Path.GetFullPath(path: request.TemplatePath)),
            TemplateSearch = templateSearchPath is null ? null : GetScopePath(root: root, path: templateSearchPath),
            request.Configuration.Templates,
            request.Configuration.Exclude,
            request.Configuration.DefaultTargetFramework,
        };
        return Convert.ToHexString(inArray: SHA256.HashData(source: JsonSerializer.SerializeToUtf8Bytes(value: scope)));
    }

    private static string GetScopePath(string root, string path)
    {
        var relative = Path.GetRelativePath(relativeTo: root, path: Path.TrimEndingDirectorySeparator(path: path));
        return OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? relative.ToUpperInvariant() : relative;
    }

    private static bool IsLockContention(IOException exception) =>
        OperatingSystem.IsWindows() ? (exception.HResult & 0xffff) is 32 or 33 : (exception.HResult & 0xffff) is 11 or 35;

    private static bool IsFileSystemError(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException;

    private static bool IsSafeRelativePath(string path) =>
        !Path.IsPathRooted(path: path)
        && path.Split(separator: [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], options: StringSplitOptions.None)
            .All(predicate: segment => segment.Length > 0
                && segment is not "." and not ".."
                && !segment.Equals(value: ".git", comparisonType: StringComparison.OrdinalIgnoreCase)
                && !IsStateDirectoryName(name: segment)
                && (!OperatingSystem.IsWindows() || (!segment.Contains(value: ':', comparisonType: StringComparison.Ordinal)
                    && !segment.EndsWith(value: '.') && !segment.EndsWith(value: ' ')))
                && segment.IndexOfAny(anyOf: Path.GetInvalidFileNameChars()) < 0);

    private static bool HasNoReparsePoints(string path, string root)
    {
        try
        {
            for (var current = path; current is not null; current = Path.GetDirectoryName(path: current))
            {
                if (TryGetAttributes(path: current) is { } attributes && attributes.HasFlag(flag: FileAttributes.ReparsePoint))
                {
                    return false;
                }

                if (PathComparer.Equals(x: current, y: root))
                {
                    return true;
                }
            }

            return false;
        }
        catch (Exception ex) when (IsFileSystemError(exception: ex))
        {
            return false;
        }
    }

    private static FileAttributes? TryGetAttributes(string path)
    {
        try
        {
            return File.GetAttributes(path: path);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
    }

    private async Task<bool> TryDeleteAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(path: path) || !HasNoReparsePoints(path: path, root: _root))
            {
                return false;
            }

#pragma warning disable SCS0018,SEC0112,SEC0116 // The path is workspace-relative and checked for links; deletion also requires the header.
            var stream = new FileStream(path: path, mode: FileMode.Open, access: FileAccess.Read, share: FileShare.Read | FileShare.Delete);
            await using var streamScope = stream.ConfigureAwait(continueOnCapturedContext: false);
            using var input = new StreamReader(stream: stream, leaveOpen: true);

            // Two extra characters for CRLF within the header, one to verify the final line boundary.
            var prefix = new char[GeneratedFileHeader.Value.Length + 3];
            var read = await input.ReadBlockAsync(buffer: prefix, cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
            if (!GeneratedFilePlanner.HasGeneratedHeader(content: prefix.AsSpan(start: 0, length: read)))
            {
                return false;
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (!HasNoReparsePoints(path: path, root: _root))
            {
                return false;
            }

            File.Delete(path: path);
#pragma warning restore SCS0018,SEC0112,SEC0116
            return true;
        }
        catch (Exception ex) when (IsFileSystemError(exception: ex))
        {
            return false;
        }
    }

    private bool MergeObservations(Dictionary<string, Entry> entries)
    {
        var changed = false;
        foreach (var (path, observedPerSourceFile) in _observations)
        {
            _ = entries.TryGetValue(key: path, value: out var entry);
            var perSourceFile = observedPerSourceFile && entry?.PerSourceFile != false;
            if (entry is not null && entry.Scopes.Contains(value: _scope, comparer: StringComparer.Ordinal))
            {
                if (entry.PerSourceFile != perSourceFile)
                {
                    entries[path] = entry with { PerSourceFile = perSourceFile };
                    changed = true;
                }

                continue;
            }

            entries[path] = new Entry(PerSourceFile: perSourceFile, Scopes: entry is null ? [_scope] : [..entry.Scopes, _scope]);
            changed = true;
        }

        return changed;
    }

    private bool ReleaseStalePaths(Dictionary<string, Entry> entries, List<string> stale)
    {
        // An absence is only evidence against the scope that just ran in full.
        var changed = false;
        foreach (var (path, entry) in entries.ToArray())
        {
            if (_observations.ContainsKey(key: path) || !entry.Scopes.Contains(value: _scope, comparer: StringComparer.Ordinal))
            {
                continue;
            }

            changed = true;
            if (entry.Scopes.Length == 1)
            {
                _ = entries.Remove(key: path);
                if (entry.PerSourceFile)
                {
                    stale.Add(item: path);
                }
            }
            else
            {
                entries[path] = entry with
                {
                    Scopes = entry.Scopes.Where(predicate: scope => !string.Equals(a: scope, b: _scope, comparisonType: StringComparison.Ordinal)).ToArray(),
                };
            }
        }

        return changed;
    }

    private async Task<Dictionary<string, Entry>?> LoadAsync(CancellationToken cancellationToken)
    {
        if (!HasNoReparsePoints(path: _manifestPath, root: _root))
        {
            return null;
        }

        try
        {
#pragma warning disable SCS0018,SEC0116 // Fixed filename under the link-checked workspace state directory.
            var input = File.OpenRead(path: _manifestPath);
#pragma warning restore SCS0018,SEC0116
            await using (input.ConfigureAwait(continueOnCapturedContext: false))
            {
                var entries = await JsonSerializer.DeserializeAsync<Dictionary<string, Entry>>(
                    utf8Json: input,
                    cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
                if (entries is null || entries.Any(predicate: pair => !IsSafeRelativePath(path: pair.Key)
                    || pair.Value is not { Scopes.Length: > 0 }))
                {
                    return null;
                }

                return new Dictionary<string, Entry>(dictionary: entries, comparer: PathComparer);
            }
        }
        catch (FileNotFoundException)
        {
#pragma warning disable IDE0028 // Keep the explicit filesystem path comparer.
            return new Dictionary<string, Entry>(comparer: PathComparer);
#pragma warning restore IDE0028
        }
        catch (Exception ex) when (IsFileSystemError(exception: ex) || ex is JsonException)
        {
            return null;
        }
    }

    private async Task<bool> SaveAsync(Dictionary<string, Entry> entries, CancellationToken cancellationToken)
    {
        if (!HasNoReparsePoints(path: _manifestPath, root: _root))
        {
            return false;
        }

        var temporaryPath = _manifestPath + "." + Guid.NewGuid().ToString(format: "N") + ".tmp";
        var temporaryFileExists = false;
#pragma warning disable SCS0018,SEC0112,SEC0116 // Only the fixed state filename and a newly allocated sibling temp file are modified.
        try
        {
            var output = new FileStream(path: temporaryPath, mode: FileMode.CreateNew, access: FileAccess.Write);
            temporaryFileExists = true;
            await using (output.ConfigureAwait(continueOnCapturedContext: false))
            {
                await JsonSerializer.SerializeAsync(
                    utf8Json: output,
                    value: entries,
                    cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(sourceFileName: temporaryPath, destFileName: _manifestPath, overwrite: true);
            temporaryFileExists = false;
            return true;
        }
        catch (Exception ex) when (IsFileSystemError(exception: ex))
        {
            return false;
        }
        finally
        {
            if (temporaryFileExists)
            {
                try
                {
                    File.Delete(path: temporaryPath);
                }
                catch (Exception ex) when (IsFileSystemError(exception: ex))
                {
                    _ = ex;
                }
            }
        }
#pragma warning restore SCS0018,SEC0112,SEC0116
    }

    private sealed record Entry(bool PerSourceFile, string[] Scopes);
}
