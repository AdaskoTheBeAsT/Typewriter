using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Typewriter.LanguageServer;

internal sealed class JsonRpcConnection : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(defaults: JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

#pragma warning disable CA2213 // Disposable fields should be disposed
    private readonly Stream _input;
    private readonly Stream _output;
#pragma warning restore CA2213 // Disposable fields should be disposed
    private readonly SemaphoreSlim _writeLock = new(initialCount: 1, maxCount: 1);

    public JsonRpcConnection(
        Stream input,
        Stream output)
    {
        _input = input;
        _output = output;
    }

    public void Dispose()
    {
        _writeLock.Dispose();
    }

    public async Task<JsonDocument?> ReadMessageAsync(CancellationToken cancellationToken)
    {
        var contentLength = await ReadContentLengthAsync(cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        if (contentLength is null)
        {
            return null;
        }

        var payload = new byte[contentLength.Value];
        await _input.ReadExactlyAsync(buffer: payload, cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        return JsonDocument.Parse(utf8Json: payload);
    }

    public Task WriteResponseAsync(
        JsonElement id,
        object? result,
        CancellationToken cancellationToken) =>
#pragma warning disable CC0021 // Use nameof
        WriteAsync(
            payload: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [key: "jsonrpc"] = "2.0",
                [key: "id"] = id.Clone(),
                [key: "result"] = result,
            },
            cancellationToken: cancellationToken);
#pragma warning restore CC0021 // Use nameof

    public Task WriteErrorResponseAsync(
        JsonElement id,
        int code,
        string message,
        CancellationToken cancellationToken) =>
#pragma warning disable CC0021 // Use nameof
        WriteAsync(
            payload: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [key: "jsonrpc"] = "2.0",
                [key: "id"] = id.Clone(),
                [key: "error"] = new
                {
                    code,
                    message,
                },
            },
            cancellationToken: cancellationToken);
#pragma warning restore CC0021 // Use nameof

    public Task WriteNotificationAsync(
        string method,
        object? parameters,
        CancellationToken cancellationToken) =>
#pragma warning disable CC0021 // Use nameof
        WriteAsync(
            payload: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [key: "jsonrpc"] = "2.0",
                [key: "method"] = method,
                [key: "params"] = parameters,
            },
            cancellationToken: cancellationToken);
#pragma warning restore CC0021 // Use nameof

    private async Task WriteAsync(
        object payload,
        CancellationToken cancellationToken)
    {
        var content = JsonSerializer.SerializeToUtf8Bytes(value: payload, options: JsonOptions);
        var header = Encoding.ASCII.GetBytes(s: $"Content-Length: {content.Length}\r\n\r\n");

        await _writeLock.WaitAsync(cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        try
        {
            await _output.WriteAsync(buffer: header, cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
            await _output.WriteAsync(buffer: content, cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
            await _output.FlushAsync(cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        }
        finally
        {
            _ = _writeLock.Release();
        }
    }

    private async Task<int?> ReadContentLengthAsync(CancellationToken cancellationToken)
    {
        int? contentLength = null;
        while (true)
        {
            var line = await ReadHeaderLineAsync(cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
            if (line is null)
            {
                return null;
            }

            if (line.Length == 0)
            {
                return contentLength;
            }

            var separator = line.IndexOf(value: ':', comparisonType: StringComparison.Ordinal);
            if (separator <= 0)
            {
                continue;
            }

            var name = line[..separator].Trim();
            if (!name.Equals(value: "Content-Length", comparisonType: StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = line[(separator + 1)..].Trim();
            if (int.TryParse(s: value, style: NumberStyles.Integer, provider: CultureInfo.InvariantCulture, result: out var parsedLength))
            {
                contentLength = parsedLength;
            }
        }
    }

    private async Task<string?> ReadHeaderLineAsync(CancellationToken cancellationToken)
    {
        var line = new List<byte>();
        while (true)
        {
            var value = await ReadByteAsync(cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
            if (value < 0)
            {
                return line.Count == 0
                    ? null
                    : Encoding.ASCII.GetString(bytes: line.ToArray());
            }

            if (value == '\n')
            {
                return Encoding.ASCII.GetString(bytes: line.ToArray());
            }

            if (value != '\r')
            {
                line.Add(item: (byte)value);
            }
        }
    }

    private async Task<int> ReadByteAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[1];
        var read = await _input.ReadAsync(buffer: buffer.AsMemory(start: 0, length: 1), cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        return read == 0
            ? -1
            : buffer[0];
    }
}
