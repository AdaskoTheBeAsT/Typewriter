namespace Typewriter.LanguageServer;

internal static class LanguageServerProgram
{
    public static async Task RunAsync(
        string[] args,
        CancellationToken cancellationToken)
    {
        _ = args;

#pragma warning disable MA0004 // Use Task.ConfigureAwait
        await using var input = Console.OpenStandardInput();
        await using var output = Console.OpenStandardOutput();
#pragma warning restore MA0004 // Use Task.ConfigureAwait
        using var connection = new JsonRpcConnection(input: input, output: output);
        var server = new LanguageServerHost(connection: connection);
        await server.RunAsync(cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
    }
}
