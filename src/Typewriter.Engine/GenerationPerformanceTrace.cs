using System.Diagnostics;
using System.Globalization;
using Typewriter.Abstractions;

namespace Typewriter.Engine;

internal sealed class GenerationPerformanceTrace
{
    private readonly Lock _sync = new();
    private readonly List<(string Stage, TimeSpan Elapsed)>? _entries;
    private readonly List<TemplatePerformanceStatistics>? _templates;
    private readonly long _allocatedBytes;
    private readonly TimeSpan _gcPause;
    private readonly int[]? _collections;

    internal GenerationPerformanceTrace(bool enabled)
    {
        Enabled = enabled;
        if (enabled)
        {
            _entries = [];
            _templates = [];
            _allocatedBytes = GC.GetTotalAllocatedBytes(precise: false);
            _gcPause = GC.GetTotalPauseDuration();
            _collections = [GC.CollectionCount(generation: 0), GC.CollectionCount(generation: 1), GC.CollectionCount(generation: 2)];
            MetadataSink = RecordMetadata;
        }
    }

    public bool Enabled { get; }

    public Action<MetadataStageTiming>? MetadataSink { get; }

    public static GenerationPerformanceTrace Create()
    {
        var value = Environment.GetEnvironmentVariable(variable: "TYPEWRITER_TRACE_PERF");
        return new GenerationPerformanceTrace(
            enabled: value is not null
                && (value.Equals(value: "1", comparisonType: StringComparison.OrdinalIgnoreCase)
                    || value.Equals(value: "true", comparisonType: StringComparison.OrdinalIgnoreCase)
                    || value.Equals(value: "yes", comparisonType: StringComparison.OrdinalIgnoreCase)));
    }

    public long Start() => Enabled ? Stopwatch.GetTimestamp() : 0;

    public TemplatePerformanceStatistics? CreateTemplateStatistics(string templatePath)
    {
        if (!Enabled)
        {
            return null;
        }

        var statistics = new TemplatePerformanceStatistics(templatePath: templatePath);
        _templates!.Add(item: statistics);
        return statistics;
    }

    public void Add(string stage, TimeSpan elapsed)
    {
        if (_entries is not null)
        {
            lock (_sync)
            {
                _entries.Add(item: (stage, elapsed));
            }
        }
    }

    public void AddElapsed(string stage, long started)
    {
        if (Enabled)
        {
            Add(stage: stage, elapsed: Stopwatch.GetElapsedTime(startingTimestamp: started));
        }
    }

    public void AddDiagnostics(ICollection<GenerationDiagnostic> diagnostics, string file)
    {
        if (!Enabled)
        {
            return;
        }

        var allocated = GC.GetTotalAllocatedBytes(precise: false) - _allocatedBytes;
        var pause = GC.GetTotalPauseDuration() - _gcPause;
        var gen0 = GC.CollectionCount(generation: 0) - _collections![0];
        var gen1 = GC.CollectionCount(generation: 1) - _collections[1];
        var gen2 = GC.CollectionCount(generation: 2) - _collections[2];
        lock (_sync)
        {
            foreach (var entry in _entries!)
            {
                AddDiagnostic(
                    diagnostics: diagnostics,
                    file: file,
                    message: string.Create(provider: CultureInfo.InvariantCulture, handler: $"{entry.Stage}: {entry.Elapsed.TotalMilliseconds:F1} ms"));
            }
        }

        foreach (var template in _templates!)
        {
            AddDiagnostic(diagnostics: diagnostics, file: template.TemplatePath, message: template.CreateSummary());
        }

        AddDiagnostic(diagnostics: diagnostics, file: file, message: "Metadata cache (process-wide): " + MetadataCacheMetrics.CreateSummary());
        AddDiagnostic(
            diagnostics: diagnostics,
            file: file,
            message: string.Create(
                provider: CultureInfo.InvariantCulture,
                handler: $"Memory (process-wide delta; concurrent work may contribute): allocated={allocated} bytes, GC={gen0}/{gen1}/{gen2}, GC-pause={pause.TotalMilliseconds:F1} ms"));
    }

    private static void AddDiagnostic(ICollection<GenerationDiagnostic> diagnostics, string file, string message) =>
        diagnostics.Add(item: new GenerationDiagnostic(File: file, Line: null, Column: null, Severity: DiagnosticSeverity.Info, Message: message, Code: "TWPERF"));

    private void RecordMetadata(MetadataStageTiming timing) =>
        Add(stage: $"Metadata project ({timing.ProjectPath}) / {timing.Stage}", elapsed: timing.Elapsed);
}
