using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using StreamJsonRpc;

namespace Typewriter.VisualStudio;

internal sealed class TypewriterPersistentGenerationClient : IDisposable
{
    private readonly SemaphoreSlim _connectionLock = new(initialCount: 1, maxCount: 1);
    private System.Diagnostics.Process? _process;
    private JsonRpc? _rpc;

    public void Dispose()
    {
        ResetConnection();
        _connectionLock.Dispose();
    }

    public async Task<CliResult?> TryGenerateAsync(
        TypewriterGenerationRequest request,
        TypewriterOptions options,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        await _connectionLock.WaitAsync(cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        try
        {
            await EnsureConnectedAsync(
                options: options,
                workspacePath: request.WorkspacePath,
                workingDirectory: workingDirectory,
                cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
            return await _rpc!.InvokeWithParameterObjectAsync<CliResult>(
                targetName: "typewriter/generate",
                argument: request,
                cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            ActivityLog.TryLogInformation(
                source: "Typewriter",
                message: "Dedicated persistent generation unavailable; falling back to the CLI: " + exception.Message);
            ResetConnection();
            return null;
        }
        finally
        {
            _ = _connectionLock.Release();
        }
    }

    private async Task EnsureConnectedAsync(
        TypewriterOptions options,
        string workspacePath,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        if (_rpc is not null && _process is { HasExited: false })
        {
            return;
        }

        ResetConnection();
        var invocation = TypewriterLanguageClient.ResolveLanguageServerInvocation(options: options, workspacePath: workspacePath);
        var process = new System.Diagnostics.Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = invocation.Command,
                Arguments = string.Join(separator: " ", values: invocation.Arguments.Select(selector: QuoteArgument)),
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            },
        };
        try
        {
            process.Start();
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            process.Dispose();
            throw;
        }

        _process = process;
        _ = Task.Run(
            function: () => TypewriterLanguageClient.DrainErrorAsync(process: process),
            cancellationToken: CancellationToken.None);
        var handler = new HeaderDelimitedMessageHandler(
            sendingStream: process.StandardInput.BaseStream,
            receivingStream: process.StandardOutput.BaseStream,
            formatter: new JsonMessageFormatter());
        var rpc = new JsonRpc(messageHandler: handler);
        rpc.StartListening();
        _rpc = rpc;

        await rpc.InvokeWithParameterObjectAsync<object>(
            targetName: "initialize",
            argument: new
            {
                rootUri = new Uri(uriString: workspacePath).AbsoluteUri,
                initializationOptions = new
                {
                    workspacePath,
                },
            },
            cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
    }

    private void ResetConnection()
    {
        var rpc = _rpc;
        _rpc = null;
        rpc?.Dispose();

        var process = _process;
        _process = null;
        if (process is null)
        {
            return;
        }

        try
        {
            if (!process.HasExited)
            {
                process.Kill();
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception)
        {
            ActivityLog.TryLogInformation(source: "Typewriter", message: exception.Message);
        }
        finally
        {
            process.Dispose();
        }
    }

    private static string QuoteArgument(string argument)
    {
        if (argument.Length == 0)
        {
            return "\"\"";
        }

        return argument.Any(predicate: char.IsWhiteSpace) || argument.Contains(value: "\"")
            ? "\"" + argument.Replace(oldValue: "\\", newValue: "\\\\").Replace(oldValue: "\"", newValue: "\\\"") + "\""
            : argument;
    }
}
