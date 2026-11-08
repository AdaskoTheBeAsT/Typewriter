using System.Diagnostics;
using Typewriter.Abstractions;

namespace Typewriter.Roslyn;

internal readonly struct MetadataStageTimer(ProjectContext project)
{
    private readonly long _started = project.PerformanceSink is null ? 0 : Stopwatch.GetTimestamp();

    public void Complete(string stage) =>
        project.PerformanceSink?.Invoke(
            obj: new MetadataStageTiming(ProjectPath: project.ProjectPath, Stage: stage, Elapsed: Stopwatch.GetElapsedTime(startingTimestamp: _started)));
}
