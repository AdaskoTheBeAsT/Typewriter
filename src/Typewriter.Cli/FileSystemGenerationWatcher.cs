namespace Typewriter.Cli;

internal sealed class FileSystemGenerationWatcher : IDisposable
{
    private static readonly HashSet<string> WatchedExtensions = new(comparer: StringComparer.OrdinalIgnoreCase)
    {
        ".cs",
        ".csproj",
        ".json",
        ".props",
        ".sln",
        ".slnx",
        ".targets",
        ".tst",
    };

    private readonly Lock _sync = new();
    private readonly FileSystemWatcher[] _watchers;
    private TaskCompletionSource _changeSignal = CreateSignal();

    private FileSystemGenerationWatcher(IEnumerable<string> roots)
    {
        _watchers = roots.Select(selector: CreateWatcher).ToArray();
    }

    public static FileSystemGenerationWatcher Create(CliOptions options) =>
        new(roots: ResolveWatchRoots(options: options));

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

    private static IEnumerable<string> ResolveWatchRoots(CliOptions options)
    {
        var roots = new[]
            {
                options.WorkspacePath,
                options.ProjectPath,
                options.TemplatePath,
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

    private static bool ShouldWatchPath(string path)
    {
        if (string.IsNullOrWhiteSpace(value: path))
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

        return WatchedExtensions.Contains(item: Path.GetExtension(path: fullPath));
    }

    private static bool IsIgnoredDirectory(string segment) =>
        segment.Equals(value: "bin", comparisonType: StringComparison.OrdinalIgnoreCase)
        || segment.Equals(value: "obj", comparisonType: StringComparison.OrdinalIgnoreCase)
        || segment.Equals(value: "node_modules", comparisonType: StringComparison.OrdinalIgnoreCase)
        || segment.Equals(value: "generated", comparisonType: StringComparison.OrdinalIgnoreCase);

    private static TaskCompletionSource CreateSignal() =>
        new(creationOptions: TaskCreationOptions.RunContinuationsAsynchronously);

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
        if (ShouldWatchPath(path: args.FullPath) || ShouldWatchPath(path: args.OldFullPath))
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
            Signal();
        }
    }

    private void OnWatcherError(
        object sender,
        ErrorEventArgs args) =>
        Signal();

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
