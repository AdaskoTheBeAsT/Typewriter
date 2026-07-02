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
                    CreateRequest(
                        id: 4,
                        method: "typewriter/generate",
                        parameters: new
                        {
                            command = "generate",
                            workspacePath = directory,
                            templatePath,
                        }),
                    CreateRequest(
                        id: 5,
                        method: "textDocument/semanticTokens/full",
                        parameters: new
                        {
                            textDocument = new
                            {
                                uri = templateUri,
                            },
                        }),
                    CreateRequest(id: 6, method: "shutdown", parameters: new { }), CreateNotification(method: "exit", parameters: new { })
                ]);
            await using var output = new MemoryStream();
            using var connection = new JsonRpcConnection(input: input, output: output);
            using var templateFeatureService = new TemplateFeatureService();
            var host = new LanguageServerHost(
                connection: connection,
                diagnosticService: new NoopDiagnosticService(),
                generationService: new StubGenerationService(),
                featureService: templateFeatureService);

            await host.RunAsync(cancellationToken: CancellationToken.None);

            var messages = ReadOutputMessages(output: output);
            var initializeResponse = FindResponse(messages: messages, id: 1);
            initializeResponse
                .GetProperty(propertyName: "result")
                .GetProperty(propertyName: "capabilities")
                .GetProperty(propertyName: "completionProvider")
                .GetProperty(propertyName: "resolveProvider")
                .ValueKind
                .Should()
                .Be(JsonValueKind.False);
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

            semanticLegend.Should().Contain("macro");
            semanticLegend.Should().Contain("keyword");
            initializeResponse
                .GetProperty(propertyName: "result")
                .GetProperty(propertyName: "capabilities")
                .GetProperty(propertyName: "experimental")
                .GetProperty(propertyName: "typewriterGenerationProvider")
                .GetBoolean()
                .Should()
                .BeTrue();

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

            labels.Should().Contain("Classes");
            labels.Should().Contain("FormatName");

            var semanticTokensResponse = FindResponse(messages: messages, id: 3);
            var semanticTokensEnumerator = semanticTokensResponse
                .GetProperty(propertyName: "result")
                .GetProperty(propertyName: "data")
                .EnumerateArray();
            using (semanticTokensEnumerator)
            {
                semanticTokensEnumerator.MoveNext().Should().BeTrue();
            }

            var generationResponse = FindResponse(messages: messages, id: 4);
            generationResponse
                .GetProperty(propertyName: "result")
                .GetProperty(propertyName: "durationMs")
                .GetInt64()
                .Should()
                .Be(12);
            FindResponseIndex(messages: messages, id: 5)
                .Should()
                .BeLessThan(FindResponseIndex(messages: messages, id: 4));

            var shutdownResponse = FindResponse(messages: messages, id: 6);
            shutdownResponse.TryGetProperty(propertyName: "result", value: out _).Should().BeTrue();
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(directory: directory);
        }
    }

    [Fact]
    public async Task RunAsyncHandlesEmbeddedDocumentRequestsOverJsonRpc()
    {
        var directory = CreateProjectDirectory();
        try
        {
            var templatePath = Path.Combine(path1: directory, path2: "Models.tst");
            var templateUri = UriFromPath(path: templatePath);
            const string Template = "${\n"
                + "    string Suffix(Typewriter.CodeModel.Class c) => c.Name;\n"
                + "}\n"
                + "export const url = `${environment.apiBaseUrl}/api`;\n";

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
                                text = Template,
                            },
                        }),
                    CreateRequest(
                        id: 2,
                        method: "typewriter/embeddedDocument",
                        parameters: new
                        {
                            textDocument = new
                            {
                                uri = templateUri,
                            },
                            kind = "typescript",
                        }),
                    CreateRequest(
                        id: 3,
                        method: "typewriter/embeddedDocument",
                        parameters: new
                        {
                            textDocument = new
                            {
                                uri = templateUri,
                            },
                            kind = "csharp",
                        }),
                    CreateRequest(
                        id: 4,
                        method: "typewriter/embeddedPosition",
                        parameters: new
                        {
                            textDocument = new
                            {
                                uri = templateUri,
                            },
                            position = new
                            {
                                line = 1,
                                character = 11,
                            },
                        }),
                    CreateRequest(
                        id: 5,
                        method: "typewriter/templateRange",
                        parameters: new
                        {
                            textDocument = new
                            {
                                uri = templateUri,
                            },
                            kind = "typescript",
                            range = new
                            {
                                start = new
                                {
                                    line = 3,
                                    character = 0,
                                },
                                end = new
                                {
                                    line = 3,
                                    character = 6,
                                },
                            },
                        }),
                    CreateRequest(id: 6, method: "shutdown", parameters: new { }), CreateNotification(method: "exit", parameters: new { })
                ]);
            await using var output = new MemoryStream();
            using var connection = new JsonRpcConnection(input: input, output: output);
            using var templateFeatureService = new TemplateFeatureService();
            var host = new LanguageServerHost(
                connection: connection,
                diagnosticService: new NoopDiagnosticService(),
                generationService: new StubGenerationService(),
                featureService: templateFeatureService);

            await host.RunAsync(cancellationToken: CancellationToken.None);

            var messages = ReadOutputMessages(output: output);
            var typeScriptResponse = FindResponse(messages: messages, id: 2);
            var typeScriptContent = typeScriptResponse
                .GetProperty(propertyName: "result")
                .GetProperty(propertyName: "content")
                .GetString();
            typeScriptContent.Should().NotContain("Suffix");
            typeScriptContent.Should().Contain("`${environment.apiBaseUrl}/api`");
            typeScriptContent.Should().HaveLength(Template.Length);

            var csharpResponse = FindResponse(messages: messages, id: 3);
            var csharpContent = csharpResponse
                .GetProperty(propertyName: "result")
                .GetProperty(propertyName: "content")
                .GetString();
            csharpContent.Should().Contain("string Suffix(Typewriter.CodeModel.Class c) => c.Name;");
            csharpContent.Should().Contain("class TypewriterTemplateHost");

            var positionResponse = FindResponse(messages: messages, id: 4);
            positionResponse
                .GetProperty(propertyName: "result")
                .GetProperty(propertyName: "kind")
                .GetString()
                .Should()
                .Be("csharp");
            var virtualPosition = positionResponse
                .GetProperty(propertyName: "result")
                .GetProperty(propertyName: "virtualPosition");
            virtualPosition.ValueKind.Should().Be(JsonValueKind.Object);

            var rangeResponse = FindResponse(messages: messages, id: 5);
            rangeResponse
                .GetProperty(propertyName: "result")
                .GetProperty(propertyName: "start")
                .GetProperty(propertyName: "line")
                .GetInt32()
                .Should()
                .Be(3);

            messages
                .Count(predicate: message => message.TryGetProperty(propertyName: "method", value: out var method)
                                             && method.ValueKind == JsonValueKind.String
                                             && string.Equals(a: method.GetString(), b: "typewriter/embeddedDocumentChanged", comparisonType: StringComparison.Ordinal))
                .Should()
                .BePositive();
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

        Path.GetFullPath(path: path!).Should().Be(Path.GetFullPath(path: @"d:\Github\Test\Models.tst"));
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

    private static int FindResponseIndex(
        IReadOnlyList<JsonElement> messages,
        int id) =>
        messages
            .Select(selector: (message, index) => new { Message = message, Index = index })
            .Single(
                predicate: item => item.Message.TryGetProperty(propertyName: nameof(id), value: out var idElement)
                                   && idElement.ValueKind == JsonValueKind.Number
                                   && idElement.GetInt32() == id)
            .Index;

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

    private sealed class StubGenerationService : IWorkspaceGenerationService
    {
        public async Task<WorkspaceGenerationResult> GenerateAsync(
            WorkspaceGenerationRequest request,
            LanguageServerSettings settings,
            CancellationToken cancellationToken)
        {
            request.Command.Should().Be("generate");
            request.TemplatePath.Should().NotBeNullOrWhiteSpace();
            settings.WorkspacePath.Should().NotBeNullOrWhiteSpace();
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(millisecondsDelay: 50, cancellationToken: cancellationToken);
            return new WorkspaceGenerationResult(
                Success: true,
                DurationMs: 12,
                GeneratedFiles: [],
                Diagnostics: []);
        }
    }
}
