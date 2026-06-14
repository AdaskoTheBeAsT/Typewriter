using System.Text;
using System.Text.Json;
using Typewriter.Abstractions;
using Xunit;

namespace Typewriter.LanguageServer.Tests;

public sealed class LanguageServerHostTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(defaults: JsonSerializerDefaults.Web);

    [Fact]
    public async Task RunAsyncHandlesDocumentSyncAndCompletionOverJsonRpc()
    {
        var directory = CreateProjectDirectory();
        try
        {
            var templatePath = Path.Combine(path1: directory, path2: "Models.tst");
            var templateUri = UriFromPath(path: templatePath);
            const string ChangedTemplate = """
                ${
                    string FormatName(Typewriter.CodeModel.Class @class) => @class.Name;
                }
                $
                """;

            await using var input = CreateInput(
                messages:
                [
                    CreateRequest(
                        id: 1,
                        method: "initialize",
                        parameters: new
                        {
                            rootUri = UriFromPath(path: directory),
                            initializationOptions = new
                            {
                                workspacePath = directory,
                            },
                        }),
                    CreateNotification(
                        method: "textDocument/didOpen",
                        parameters: new
                        {
                            textDocument = new
                            {
                                uri = templateUri,
                                languageId = "typewriter",
                                version = 1,
                                text = "$",
                            },
                        }),
                    CreateNotification(
                        method: "textDocument/didChange",
                        parameters: new
                        {
                            textDocument = new
                            {
                                uri = templateUri,
                                version = 2,
                            },
                            contentChanges = new[]
                            {
                                new
                                {
                                    text = ChangedTemplate,
                                },
                            },
                        }),
                    CreateRequest(
                        id: 2,
                        method: "textDocument/completion",
                        parameters: new
                        {
                            textDocument = new
                            {
                                uri = templateUri,
                            },
                            position = new
                            {
                                line = 3,
                                character = 1,
                            },
                        }),
                    CreateRequest(
                        id: 3,
                        method: "textDocument/semanticTokens/full",
                        parameters: new
                        {
                            textDocument = new
                            {
                                uri = templateUri,
                            },
                        }),
                    CreateRequest(id: 4, method: "shutdown", parameters: new { }), CreateNotification(method: "exit", parameters: new { })
                ]);
            await using var output = new MemoryStream();
            using var connection = new JsonRpcConnection(input: input, output: output);
            using var templateFeatureService = new TemplateFeatureService();
            var host = new LanguageServerHost(
                connection: connection,
                diagnosticService: new NoopDiagnosticService(),
                featureService: templateFeatureService);

            await host.RunAsync(cancellationToken: CancellationToken.None);

            var messages = ReadOutputMessages(output: output);
            var initializeResponse = FindResponse(messages: messages, id: 1);
            Assert.True(
                condition: initializeResponse
                    .GetProperty(propertyName: "result")
                    .GetProperty(propertyName: "capabilities")
                    .GetProperty(propertyName: "completionProvider")
                    .GetProperty(propertyName: "resolveProvider")
                    .ValueKind is JsonValueKind.False);
            var semanticLegend = new List<string?>();
            var legendEnumerator = initializeResponse
                .GetProperty(propertyName: "result")
                .GetProperty(propertyName: "capabilities")
                .GetProperty(propertyName: "semanticTokensProvider")
                .GetProperty(propertyName: "legend")
                .GetProperty(propertyName: "tokenTypes")
                .EnumerateArray();
            using (legendEnumerator)
            {
                while (legendEnumerator.MoveNext())
                {
                    semanticLegend.Add(item: legendEnumerator.Current.GetString());
                }
            }

            Assert.Contains(expected: "macro", collection: semanticLegend);
            Assert.Contains(expected: "keyword", collection: semanticLegend);

            var completionResponse = FindResponse(messages: messages, id: 2);
            var labels = new List<string?>();
            var resultEnumerator = completionResponse.GetProperty(propertyName: "result").GetProperty(propertyName: "items").EnumerateArray();
            using (resultEnumerator)
            {
                while (resultEnumerator.MoveNext())
                {
                    labels.Add(item: resultEnumerator.Current.GetProperty(propertyName: "label").GetString());
                }
            }

            Assert.Contains(expected: "Classes", collection: labels);
            Assert.Contains(expected: "FormatName", collection: labels);

            var semanticTokensResponse = FindResponse(messages: messages, id: 3);
            var semanticTokensEnumerator = semanticTokensResponse
                .GetProperty(propertyName: "result")
                .GetProperty(propertyName: "data")
                .EnumerateArray();
            using (semanticTokensEnumerator)
            {
                Assert.True(condition: semanticTokensEnumerator.MoveNext());
            }

            var shutdownResponse = FindResponse(messages: messages, id: 4);
            Assert.True(condition: shutdownResponse.TryGetProperty(propertyName: "result", value: out _));
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(directory: directory);
        }
    }

    [Fact]
    public void FileUriPathStripsWindowsDrivePrefixSlash()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var path = FileUriPath.TryGetPath(uri: "file:///d%3A/Github/Test/Models.tst");

        Assert.Equal(
            expected: Path.GetFullPath(path: @"d:\Github\Test\Models.tst"),
            actual: Path.GetFullPath(path: path!));
    }

    private static MemoryStream CreateInput(params object[] messages)
    {
        var stream = new MemoryStream();
        foreach (var message in messages)
        {
            WriteMessage(stream: stream, message: message);
        }

        stream.Position = 0;
        return stream;
    }

    private static object CreateRequest(
        int id,
        string method,
        object parameters) =>
        new
        {
            jsonrpc = "2.0",
            id,
            method,
            @params = parameters,
        };

    private static object CreateNotification(
        string method,
        object parameters) =>
        new
        {
            jsonrpc = "2.0",
            method,
            @params = parameters,
        };

    private static void WriteMessage(
        Stream stream,
        object message)
    {
        var content = JsonSerializer.SerializeToUtf8Bytes(value: message, options: JsonOptions);
        var header = Encoding.ASCII.GetBytes(s: $"Content-Length: {content.Length}\r\n\r\n");
        stream.Write(buffer: header);
        stream.Write(buffer: content);
    }

    private static IReadOnlyList<JsonElement> ReadOutputMessages(MemoryStream output)
    {
        var content = Encoding.UTF8.GetString(bytes: output.ToArray());
        var messages = new List<JsonElement>();
        var offset = 0;
        while (offset < content.Length)
        {
            var headerEnd = content.IndexOf(value: "\r\n\r\n", startIndex: offset, comparisonType: StringComparison.Ordinal);
            if (headerEnd < 0)
            {
                break;
            }

            var header = content[offset..headerEnd];
            var contentLength = header
                .Split(separator: "\r\n", options: StringSplitOptions.RemoveEmptyEntries)
                .Select(selector: line => line.Split(separator: ':', count: 2, options: StringSplitOptions.TrimEntries))
                .Where(predicate: parts => parts.Length == 2 && parts[0].Equals(value: "Content-Length", comparisonType: StringComparison.OrdinalIgnoreCase))
                .Select(selector: parts => int.Parse(s: parts[1], provider: System.Globalization.CultureInfo.InvariantCulture))
                .Single();
            var payloadStart = headerEnd + 4;
            var payload = content.Substring(startIndex: payloadStart, length: contentLength);
            using var document = JsonDocument.Parse(json: payload);
            messages.Add(item: document.RootElement.Clone());
            offset = payloadStart + contentLength;
        }

        return messages;
    }

    private static JsonElement FindResponse(
        IEnumerable<JsonElement> messages,
        int id) =>
        messages.Single(
            predicate: message => message.TryGetProperty(propertyName: nameof(id), value: out var idElement)
                                  && idElement.ValueKind == JsonValueKind.Number
                                  && idElement.GetInt32() == id);

    private static string CreateProjectDirectory()
    {
        var directory = Path.Combine(
            path1: Path.GetTempPath(),
            path2: "Typewriter.LanguageServer.Tests",
            path3: Guid.NewGuid().ToString(format: "N"));
        Directory.CreateDirectory(path: directory);
        return directory;
    }

    private static async Task DeleteDirectoryWithRetryAsync(string directory)
    {
        const int MaxAttempts = 10;

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                if (Directory.Exists(path: directory))
                {
                    Directory.Delete(path: directory, recursive: true);
                }

                return;
            }
            catch (IOException) when (attempt < MaxAttempts)
            {
                await Task.Delay(millisecondsDelay: 100);
            }
            catch (UnauthorizedAccessException) when (attempt < MaxAttempts)
            {
                await Task.Delay(millisecondsDelay: 100);
            }
        }
    }

    private static string UriFromPath(string path) => new Uri(uriString: path).AbsoluteUri;

    private sealed class NoopDiagnosticService : ITemplateDiagnosticService
    {
        public Task<IReadOnlyList<GenerationDiagnostic>> ValidateAsync(
            TextDocumentState document,
            LanguageServerSettings settings,
            CancellationToken cancellationToken)
        {
            _ = document;
            _ = settings;
            _ = cancellationToken;
            return Task.FromResult<IReadOnlyList<GenerationDiagnostic>>(result: []);
        }
    }
}
