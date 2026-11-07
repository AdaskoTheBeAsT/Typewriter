using System.Diagnostics;
using System.Globalization;

namespace Typewriter.Engine;

internal sealed class TemplatePerformanceStatistics(string templatePath)
{
    private readonly List<(string Path, TimeSpan Elapsed)> _slowest = new(capacity: 5);
    private TimeSpan _render;
    private TimeSpan _planning;
    private TimeSpan _formatting;
    private TimeSpan _writing;
    private int _calls;
    private int _outputs;

    public string TemplatePath { get; } = templatePath;

    public void RecordRender(string sourcePath, long started)
    {
        var elapsed = Stopwatch.GetElapsedTime(startingTimestamp: started);
        _render += elapsed;
        _calls++;
        var index = 0;
        while (index < _slowest.Count && _slowest[index].Elapsed >= elapsed)
        {
            index++;
        }

        if (index < 5)
        {
            if (_slowest.Count == 5)
            {
                _slowest.RemoveAt(index: 4);
            }

            _slowest.Insert(index: index, item: (sourcePath, elapsed));
        }
    }

    public void RecordPlanning(long started) => _planning += Stopwatch.GetElapsedTime(startingTimestamp: started);

    public void RecordFormatting(long started) => _formatting += Stopwatch.GetElapsedTime(startingTimestamp: started);

    public void RecordWrite(long started)
    {
        _writing += Stopwatch.GetElapsedTime(startingTimestamp: started);
        _outputs++;
    }

    public string CreateSummary()
    {
        var slowest = string.Join(
            separator: ", ",
            values: _slowest.Select(selector: item => string.Create(provider: CultureInfo.InvariantCulture, handler: $"{item.Path}={item.Elapsed.TotalMilliseconds:F1} ms")));
        return string.Create(
            provider: CultureInfo.InvariantCulture,
            handler: $"Template run ({Path.GetFileName(path: TemplatePath)}): render-calls={_calls}, outputs={_outputs}, render={_render.TotalMilliseconds:F1} ms, avg={(_calls == 0 ? 0 : _render.TotalMilliseconds / _calls):F3} ms, planning={_planning.TotalMilliseconds:F1} ms, formatting/writing={_writing.TotalMilliseconds:F1} ms (formatting={_formatting.TotalMilliseconds:F1} ms), slowest=[{slowest}]");
    }
}
