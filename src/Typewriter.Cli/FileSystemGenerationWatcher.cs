using Typewriter.Abstractions;

namespace Typewriter.Cli;

internal sealed class FileSystemGenerationWatcher : IDisposable
{
    private const int MaxTrackedChangedInputs = 4096;
    private readonly Lock _sync = new();
    private readonly FileSystemWatcher[] _watchers;
    private readonly HashSet<string> _watchedExtensions;
    private readonly Dictionary<string, ChangedInputKind> _changedInputs = new(comparer: StringComparer.OrdinalIgnoreCase);
    private bool _changedInputsOverflowed;
    private TaskCompletionSource _changeSignal = CreateSignal();

    private FileSystemGenerationWatcher(
        IEnumerable<string> roots,
        IEnumerable<string> watchedExtensions)
    {
        _watchedExtensions = new HashSet<string>(collection: watchedExtensions, comparer: StringComparer.OrdinalIgnoreCase);
        _watchers = roots.Select(selector: CreateWatcher).ToArray();
    }

    public static FileSystemGenerationWatcher Create(
        CliOptions options,
        TypewriterConfiguration configuration) =>
        new(roots: ResolveWatchRoots(options: options), watchedExtensions: configuration.InputExtensions);

    public void Dispose()
    {
        foreach (var watcher in _watchers)
        {
            watcher.Dispose();
        }
    }

    public Task WaitForChangeAsync(CancellationToken cancellationToken)
    {
        Task signalTask;
        lock (_sync)
        {
            signalTask = _changeSignal.Task;
        }

        return signalTask.WaitAsync(cancellationToken: cancellationToken);
    }

    public async Task WaitForQuietPeriodAsync(
        TimeSpan quietPeriod,
        CancellationToken cancellationToken)
    {
        do
        {
            ResetSignal();
            await Task.Delay(delay: quietPeriod, cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        }
        while (IsSignaled());
    }

    /// <summary>
    /// Returns the inputs changed since the previous drain and clears the accumulated set.
    /// Returns <c>null</c> when the provenance is unknown (no change recorded yet, the
    /// tracked set overflowed, or a watcher error occurred) and a full generation is required.
    /// </summary>
    /// <returns>The changed inputs, or <c>null</c> when a full generation is required.</returns>
    public IReadOnlyCollection<ChangedInput>? DrainChangedInputs()
    {
        lock (_sync)
        {
            if (_changedInputsOverflowed)
            {
                _changedInputsOverflowed = false;
                _changedInputs.Clear();
                return null;
            }

            if (_changedInputs.Count == 0)
            {
                return null;
            }

            var drained = _changedInputs
                .Select(selector: pair => new ChangedInput(FullPath: pair.Key, Kind: pair.Value))
                .ToArray();
            _changedInputs.Clear();
            return drained;
        }
    }

    private static IEnumerable<string> ResolveWatchRoots(CliOptions options)
    {
        var roots = new[]
            {
                options.WorkspacePath,
                options.ProjectPath,
                options.TemplatePath,
                options.TemplateSearchPath,
            }
            .Where(predicate: path => !string.IsNullOrWhiteSpace(value: path))
            .Select(selector: ResolveWatchDirectory)
            .Where(predicate: Directory.Exists)
            .Distinct(comparer: StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return roots.Length == 0
            ? [Environment.CurrentDirectory]
            : roots;
    }

    private static string ResolveWatchDirectory(string? path)
    {
        if (string.IsNullOrWhiteSpace(value: path))
        {
            return Environment.CurrentDirectory;
        }

        var fullPath = Path.GetFullPath(path: path);
        return File.Exists(path: fullPath)
            ? Path.GetDirectoryName(path: fullPath) ?? Environment.CurrentDirectory
            : fullPath;
    }

    private static bool IsIgnoredDirectory(string segment) =>
        segment.Equals(value: "bin", comparisonType: StringComparison.OrdinalIgnoreCase)
        || segment.Equals(value: "obj", comparisonType: StringComparison.OrdinalIgnoreCase)
        || segment.Equals(value: "node_modules", comparisonType: StringComparison.OrdinalIgnoreCase)
        || segment.Equals(value: "generated", comparisonType: StringComparison.OrdinalIgnoreCase);

    private static TaskCompletionSource CreateSignal() =>
        new(creationOptions: TaskCreationOptions.RunContinuationsAsynchronously);

    private static ChangedInputKind MapChangedInputKind(WatcherChangeTypes changeType) =>
        changeType switch
        {
            WatcherChangeTypes.Created => ChangedInputKind.Added,
            WatcherChangeTypes.Deleted => ChangedInputKind.Deleted,
            _ => ChangedInputKind.Modified,
        };

    private bool ShouldWatchPath(string path)
    {
        if (string.IsNullOrWhiteSpace(value: path))
        {
            return false;
        }

        if (!_watchedExtensions.Contains(item: Path.GetExtension(path: path)))
        {
            return false;
        }

        var fullPath = Path.GetFullPath(path: path);
        var segments = fullPath.Split(
            separator: [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            options: StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(predicate: IsIgnoredDirectory))
        {
            return false;
        }

        return true;
    }

    private FileSystemWatcher CreateWatcher(string root)
    {
        var watcher = new FileSystemWatcher(path: root)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName
                | NotifyFilters.LastWrite
                | NotifyFilters.CreationTime
                | NotifyFilters.DirectoryName,
        };

        watcher.Changed += OnFileChanged;
        watcher.Created += OnFileChanged;
        watcher.Deleted += OnFileChanged;
        watcher.Renamed += OnFileRenamed;
        watcher.Error += OnWatcherError;
        watcher.EnableRaisingEvents = true;
        return watcher;
    }

    private void OnFileRenamed(
        object sender,
        RenamedEventArgs args)
    {
        var signal = false;
        if (ShouldWatchPath(path: args.OldFullPath))
        {
            RecordChange(path: args.OldFullPath, kind: ChangedInputKind.Renamed);
            signal = true;
        }

        if (ShouldWatchPath(path: args.FullPath))
        {
            RecordChange(path: args.FullPath, kind: ChangedInputKind.Renamed);
            signal = true;
        }

        if (signal)
        {
            Signal();
        }
    }

    private void OnFileChanged(
        object sender,
        FileSystemEventArgs args)
    {
        if (ShouldWatchPath(path: args.FullPath))
        {
            RecordChange(path: args.FullPath, kind: MapChangedInputKind(changeType: args.ChangeType));
            Signal();
        }
    }

    private void OnWatcherError(
        object sender,
        ErrorEventArgs args)
    {
        MetadataCacheInvalidation.MarkAllDirty();
        lock (_sync)
        {
            _changedInputsOverflowed = true;
            _changedInputs.Clear();
        }

        Signal();
    }

    private void RecordChange(
        string path,
        ChangedInputKind kind)
    {
        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path: path);
        }
        catch (ArgumentException)
        {
            MarkChangedInputsOverflowed();
            return;
        }
        catch (NotSupportedException)
        {
            MarkChangedInputsOverflowed();
            return;
        }
        catch (PathTooLongException)
        {
            MarkChangedInputsOverflowed();
            return;
        }

        MetadataCacheInvalidation.MarkDirty(fullPath: fullPath);
        lock (_sync)
        {
            if (_changedInputsOverflowed)
            {
                return;
            }

            if (_changedInputs.Count >= MaxTrackedChangedInputs && !_changedInputs.ContainsKey(key: fullPath))
            {
                _changedInputsOverflowed = true;
                _changedInputs.Clear();
                return;
            }

            _changedInputs[key: fullPath] = kind;
        }
    }

    private void MarkChangedInputsOverflowed()
    {
        MetadataCacheInvalidation.MarkAllDirty();
        lock (_sync)
        {
            _changedInputsOverflowed = true;
            _changedInputs.Clear();
        }
    }

    private void Signal()
    {
        lock (_sync)
        {
            _ = _changeSignal.TrySetResult();
        }
    }

    private void ResetSignal()
    {
        lock (_sync)
        {
            if (_changeSignal.Task.IsCompleted)
            {
                _changeSignal = CreateSignal();
            }
        }
    }

    private bool IsSignaled()
    {
        lock (_sync)
        {
            return _changeSignal.Task.IsCompleted;
        }
    }
}
