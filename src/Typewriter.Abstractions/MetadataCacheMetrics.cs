using System.Globalization;

namespace Typewriter.Abstractions;

/// <summary>
/// Process-wide counters describing project metadata cache behavior. Reported through
/// the generation performance trace when <c>TYPEWRITER_TRACE_PERF</c> is enabled.
/// </summary>
public static class MetadataCacheMetrics
{
    private static readonly Lock Sync = new();
    private static long _cacheHits;
    private static long _cacheMisses;
    private static long _statValidatedHits;
    private static long _dirtyValidatedHits;
    private static long _sourceOnlyRebuilds;
    private static long _fullLoads;
    private static long _parsedSourceFiles;
    private static long _reusedSyntaxTrees;
    private static string? _lastInvalidationReason;

    public static long CacheHits
    {
        get
        {
            lock (Sync)
            {
                return _cacheHits;
            }
        }
    }

    public static long CacheMisses
    {
        get
        {
            lock (Sync)
            {
                return _cacheMisses;
            }
        }
    }

    public static long StatValidatedHits
    {
        get
        {
            lock (Sync)
            {
                return _statValidatedHits;
            }
        }
    }

    public static long DirtyValidatedHits
    {
        get
        {
            lock (Sync)
            {
                return _dirtyValidatedHits;
            }
        }
    }

    public static long SourceOnlyRebuilds
    {
        get
        {
            lock (Sync)
            {
                return _sourceOnlyRebuilds;
            }
        }
    }

    public static long FullLoads
    {
        get
        {
            lock (Sync)
            {
                return _fullLoads;
            }
        }
    }

    public static long ParsedSourceFiles
    {
        get
        {
            lock (Sync)
            {
                return _parsedSourceFiles;
            }
        }
    }

    public static long ReusedSyntaxTrees
    {
        get
        {
            lock (Sync)
            {
                return _reusedSyntaxTrees;
            }
        }
    }

    public static string? LastInvalidationReason
    {
        get
        {
            lock (Sync)
            {
                return _lastInvalidationReason;
            }
        }
    }

    public static void RecordCacheHit(bool dirtyValidated)
    {
        lock (Sync)
        {
            _cacheHits++;
            if (dirtyValidated)
            {
                _dirtyValidatedHits++;
            }
            else
            {
                _statValidatedHits++;
            }
        }
    }

    public static void RecordCacheMiss(string? reason)
    {
        lock (Sync)
        {
            _cacheMisses++;
            if (!string.IsNullOrWhiteSpace(value: reason))
            {
                _lastInvalidationReason = reason;
            }
        }
    }

    public static void RecordSourceOnlyRebuild()
    {
        lock (Sync)
        {
            _sourceOnlyRebuilds++;
        }
    }

    public static void RecordFullLoad()
    {
        lock (Sync)
        {
            _fullLoads++;
        }
    }

    public static void RecordParsedSourceFile()
    {
        lock (Sync)
        {
            _parsedSourceFiles++;
        }
    }

    public static void RecordReusedSyntaxTree()
    {
        lock (Sync)
        {
            _reusedSyntaxTrees++;
        }
    }

    public static string CreateSummary()
    {
        lock (Sync)
        {
            return string.Create(
                provider: CultureInfo.InvariantCulture,
                handler: $"hits={_cacheHits} (stat={_statValidatedHits}, dirty={_dirtyValidatedHits}), misses={_cacheMisses}, source-only-rebuilds={_sourceOnlyRebuilds}, full-loads={_fullLoads}, parsed-files={_parsedSourceFiles}, reused-trees={_reusedSyntaxTrees}, last-invalidation={_lastInvalidationReason ?? "none"}");
        }
    }

    public static void Reset()
    {
        lock (Sync)
        {
            _cacheHits = 0;
            _cacheMisses = 0;
            _statValidatedHits = 0;
            _dirtyValidatedHits = 0;
            _sourceOnlyRebuilds = 0;
            _fullLoads = 0;
            _parsedSourceFiles = 0;
            _reusedSyntaxTrees = 0;
            _lastInvalidationReason = null;
        }
    }
}
