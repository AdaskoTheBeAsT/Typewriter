namespace Typewriter.VisualStudio;

public sealed class NullLog : ILog
{
    public static NullLog Instance { get; } = new();

    public void LogDebug(
        string message,
        params object[] parameters)
    {
    }

    public void LogInfo(
        string message,
        params object[] parameters)
    {
    }

    public void LogWarning(
        string message,
        params object[] parameters)
    {
    }

    public void LogError(
        string message,
        params object[] parameters)
    {
    }
}
