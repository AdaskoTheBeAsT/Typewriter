using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace Typewriter.VisualStudio;

internal sealed class TypewriterSaveListener : IVsRunningDocTableEvents3, IDisposable
{
    private static readonly TimeSpan SaveDebounceDelay = TimeSpan.FromMilliseconds(value: 300);
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

    private static readonly HashSet<string> TemplateExtensions = new(comparer: StringComparer.OrdinalIgnoreCase)
    {
        ".tst",
    };

    private readonly object _sync = new();
    private readonly TypewriterPackage _package;
    private readonly TypewriterCommandService _commandService;
    private readonly Timer _saveTimer;
    private SaveGenerationRequest? _pendingRequest;
    private DateTimeOffset _lastSaveAt = DateTimeOffset.MinValue;
    private bool _isProcessing;
    private bool _disposed;

    public TypewriterSaveListener(
        TypewriterPackage package,
        TypewriterCommandService commandService)
    {
        _package = package;
        _commandService = commandService;
        _saveTimer = new Timer(
            callback: OnSaveTimerElapsed,
            state: null,
            dueTime: Timeout.InfiniteTimeSpan,
            period: Timeout.InfiniteTimeSpan);
    }

    public int OnAfterSave(uint docCookie)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var options = _package.GetTypewriterOptions();
        if (!options.GenerateOnSave)
        {
            return VSConstants.S_OK;
        }

        var savedPath = GetDocumentMoniker(docCookie: docCookie) ?? string.Empty;
        _ = _package.JoinableTaskFactory.RunAsync(
            asyncMethod: async () =>
            {
                var inputExtensions = await Task.Run(function: () => ReadConfiguredInputExtensions(path: savedPath)).ConfigureAwait(continueOnCapturedContext: false);
                var request = CreateSaveGenerationRequest(path: savedPath, inputExtensions: inputExtensions);
                if (request is not null)
                {
                    Schedule(request: request);
                }
            });

        return VSConstants.S_OK;
    }

    public void Dispose()
    {
        lock (_sync)
        {
            _disposed = true;
            _pendingRequest = null;
        }

        _saveTimer.Dispose();
    }

    private static IReadOnlyCollection<string> ReadConfiguredInputExtensions(string path)
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

        return extensions;
    }

    public int OnBeforeSave(uint docCookie) => VSConstants.S_OK;

    public int OnAfterAttributeChange(
        uint docCookie,
        uint grfAttribs) =>
        VSConstants.S_OK;

    public int OnAfterDocumentWindowHide(
        uint docCookie,
        IVsWindowFrame pFrame) =>
        VSConstants.S_OK;

    public int OnAfterFirstDocumentLock(
        uint docCookie,
        uint dwRDTLockType,
        uint dwReadLocksRemaining,
        uint dwEditLocksRemaining) =>
        VSConstants.S_OK;

    public int OnBeforeDocumentWindowShow(
        uint docCookie,
        int fFirstShow,
        IVsWindowFrame pFrame) =>
        VSConstants.S_OK;

    public int OnBeforeLastDocumentUnlock(
        uint docCookie,
        uint dwRDTLockType,
        uint dwReadLocksRemaining,
        uint dwEditLocksRemaining) =>
        VSConstants.S_OK;

    public int OnAfterAttributeChangeEx(
        uint docCookie,
        uint grfAttribs,
        IVsHierarchy pHierOld,
        uint itemidOld,
        string pszMkDocumentOld,
        IVsHierarchy pHierNew,
        uint itemidNew,
        string pszMkDocumentNew) =>
        VSConstants.S_OK;

    public int OnBeforeDocumentWindowShow(
        uint docCookie,
        int fFirstShow,
        IVsWindowFrame pFrame,
        IVsUIHierarchy pHier,
        uint itemid,
        string pszMkDocument) =>
        VSConstants.S_OK;

    private string? GetDocumentMoniker(uint docCookie)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (_package.GetVisualStudioServiceAsync(serviceType: typeof(SVsRunningDocumentTable), cancellationToken: CancellationToken.None)
                .GetAwaiter()
                .GetResult() is not IVsRunningDocumentTable table)
        {
            return null;
        }

        ErrorHandler.ThrowOnFailure(
            hr: table.GetDocumentInfo(
                docCookie: docCookie,
                pgrfRDTFlags: out _,
                pdwReadLocks: out _,
                pdwEditLocks: out _,
                pbstrMkDocument: out var moniker,
                ppHier: out _,
                pitemid: out _,
                ppunkDocData: out _));
        return moniker;
    }

    private void Schedule(SaveGenerationRequest request)
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _pendingRequest = SaveGenerationRequest.Merge(existing: _pendingRequest, incoming: request);
            _lastSaveAt = DateTimeOffset.UtcNow;
            _saveTimer.Change(dueTime: SaveDebounceDelay, period: Timeout.InfiniteTimeSpan);
        }
    }

    private void OnSaveTimerElapsed(object? state) =>
        _ = _package.JoinableTaskFactory.RunAsync(asyncMethod: ProcessPendingSavesAsync);

    private async Task ProcessPendingSavesAsync()
    {
        lock (_sync)
        {
            if (_disposed || _isProcessing)
            {
                return;
            }

            _isProcessing = true;
        }

        try
        {
            while (true)
            {
                await WaitForQuietPeriodAsync().ConfigureAwait(continueOnCapturedContext: false);

                SaveGenerationRequest? request;
                lock (_sync)
                {
                    if (_disposed)
                    {
                        return;
                    }

                    request = _pendingRequest;
                    _pendingRequest = null;
                    if (request is null)
                    {
                        return;
                    }
                }

                await ExecuteSaveRequestAsync(request: request).ConfigureAwait(continueOnCapturedContext: false);
            }
        }
        finally
        {
            lock (_sync)
            {
                _isProcessing = false;
                if (!_disposed && _pendingRequest is not null)
                {
                    _saveTimer.Change(dueTime: SaveDebounceDelay, period: Timeout.InfiniteTimeSpan);
                }
            }
        }
    }

    private async Task WaitForQuietPeriodAsync()
    {
        while (true)
        {
            TimeSpan remaining;
            lock (_sync)
            {
                remaining = SaveDebounceDelay - (DateTimeOffset.UtcNow - _lastSaveAt);
            }

            if (remaining <= TimeSpan.Zero)
            {
                return;
            }

            await Task.Delay(delay: remaining).ConfigureAwait(continueOnCapturedContext: false);
        }
    }

    private Task ExecuteSaveRequestAsync(SaveGenerationRequest request) =>
        request.Scope == SaveGenerationScope.CurrentTemplate
            ? _commandService.GenerateSavedTemplateAsync(templatePath: request.Path, cancellationToken: CancellationToken.None)
            : _commandService.GenerateSavedInputAsync(inputPath: request.Path, changedPaths: request.ChangedPaths, cancellationToken: CancellationToken.None);

    private static SaveGenerationRequest? CreateSaveGenerationRequest(
        string path,
        IReadOnlyCollection<string> inputExtensions)
    {
        if (string.IsNullOrWhiteSpace(value: path) || IsIgnoredPath(path: path))
        {
            return null;
        }

        var extension = Path.GetExtension(path: path);
        if (!inputExtensions.Contains(value: extension, comparer: StringComparer.OrdinalIgnoreCase))
        {
            return null;
        }

        var scope = TemplateExtensions.Contains(item: extension)
            ? SaveGenerationScope.CurrentTemplate
            : SaveGenerationScope.AllTemplates;
        return new SaveGenerationRequest(path: path, scope: scope, changedPaths: [path]);
    }

    private static IEnumerable<string> FindConfigurationFiles(string path)
    {
        if (string.IsNullOrWhiteSpace(value: path))
        {
            yield break;
        }

        var directory = File.Exists(path: path)
            ? Path.GetDirectoryName(path: path)
            : path;
        var directories = new List<string>();
        while (!string.IsNullOrWhiteSpace(value: directory))
        {
            directories.Add(item: directory!);
            directory = Directory.GetParent(path: directory)?.FullName;
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

    private static bool IsIgnoredPath(string path)
    {
        var fullPath = Path.GetFullPath(path: path);
        var segments = fullPath.Split(
            separator: [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            options: StringSplitOptions.RemoveEmptyEntries);
        return Array.Exists(array: segments, match: IsIgnoredDirectory);
    }

    private static bool IsIgnoredDirectory(string segment) =>
        segment.Equals(value: "bin", comparisonType: StringComparison.OrdinalIgnoreCase)
        || segment.Equals(value: "obj", comparisonType: StringComparison.OrdinalIgnoreCase)
        || segment.Equals(value: "node_modules", comparisonType: StringComparison.OrdinalIgnoreCase)
        || segment.Equals(value: "generated", comparisonType: StringComparison.OrdinalIgnoreCase);

    private sealed class SaveGenerationRequest
    {
        public SaveGenerationRequest(
            string path,
            SaveGenerationScope scope,
            IReadOnlyCollection<string> changedPaths)
        {
            Path = path;
            Scope = scope;
            ChangedPaths = changedPaths;
        }

        public string Path { get; }

        public SaveGenerationScope Scope { get; }

        public IReadOnlyCollection<string> ChangedPaths { get; }

        public static SaveGenerationRequest Merge(
            SaveGenerationRequest? existing,
            SaveGenerationRequest incoming)
        {
            if (existing is null)
            {
                return incoming;
            }

            var winner = existing.Scope == SaveGenerationScope.AllTemplates && incoming.Scope == SaveGenerationScope.CurrentTemplate
                ? existing
                : incoming;
            var changedPaths = new HashSet<string>(collection: existing.ChangedPaths, comparer: StringComparer.OrdinalIgnoreCase);
            changedPaths.UnionWith(other: incoming.ChangedPaths);
            return new SaveGenerationRequest(path: winner.Path, scope: winner.Scope, changedPaths: changedPaths);
        }
    }

    private enum SaveGenerationScope
    {
        CurrentTemplate,
        AllTemplates,
    }
}
