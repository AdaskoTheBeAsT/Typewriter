namespace Typewriter.Cli;

internal static class Program
{
#pragma warning disable CC0061 // Asynchronous method can be terminated with the 'Async' keyword.
    public static async Task<int> Main(string[] args)
#pragma warning restore CC0061 // Asynchronous method can be terminated with the 'Async' keyword.
    {
        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler handler = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };

        Console.CancelKeyPress += handler;
        try
        {
            return await TypewriterCli.RunAsync(args: args, cancellationToken: cancellation.Token).ConfigureAwait(continueOnCapturedContext: false);
        }
        finally
        {
            Console.CancelKeyPress -= handler;
        }
    }
}
