namespace Typewriter.Abstractions;

/// <summary>
/// Process-wide dirty-path tracking for the project metadata cache. Hosts that run a
/// filesystem watcher over every metadata input (for example <c>typewriter watch</c>)
/// enable tracking and feed changed paths in. Cache entries validated at a version with
/// no dirty paths marked since then can be accepted without any filesystem access.
/// Hosts without a watcher leave tracking disabled and cache validation falls back to
/// the per-file stamp manifest check.
/// </summary>
public static class MetadataCacheInvalidation
{
    private const int MaxTrackedPaths = 8192;
    private static readonly Lock Sync = new();
    private static readonly Dictionary<string, long> DirtyPaths = new(comparer: StringComparer.OrdinalIgnoreCase);
    private static long _version;
    private static long _unknownBeforeVersion;
    private static bool _trackingEnabled;

    public static bool TrackingEnabled
    {
        get
        {
            lock (Sync)
            {
                return _trackingEnabled;
            }
        }
    }

    public static long CurrentVersion
    {
        get
        {
            lock (Sync)
            {
                return _version;
            }
        }
    }

    public static void EnableTracking()
    {
        lock (Sync)
        {
            if (_trackingEnabled)
            {
                return;
            }

            _trackingEnabled = true;
            _version++;
            _unknownBeforeVersion = _version;
            DirtyPaths.Clear();
        }
    }

    public static void MarkDirty(string fullPath)
    {
        if (string.IsNullOrWhiteSpace(value: fullPath))
        {
            return;
        }

        string normalizedPath;
        try
        {
            normalizedPath = Path.GetFullPath(path: fullPath);
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

        lock (Sync)
        {
            if (!_trackingEnabled)
            {
                return;
            }

            _version++;
            if (DirtyPaths.Count >= MaxTrackedPaths)
            {
                DirtyPaths.Clear();
                _unknownBeforeVersion = _version;
                return;
            }

            DirtyPaths[key: normalizedPath] = _version;
        }
    }

    public static void MarkAllDirty()
    {
        lock (Sync)
        {
            _version++;
            _unknownBeforeVersion = _version;
            DirtyPaths.Clear();
        }
    }

    /// <summary>
    /// Returns the paths marked dirty after <paramref name="sinceVersion"/>, or <c>null</c>
    /// when tracking cannot answer (tracking disabled, tracking started after the entry
    /// was validated, or the tracked set overflowed) and a full manifest check is required.
    /// </summary>
    /// <param name="sinceVersion">The version the cache entry was last validated at.</param>
    /// <returns>The dirty paths, an empty list when nothing changed, or <c>null</c> when unknown.</returns>
    public static IReadOnlyList<string>? GetDirtySince(long sinceVersion)
    {
        lock (Sync)
        {
            if (!_trackingEnabled || sinceVersion < _unknownBeforeVersion)
            {
                return null;
            }

            if (sinceVersion >= _version)
            {
                return [];
            }

            return DirtyPaths
                .Where(predicate: pair => pair.Value > sinceVersion)
                .Select(selector: pair => pair.Key)
                .ToArray();
        }
    }

    public static void Reset()
    {
        lock (Sync)
        {
            _trackingEnabled = false;
            _version++;
            _unknownBeforeVersion = _version;
            DirtyPaths.Clear();
        }
    }
}
