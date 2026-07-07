using System.Collections.Concurrent;
using System.Text.Json;

namespace Typewriter.LanguageServer;

internal sealed class LanguageServerHost
{
    private static readonly TimeSpan ValidationDelay = TimeSpan.FromMilliseconds(milliseconds: 250);
    private static readonly JsonSerializerOptions JsonOptions = new(defaults: JsonSerializerDefaults.Web);

    private readonly JsonRpcConnection _connection;
    private readonly ITemplateDiagnosticService _diagnosticService;
#pragma warning disable IDISP008 // Don't assign member with injected and created disposables
    private readonly IWorkspaceGenerationService _generationService;
    private readonly TemplateFeatureService _featureService;
#pragma warning restore IDISP008 // Don't assign member with injected and created disposables
    private readonly TemplateSemanticTokenService _semanticTokenService;
    private readonly ConcurrentDictionary<string, TextDocumentState> _documents = new(comparer: StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<long, Task> _pendingGenerationRequests = [];
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _pendingValidations = new(comparer: StringComparer.OrdinalIgnoreCase);
    private long _nextGenerationRequestId;
    private LanguageServerSettings _settings = LanguageServerSettings.Default;
    private bool _exitRequested;

    public LanguageServerHost(JsonRpcConnection connection)
        : this(
            connection: connection,
            diagnosticService: new TemplateDiagnosticService(),
            generationService: new WorkspaceGenerationService(),
            featureService: new TemplateFeatureService(),
            semanticTokenService: new TemplateSemanticTokenService())
    {
    }

    internal LanguageServerHost(
        JsonRpcConnection connection,
        ITemplateDiagnosticService diagnosticService,
        IWorkspaceGenerationService generationService,
        TemplateFeatureService featureService,
        TemplateSemanticTokenService? semanticTokenService = null)
    {
        _connection = connection;
        _diagnosticService = diagnosticService;
        _generationService = generationService;
        _featureService = featureService;
        _semanticTokenService = semanticTokenService ?? new TemplateSemanticTokenService();
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!_exitRequested && !cancellationToken.IsCancellationRequested)
        {
            using var message = await _connection.ReadMessageAsync(cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
            if (message is null)
            {
                return;
            }

            await HandleMessageAsync(message: message.RootElement, cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        }
    }

    private static object CreateInitializeResult() =>
        new
        {
            capabilities = new
            {
                textDocumentSync = new
                {
                    openClose = true,
                    change = 1,
                    save = new
                    {
                        includeText = false,
                    },
                },
                completionProvider = new
                {
                    triggerCharacters = new[] { "$", "(", "[", "." },
                    resolveProvider = false,
                },
                hoverProvider = true,
                definitionProvider = true,
                semanticTokensProvider = new
                {
                    legend = new
                    {
                        tokenTypes = TemplateSemanticTokenService.TokenTypes,
                        tokenModifiers = TemplateSemanticTokenService.TokenModifiers,
                    },
                    full = true,
                    range = false,
                },
                experimental = new
                {
                    typewriterGenerationProvider = true,
                    typewriterEmbeddedLanguageProvider = true,
                },
            },
            serverInfo = new
            {
                name = "Typewriter Language Server",
                version = "4.7.0",
            },
        };

    private static bool TryReadOpenedDocument(
        JsonElement parameters,
        out TextDocumentState document)
    {
        document = default!;
        if (parameters.ValueKind != JsonValueKind.Object
            || !parameters.TryGetProperty(propertyName: "textDocument", value: out var textDocument)
            || !TryReadString(element: textDocument, propertyName: "uri", value: out var uri)
            || !TryReadString(element: textDocument, propertyName: "text", value: out var text))
        {
            return false;
        }

        var version = TryReadInt(element: textDocument, propertyName: "version", value: out var documentVersion)
            ? (int?)documentVersion
            : null;
        document = new TextDocumentState(
            Uri: uri,
            Path: PathFromUri(uri: uri),
            Text: text,
            Version: version);
        return true;
    }

    private static bool TryReadTextDocumentUri(
        JsonElement parameters,
        out string uri)
    {
        uri = string.Empty;
#pragma warning disable CC0021 // Use nameof
        return parameters.ValueKind == JsonValueKind.Object
               && parameters.TryGetProperty(propertyName: "textDocument", value: out var textDocument)
               && TryReadString(element: textDocument, propertyName: "uri", value: out uri);
#pragma warning restore CC0021 // Use nameof
    }

    private static bool TryReadString(
        JsonElement element,
        string propertyName,
        out string value)
    {
        value = string.Empty;
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty(propertyName: propertyName, value: out var property)
            || property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString() ?? string.Empty;
        return true;
    }

    private static bool TryReadInt(
        JsonElement element,
        string propertyName,
        out int value)
    {
        value = 0;
        return element.ValueKind == JsonValueKind.Object
               && element.TryGetProperty(propertyName: propertyName, value: out var property)
               && property.ValueKind == JsonValueKind.Number
               && property.TryGetInt32(value: out value);
    }

    private static bool TryReadRange(
        JsonElement parameters,
        out LspRange range)
    {
        range = default!;
#pragma warning disable CC0021 // Use nameof
        if (parameters.ValueKind != JsonValueKind.Object
            || !parameters.TryGetProperty(propertyName: "range", value: out var rangeElement)
            || !TryReadPosition(element: rangeElement, propertyName: "start", position: out var start)
            || !TryReadPosition(element: rangeElement, propertyName: "end", position: out var end))
        {
            return false;
        }
#pragma warning restore CC0021 // Use nameof

        range = new LspRange(Start: start, End: end);
        return true;
    }

    private static bool TryReadPosition(
        JsonElement element,
        string propertyName,
        out LspPosition position)
    {
        position = new LspPosition(Line: 0, Character: 0);
#pragma warning disable CC0021 // Use nameof
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty(propertyName: propertyName, value: out var positionElement)
            || !TryReadInt(element: positionElement, propertyName: "line", value: out var line)
            || !TryReadInt(element: positionElement, propertyName: "character", value: out var character))
        {
            return false;
        }
#pragma warning restore CC0021 // Use nameof

        position = new LspPosition(Line: line, Character: character);
        return true;
    }

    private static string PathFromUri(string uri)
    {
        return FileUriPath.TryGetPath(uri: uri) ?? uri;
    }

    private async Task HandleMessageAsync(
        JsonElement message,
        CancellationToken cancellationToken)
    {
        if (!message.TryGetProperty(propertyName: "method", value: out var methodElement)
            || methodElement.ValueKind != JsonValueKind.String)
        {
            return;
        }

        var method = methodElement.GetString();
        var parameters = message.TryGetProperty(propertyName: "params", value: out var paramsElement)
            ? paramsElement
            : default;

        if (message.TryGetProperty(propertyName: "id", value: out var id))
        {
            if (string.Equals(a: method, b: "typewriter/generate", comparisonType: StringComparison.Ordinal))
            {
                StartGenerationRequest(
                    id: id.Clone(),
                    parameters: parameters.Clone(),
                    cancellationToken: cancellationToken);
                return;
            }

            await HandleRequestAsync(id: id, method: method, parameters: parameters, cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
            return;
        }

        await HandleNotificationAsync(method: method, parameters: parameters, cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
    }

    private async Task HandleRequestAsync(
        JsonElement id,
        string? method,
        JsonElement parameters,
        CancellationToken cancellationToken)
    {
        switch (method)
        {
            case "initialize":
                _settings = LanguageServerSettings.FromInitializeParams(parameters: parameters);
                await _connection.WriteResponseAsync(
                    id: id,
                    result: CreateInitializeResult(),
                    cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
                break;
            case "shutdown":
                await AwaitPendingGenerationRequestsAsync().ConfigureAwait(continueOnCapturedContext: false);
                await _connection.WriteResponseAsync(id: id, result: null, cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
                break;
            case "textDocument/completion":
                await WriteCompletionResponseAsync(id: id, parameters: parameters, cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
                break;
            case "textDocument/hover":
                await WriteHoverResponseAsync(id: id, parameters: parameters, cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
                break;
            case "textDocument/definition":
                await WriteDefinitionResponseAsync(id: id, parameters: parameters, cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
                break;
            case "textDocument/semanticTokens/full":
                await WriteSemanticTokensResponseAsync(id: id, parameters: parameters, cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
                break;
            case "typewriter/embeddedDocument":
                await WriteEmbeddedDocumentResponseAsync(id: id, parameters: parameters, cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
                break;
            case "typewriter/embeddedPosition":
                await WriteEmbeddedPositionResponseAsync(id: id, parameters: parameters, cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
                break;
            case "typewriter/templateRange":
                await WriteTemplateRangeResponseAsync(id: id, parameters: parameters, cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
                break;
            default:
                await _connection.WriteErrorResponseAsync(
                    id: id,
                    code: -32601,
                    message: $"Method not found: {method}",
                    cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
                break;
        }
    }

    private async Task HandleNotificationAsync(
        string? method,
        JsonElement parameters,
        CancellationToken cancellationToken)
    {
        switch (method)
        {
            case "exit":
                _exitRequested = true;
                CancelPendingValidations();
                break;
            case "textDocument/didOpen":
                if (TryReadOpenedDocument(parameters: parameters, document: out var openedDocument))
                {
                    _documents[key: openedDocument.Uri] = openedDocument;
                    ScheduleValidation(uri: openedDocument.Uri, immediate: true, cancellationToken: cancellationToken);
                    await NotifyEmbeddedDocumentChangedAsync(uri: openedDocument.Uri, cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
                }

                break;
            case "textDocument/didChange":
                if (TryApplyDocumentChange(parameters: parameters, uri: out var changedUri))
                {
                    ScheduleValidation(uri: changedUri, immediate: false, cancellationToken: cancellationToken);
                    await NotifyEmbeddedDocumentChangedAsync(uri: changedUri, cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
                }

                break;
            case "textDocument/didSave":
                if (TryApplyDocumentSave(parameters: parameters, uri: out var savedUri))
                {
                    ScheduleValidation(uri: savedUri, immediate: true, cancellationToken: cancellationToken);
                    await NotifyEmbeddedDocumentChangedAsync(uri: savedUri, cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
                }

                break;
            case "textDocument/didClose":
                if (TryReadTextDocumentUri(parameters: parameters, uri: out var closedUri))
                {
                    RemoveDocument(uri: closedUri);
                    await PublishDiagnosticsAsync(uri: closedUri, diagnostics: [], cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
                }

                break;
        }
    }

    private async Task WriteCompletionResponseAsync(
        JsonElement id,
        JsonElement parameters,
        CancellationToken cancellationToken)
    {
        if (!TryReadDocumentAndPosition(parameters: parameters, document: out var document, position: out var position))
        {
            await _connection.WriteResponseAsync(
                id: id,
                result: new LspCompletionList(IsIncomplete: false, Items: []),
                cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
            return;
        }

        var completions = await _featureService.GetCompletionsAsync(
            document: document,
            settings: _settings,
            position: position,
            cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        await _connection.WriteResponseAsync(id: id, result: completions, cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
    }

    private async Task WriteHoverResponseAsync(
        JsonElement id,
        JsonElement parameters,
        CancellationToken cancellationToken)
    {
        if (!TryReadDocumentAndPosition(parameters: parameters, document: out var document, position: out var position))
        {
            await _connection.WriteResponseAsync(id: id, result: null, cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
            return;
        }

        var hover = await _featureService.GetHoverAsync(
            document: document,
            settings: _settings,
            position: position,
            cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        await _connection.WriteResponseAsync(id: id, result: hover, cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
    }

    private async Task WriteDefinitionResponseAsync(
        JsonElement id,
        JsonElement parameters,
        CancellationToken cancellationToken)
    {
        if (!TryReadDocumentAndPosition(parameters: parameters, document: out var document, position: out var position))
        {
            await _connection.WriteResponseAsync(id: id, result: Array.Empty<LspLocation>(), cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
            return;
        }

        var locations = await _featureService.GetDefinitionsAsync(
            document: document,
            settings: _settings,
            position: position,
            cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        await _connection.WriteResponseAsync(id: id, result: locations, cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
    }

    private async Task WriteSemanticTokensResponseAsync(
        JsonElement id,
        JsonElement parameters,
        CancellationToken cancellationToken)
    {
        if (!TryReadTextDocumentUri(parameters: parameters, uri: out var uri)
            || !_documents.TryGetValue(key: uri, value: out var document))
        {
            await _connection.WriteResponseAsync(id: id, result: new LspSemanticTokens(Data: []), cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
            return;
        }

        await _connection.WriteResponseAsync(
            id: id,
            result: _semanticTokenService.GetSemanticTokens(document: document),
            cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
    }

    private async Task WriteEmbeddedDocumentResponseAsync(
        JsonElement id,
        JsonElement parameters,
        CancellationToken cancellationToken)
    {
        if (!TryReadTextDocumentUri(parameters: parameters, uri: out var uri)
            || !_documents.TryGetValue(key: uri, value: out var document)
            || !TryReadString(element: parameters, propertyName: "kind", value: out var kind))
        {
            await _connection.WriteResponseAsync(id: id, result: null, cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
            return;
        }

        await _connection.WriteResponseAsync(
            id: id,
            result: EmbeddedDocumentService.GetSnapshot(document: document, kind: kind),
            cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
    }

    private async Task WriteEmbeddedPositionResponseAsync(
        JsonElement id,
        JsonElement parameters,
        CancellationToken cancellationToken)
    {
        if (!TryReadDocumentAndPosition(parameters: parameters, document: out var document, position: out var position))
        {
            await _connection.WriteResponseAsync(id: id, result: null, cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
            return;
        }

        await _connection.WriteResponseAsync(
            id: id,
            result: EmbeddedDocumentService.GetPositionInfo(document: document, position: position),
            cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
    }

    private async Task WriteTemplateRangeResponseAsync(
        JsonElement id,
        JsonElement parameters,
        CancellationToken cancellationToken)
    {
        if (!TryReadTextDocumentUri(parameters: parameters, uri: out var uri)
            || !_documents.TryGetValue(key: uri, value: out var document)
            || !TryReadString(element: parameters, propertyName: "kind", value: out var kind)
            || !TryReadRange(parameters: parameters, range: out var range))
        {
            await _connection.WriteResponseAsync(id: id, result: null, cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
            return;
        }

        await _connection.WriteResponseAsync(
            id: id,
            result: EmbeddedDocumentService.MapRangeToTemplate(document: document, kind: kind, range: range),
            cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
    }

    private Task NotifyEmbeddedDocumentChangedAsync(
        string uri,
        CancellationToken cancellationToken)
    {
        var version = _documents.TryGetValue(key: uri, value: out var document)
            ? document.Version
            : null;
        return _connection.WriteNotificationAsync(
            method: "typewriter/embeddedDocumentChanged",
            parameters: new
            {
                uri,
                version,
            },
            cancellationToken: cancellationToken);
    }

    private async Task WriteGenerationResponseAsync(
        JsonElement id,
        JsonElement parameters,
        CancellationToken cancellationToken)
    {
        WorkspaceGenerationRequest? request;
        try
        {
            request = parameters.ValueKind == JsonValueKind.Object
                ? JsonSerializer.Deserialize<WorkspaceGenerationRequest>(json: parameters.GetRawText(), options: JsonOptions)
                : null;
        }
        catch (JsonException)
        {
            request = null;
        }

        if (request is null)
        {
            await _connection.WriteErrorResponseAsync(
                id: id,
                code: -32602,
                message: "Invalid Typewriter generation request.",
                cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
            return;
        }

        try
        {
            var result = await _generationService.GenerateAsync(
                request: request,
                settings: _settings,
                cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
            await _connection.WriteResponseAsync(id: id, result: result, cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            await _connection.WriteErrorResponseAsync(
                id: id,
                code: -32603,
                message: exception.Message,
                cancellationToken: CancellationToken.None).ConfigureAwait(continueOnCapturedContext: false);
        }
    }

    private void StartGenerationRequest(
        JsonElement id,
        JsonElement parameters,
        CancellationToken cancellationToken)
    {
        var requestId = Interlocked.Increment(location: ref _nextGenerationRequestId);
        var task = WriteGenerationResponseAsync(id: id, parameters: parameters, cancellationToken: cancellationToken);
        _pendingGenerationRequests[key: requestId] = task;
        _ = task.ContinueWith(
            continuationAction: completedTask =>
            {
                _ = completedTask.Exception;
                _ = _pendingGenerationRequests.TryRemove(key: requestId, value: out _);
            },
            cancellationToken: CancellationToken.None,
            continuationOptions: TaskContinuationOptions.ExecuteSynchronously,
            scheduler: TaskScheduler.Default);
    }

    private async Task AwaitPendingGenerationRequestsAsync()
    {
        while (_pendingGenerationRequests.Count > 0)
        {
            await Task.WhenAll(tasks: _pendingGenerationRequests.Values).ConfigureAwait(continueOnCapturedContext: false);
        }
    }

    private void ScheduleValidation(
        string uri,
        bool immediate,
        CancellationToken cancellationToken)
    {
        if (_pendingValidations.TryRemove(key: uri, value: out var existingCancellation))
        {
            existingCancellation.Cancel();
            existingCancellation.Dispose();
        }

        var validationCancellation = CancellationTokenSource.CreateLinkedTokenSource(token: cancellationToken);
        _pendingValidations[key: uri] = validationCancellation;
        _ = ValidateAfterDelayAsync(
            uri: uri,
            delay: immediate ? TimeSpan.Zero : ValidationDelay,
            validationCancellation: validationCancellation);
    }

    private async Task ValidateAfterDelayAsync(
        string uri,
        TimeSpan delay,
        CancellationTokenSource validationCancellation)
    {
        try
        {
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay: delay, cancellationToken: validationCancellation.Token).ConfigureAwait(continueOnCapturedContext: false);
            }

            if (!_documents.TryGetValue(key: uri, value: out var document))
            {
                return;
            }

            var diagnostics = await _diagnosticService.ValidateAsync(
                document: document,
                settings: _settings,
                cancellationToken: validationCancellation.Token).ConfigureAwait(continueOnCapturedContext: false);
            await PublishDiagnosticsAsync(
                uri: document.Uri,
                diagnostics: LspDiagnosticMapper.Map(document: document, diagnostics: diagnostics),
                cancellationToken: validationCancellation.Token).ConfigureAwait(continueOnCapturedContext: false);
        }
        catch (OperationCanceledException) when (validationCancellation.IsCancellationRequested)
        {
            _ = validationCancellation.IsCancellationRequested;
        }
        catch (Exception exception)
        {
            if (_documents.TryGetValue(key: uri, value: out var document))
            {
                await PublishDiagnosticsAsync(
                    uri: document.Uri,
                    diagnostics: [LspDiagnosticMapper.ToErrorDiagnostic(message: exception.Message)],
                    cancellationToken: CancellationToken.None).ConfigureAwait(continueOnCapturedContext: false);
            }
        }
        finally
        {
            if (_pendingValidations.TryGetValue(key: uri, value: out var current)
                && ReferenceEquals(objA: current, objB: validationCancellation))
            {
                _ = _pendingValidations.TryRemove(key: uri, value: out _);
            }

#pragma warning disable IDISP007 // Don't dispose injected
            validationCancellation.Dispose();
#pragma warning restore IDISP007 // Don't dispose injected
        }
    }

    private Task PublishDiagnosticsAsync(
        string uri,
        IReadOnlyList<LspDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        return _connection.WriteNotificationAsync(
            method: "textDocument/publishDiagnostics",
            parameters: new
            {
                uri,
                diagnostics,
            },
            cancellationToken: cancellationToken);
    }

    private void RemoveDocument(string uri)
    {
        _ = _documents.TryRemove(key: uri, value: out _);
        if (_pendingValidations.TryRemove(key: uri, value: out var cancellation))
        {
            cancellation.Cancel();
            cancellation.Dispose();
        }
    }

    private void CancelPendingValidations()
    {
        foreach (var pair in _pendingValidations.ToArray())
        {
            if (_pendingValidations.TryRemove(key: pair.Key, value: out var cancellation))
            {
                cancellation.Cancel();
                cancellation.Dispose();
            }
        }
    }

    private bool TryApplyDocumentChange(
        JsonElement parameters,
        out string uri)
    {
        uri = string.Empty;
        if (!TryReadTextDocumentUri(parameters: parameters, uri: out uri)
            || !_documents.TryGetValue(key: uri, value: out var current)
            || parameters.ValueKind != JsonValueKind.Object
            || !parameters.TryGetProperty(propertyName: "contentChanges", value: out var contentChanges)
            || contentChanges.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var text = current.Text;

        using var arrayEnumerator = contentChanges.EnumerateArray();
        foreach (var change in arrayEnumerator)
        {
#pragma warning disable CC0021 // Use nameof
            if (TryReadString(element: change, propertyName: "text", value: out var changedText))
            {
                text = changedText;
            }
#pragma warning restore CC0021 // Use nameof
        }

        var version = parameters.TryGetProperty(propertyName: "textDocument", value: out var textDocument)
            && TryReadInt(element: textDocument, propertyName: "version", value: out var documentVersion)
                ? documentVersion
                : current.Version;
        _documents[key: uri] = current with
        {
            Text = text,
            Version = version,
        };
        return true;
    }

    private bool TryApplyDocumentSave(
        JsonElement parameters,
        out string uri)
    {
        if (!TryReadTextDocumentUri(parameters: parameters, uri: out uri))
        {
            return false;
        }

        if (parameters.ValueKind == JsonValueKind.Object
            && parameters.TryGetProperty(propertyName: "text", value: out var textElement)
            && textElement.ValueKind == JsonValueKind.String
            && _documents.TryGetValue(key: uri, value: out var current))
        {
            _documents[key: uri] = current with
            {
                Text = textElement.GetString() ?? current.Text,
            };
        }

        return _documents.ContainsKey(key: uri);
    }

    private bool TryReadDocumentAndPosition(
        JsonElement parameters,
        out TextDocumentState document,
        out LspPosition position)
    {
        document = default!;
        position = new LspPosition(Line: 0, Character: 0);
#pragma warning disable CC0021 // Use nameof
        if (!TryReadTextDocumentUri(parameters: parameters, uri: out var uri)
            || !_documents.TryGetValue(key: uri, value: out var currentDocument)
            || parameters.ValueKind != JsonValueKind.Object
            || !parameters.TryGetProperty(propertyName: "position", value: out var positionElement)
            || !TryReadInt(element: positionElement, propertyName: "line", value: out var line)
            || !TryReadInt(element: positionElement, propertyName: "character", value: out var character))
        {
            return false;
        }
#pragma warning restore CC0021 // Use nameof

        document = currentDocument;
        position = new LspPosition(Line: line, Character: character);
        return true;
    }
}
