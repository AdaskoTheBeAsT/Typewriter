namespace Typewriter.Abstractions;

public sealed record DiagnosticsConfiguration(bool FailOnWarning)
{
    public static DiagnosticsConfiguration Default { get; } = new(FailOnWarning: false);
}
