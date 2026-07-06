using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Recommendations;
using Microsoft.CodeAnalysis.Text;
using Typewriter.Abstractions;
using Typewriter.Engine;

namespace Typewriter.LanguageServer;

/// <summary>
/// Forwards IntelliSense requests inside .tst C# helper blocks to a real Roslyn
/// workspace built from a virtual host document, instead of static keyword lists.
/// </summary>
internal sealed class EmbeddedCSharpLanguageService : IDisposable
{
    private const int MaxCompletionItems = 2000;

    private static readonly Lazy<IReadOnlyList<MetadataReference>> SharedReferences = new(valueFactory: CreateMetadataReferences);

    private readonly Lock _gate = new();
    private DocumentCacheEntry? _cache;
    private bool _isDisposed;

    public async Task<EmbeddedCSharpCompletions?> GetCompletionsAsync(
        TextDocumentState document,
        int templateOffset,
        IReadOnlyList<Compilation>? projectCompilations = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await UseDocumentAsync<EmbeddedCSharpCompletions?>(
                document: document,
                templateOffset: templateOffset,
                projectCompilations: projectCompilations ?? [],
                fallback: null,
                action: async (entry, virtualOffset, token) =>
                {
                    var symbols = await Recommender.GetRecommendedSymbolsAtPositionAsync(
                        document: entry.Document,
                        position: virtualOffset,
                        options: null,
                        cancellationToken: token).ConfigureAwait(continueOnCapturedContext: false);
                    var (prefix, isMemberAccess) = GetCompletionContext(text: document.Text, offset: templateOffset);
                    var items = new List<LspCompletionItem>();
                    foreach (var group in symbols.GroupBy(keySelector: symbol => symbol.Name, comparer: StringComparer.Ordinal))
                    {
                        var symbol = group.First();
                        if (string.IsNullOrEmpty(value: symbol.Name)
                            || symbol.Name.StartsWith(value: '.'))
                        {
                            continue;
                        }

                        if (!isMemberAccess
                            && IsTypeOrNamespace(symbol: symbol)
                            && (prefix.Length == 0
                                || !symbol.Name.StartsWith(value: prefix, comparisonType: StringComparison.OrdinalIgnoreCase)))
                        {
                            continue;
                        }

                        items.Add(
                            item: new LspCompletionItem(
                                Label: symbol.Name,
                                Kind: GetCompletionKind(symbol: symbol),
                                Detail: symbol.ToDisplayString(format: SymbolDisplayFormat.MinimallyQualifiedFormat),
                                Documentation: isMemberAccess ? GetDocumentationSummary(symbol: symbol) : null));
                    }

                    var ordered = items
                        .OrderBy(keySelector: item => item.Label, comparer: StringComparer.OrdinalIgnoreCase)
                        .Take(count: MaxCompletionItems)
                        .ToArray();
                    return new EmbeddedCSharpCompletions(Items: ordered, IsIncomplete: !isMemberAccess);
                },
                cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<LspHover?> GetHoverAsync(
        TextDocumentState document,
        int templateOffset,
        IReadOnlyList<Compilation>? projectCompilations = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await UseDocumentAsync<LspHover?>(
                document: document,
                templateOffset: templateOffset,
                projectCompilations: projectCompilations ?? [],
                fallback: null,
                action: async (entry, virtualOffset, token) =>
                {
                    var symbol = await FindSymbolAsync(document: entry.Document, virtualOffset: virtualOffset, cancellationToken: token).ConfigureAwait(continueOnCapturedContext: false);
                    if (symbol is null)
                    {
                        return null;
                    }

                    LspRange? range = null;
                    var root = await entry.Document.GetSyntaxRootAsync(cancellationToken: token).ConfigureAwait(continueOnCapturedContext: false);
                    if (root is not null && root.FullSpan.Length > 0)
                    {
                        var syntaxToken = root.FindToken(position: Math.Min(val1: virtualOffset, val2: root.FullSpan.End - 1));
                        range = TryCreateTemplateRange(
                            document: document,
                            virtualDocument: entry.VirtualDocument,
                            virtualStart: syntaxToken.SpanStart,
                            virtualEnd: syntaxToken.Span.End);
                    }

                    return new LspHover(
                        Contents: new LspMarkupContent(Kind: "markdown", Value: CreateHoverMarkdown(symbol: symbol)),
                        Range: range);
                },
                cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<LspLocation>> GetDefinitionsAsync(
        TextDocumentState document,
        int templateOffset,
        IReadOnlyList<Compilation>? projectCompilations = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await UseDocumentAsync<IReadOnlyList<LspLocation>>(
                document: document,
                templateOffset: templateOffset,
                projectCompilations: projectCompilations ?? [],
                fallback: [],
                action: async (entry, virtualOffset, token) =>
                {
                    var symbol = await FindSymbolAsync(document: entry.Document, virtualOffset: virtualOffset, cancellationToken: token).ConfigureAwait(continueOnCapturedContext: false);
                    if (symbol is null)
                    {
                        return [];
                    }

                    var locations = new List<LspLocation>();
                    foreach (var location in symbol.Locations.Where(predicate: location => location.IsInSource))
                    {
                        var sourceFilePath = location.SourceTree?.FilePath;
                        if (!string.IsNullOrWhiteSpace(value: sourceFilePath)
                            && Path.IsPathRooted(path: sourceFilePath)
                            && System.IO.File.Exists(path: sourceFilePath))
                        {
                            var lineSpan = location.GetLineSpan();
                            locations.Add(
                                item: new LspLocation(
                                    Uri: new Uri(uriString: Path.GetFullPath(path: sourceFilePath)).AbsoluteUri,
                                    Range: new LspRange(
                                        Start: new LspPosition(Line: lineSpan.StartLinePosition.Line, Character: lineSpan.StartLinePosition.Character),
                                        End: new LspPosition(Line: lineSpan.EndLinePosition.Line, Character: lineSpan.EndLinePosition.Character))));
                            continue;
                        }

                        var sourceSpan = location.SourceSpan;
                        var range = TryCreateTemplateRange(
                            document: document,
                            virtualDocument: entry.VirtualDocument,
                            virtualStart: sourceSpan.Start,
                            virtualEnd: sourceSpan.End);
                        if (range is not null)
                        {
                            locations.Add(item: new LspLocation(Uri: document.Uri, Range: range));
                        }
                    }

                    return locations;
                },
                cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return [];
        }
    }

    public void Dispose()
    {
        DocumentCacheEntry? entryToDispose = null;
        lock (_gate)
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            if (_cache is not null)
            {
                _cache.IsRetired = true;
                if (_cache.UseCount == 0)
                {
                    entryToDispose = _cache;
                }

                _cache = null;
            }
        }

        entryToDispose?.Workspace.Dispose();
    }

    private static async Task<ISymbol?> FindSymbolAsync(
        Document document,
        int virtualOffset,
        CancellationToken cancellationToken)
    {
        var symbol = await SymbolFinder.FindSymbolAtPositionAsync(
            document: document,
            position: virtualOffset,
            cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        if (symbol is not null)
        {
            return UnwrapAlias(symbol: symbol);
        }

        var semanticModel = await document.GetSemanticModelAsync(cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        var root = await document.GetSyntaxRootAsync(cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        if (semanticModel is null || root is null || root.FullSpan.Length == 0)
        {
            return null;
        }

        var token = root.FindToken(position: Math.Min(val1: virtualOffset, val2: root.FullSpan.End - 1));
        for (var node = token.Parent; node is not null; node = node.Parent)
        {
            var declared = semanticModel.GetDeclaredSymbol(declaration: node, cancellationToken: cancellationToken);
            if (declared is not null)
            {
                return UnwrapAlias(symbol: declared);
            }

            var symbolInfo = semanticModel.GetSymbolInfo(node: node, cancellationToken: cancellationToken);
            var resolved = symbolInfo.Symbol ?? symbolInfo.CandidateSymbols.FirstOrDefault();
            if (resolved is not null)
            {
                return UnwrapAlias(symbol: resolved);
            }
        }

        return null;
    }

    private static ISymbol UnwrapAlias(ISymbol symbol) =>
        symbol is IAliasSymbol alias ? alias.Target : symbol;

    private static DocumentCacheEntry CreateDocumentCacheEntry(
        TextDocumentState document,
        EmbeddedCSharpDocument virtualDocument,
        IReadOnlyList<Compilation> projectCompilations)
    {
#pragma warning disable IDISP001 // Dispose ownership transfers to DocumentCacheEntry
        var workspace = new AdhocWorkspace();
#pragma warning restore IDISP001
        try
        {
            var projectInfo = ProjectInfo.Create(
                    id: ProjectId.CreateNewId(),
                    version: VersionStamp.Create(),
                    name: "TypewriterTemplate",
                    assemblyName: "TypewriterTemplate",
                    language: LanguageNames.CSharp)
                .WithCompilationOptions(
                    compilationOptions: new CSharpCompilationOptions(
                        outputKind: OutputKind.DynamicallyLinkedLibrary,
                        nullableContextOptions: NullableContextOptions.Disable))
                .WithParseOptions(parseOptions: CSharpParseOptions.Default.WithLanguageVersion(version: LanguageVersion.Preview))
                .WithMetadataReferences(metadataReferences: CreateWorkspaceReferences(projectCompilations: projectCompilations));
            var project = workspace.AddProject(projectInfo: projectInfo);
            var roslynDocument = workspace.AddDocument(
                projectId: project.Id,
                name: "TypewriterTemplateHost.cs",
                text: SourceText.From(text: virtualDocument.Source));
            return new DocumentCacheEntry(
                uri: document.Uri,
                text: document.Text,
                virtualDocument: virtualDocument,
                projectCompilations: projectCompilations,
                workspace: workspace,
                document: roslynDocument);
        }
        catch
        {
            workspace.Dispose();
            throw;
        }
    }

    private static IReadOnlyList<MetadataReference> CreateWorkspaceReferences(IReadOnlyList<Compilation> projectCompilations)
    {
        if (projectCompilations.Count == 0)
        {
            return SharedReferences.Value;
        }

        var references = new Dictionary<string, MetadataReference>(comparer: StringComparer.OrdinalIgnoreCase);
        foreach (var reference in SharedReferences.Value)
        {
            var key = GetReferenceKey(reference: reference);
            if (key is not null)
            {
                references[key: key] = reference;
            }
        }

        foreach (var compilation in projectCompilations)
        {
            foreach (var reference in compilation.References)
            {
                var key = GetReferenceKey(reference: reference);
                if (key is null
                    || (key.StartsWith(value: "Typewriter.", comparisonType: StringComparison.OrdinalIgnoreCase) && references.ContainsKey(key: key)))
                {
                    continue;
                }

                references[key: key] = reference;
            }

            var assemblyName = compilation.AssemblyName;
            if (!string.IsNullOrWhiteSpace(value: assemblyName))
            {
                references[key: assemblyName] = compilation.ToMetadataReference();
            }
        }

        return references.Values.ToArray();
    }

    private static string? GetReferenceKey(MetadataReference reference) =>
        reference switch
        {
            CompilationReference compilationReference => compilationReference.Compilation.AssemblyName,
            PortableExecutableReference executableReference when !string.IsNullOrWhiteSpace(value: executableReference.FilePath) =>
                Path.GetFileNameWithoutExtension(path: executableReference.FilePath),
            _ => reference.Display,
        };

    private static bool CompilationsEqual(
        IReadOnlyList<Compilation> first,
        IReadOnlyList<Compilation> second)
    {
        if (first.Count != second.Count)
        {
            return false;
        }

        for (var index = 0; index < first.Count; index++)
        {
            if (!ReferenceEquals(objA: first[index: index], objB: second[index: index]))
            {
                return false;
            }
        }

        return true;
    }

    private static IReadOnlyList<MetadataReference> CreateMetadataReferences()
    {
        var trustedAssemblies = (AppContext.GetData(name: "TRUSTED_PLATFORM_ASSEMBLIES") as string ?? string.Empty)
            .Split(separator: Path.PathSeparator, options: StringSplitOptions.RemoveEmptyEntries);
        return trustedAssemblies
            .Concat(
            second:
            [
                typeof(TemplateDocument).Assembly.Location,
                typeof(ProjectMetadata).Assembly.Location,
            ])
            .Where(predicate: path => !string.IsNullOrWhiteSpace(value: path))
            .Distinct(comparer: StringComparer.OrdinalIgnoreCase)
            .Where(predicate: System.IO.File.Exists)
            .Select(selector: CreateMetadataReference)
            .ToArray();
    }

    private static MetadataReference CreateMetadataReference(string path)
    {
        var xmlDocumentationPath = Path.ChangeExtension(path: path, extension: ".xml");
        return System.IO.File.Exists(path: xmlDocumentationPath)
            ? MetadataReference.CreateFromFile(path: path, documentation: XmlDocumentationProvider.CreateFromFile(xmlDocCommentFilePath: xmlDocumentationPath))
            : MetadataReference.CreateFromFile(path: path);
    }

    private static (string Prefix, bool IsMemberAccess) GetCompletionContext(
        string text,
        int offset)
    {
        var end = Math.Clamp(value: offset, min: 0, max: text.Length);
        var start = end;
        while (start > 0 && IsIdentifierPart(value: text[index: start - 1]))
        {
            start--;
        }

        var prefix = text[start..end];
        var lookback = start - 1;
        while (lookback >= 0 && (text[index: lookback] == ' ' || text[index: lookback] == '\t'))
        {
            lookback--;
        }

        var isMemberAccess = lookback >= 0 && text[index: lookback] == '.';
        return (prefix.TrimStart(trimChar: '@'), isMemberAccess);
    }

    private static bool IsTypeOrNamespace(ISymbol symbol) =>
        symbol is INamespaceSymbol or ITypeSymbol;

    private static int GetCompletionKind(ISymbol symbol) =>
        symbol switch
        {
            INamespaceSymbol => CompletionKind.Module,
            ITypeSymbol type => type.TypeKind switch
            {
                TypeKind.Interface => CompletionKind.Interface,
                TypeKind.Enum => CompletionKind.Enum,
                TypeKind.Struct => CompletionKind.Struct,
                _ => CompletionKind.Class,
            },
            IMethodSymbol => CompletionKind.Method,
            IPropertySymbol => CompletionKind.Property,
            IFieldSymbol field when field.IsConst =>
                field.ContainingType?.TypeKind == TypeKind.Enum
                    ? CompletionKind.EnumMember
                    : CompletionKind.Constant,
            IFieldSymbol => CompletionKind.Field,
            IEventSymbol => CompletionKind.Event,
            _ => CompletionKind.Variable,
        };

    private static string CreateHoverMarkdown(ISymbol symbol)
    {
        var signature = symbol.ToDisplayString(format: SymbolDisplayFormat.MinimallyQualifiedFormat);
        var summary = GetDocumentationSummary(symbol: symbol);
        return string.IsNullOrWhiteSpace(value: summary)
            ? $"```csharp\n{signature}\n```"
            : $"```csharp\n{signature}\n```\n\n{summary}";
    }

    private static string? GetDocumentationSummary(ISymbol symbol)
    {
        try
        {
            var xml = symbol.GetDocumentationCommentXml();
            if (string.IsNullOrWhiteSpace(value: xml))
            {
                return null;
            }

            var summary = XElement.Parse(text: xml).Element(name: "summary")?.Value;
            return string.IsNullOrWhiteSpace(value: summary)
                ? null
                : Normalize(value: summary);
        }
        catch (System.Xml.XmlException)
        {
            return null;
        }

        static string Normalize(string value) =>
            string.Join(
                separator: ' ',
                value: value.Split(separator: ['\r', '\n'], options: StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static LspRange? TryCreateTemplateRange(
        TextDocumentState document,
        EmbeddedCSharpDocument virtualDocument,
        int virtualStart,
        int virtualEnd)
    {
        if (!virtualDocument.TryMapToTemplate(virtualOffset: virtualStart, templateOffset: out var templateStart)
            || !virtualDocument.TryMapToTemplate(virtualOffset: virtualEnd, templateOffset: out var templateEnd)
            || templateEnd < templateStart)
        {
            return null;
        }

        return new LspRange(
            Start: OffsetToPosition(text: document.Text, offset: templateStart),
            End: OffsetToPosition(text: document.Text, offset: templateEnd));
    }

    private static LspPosition OffsetToPosition(
        string text,
        int offset)
    {
        var line = 0;
        var character = 0;
        for (var index = 0; index < offset && index < text.Length; index++)
        {
            if (text[index: index] == '\n')
            {
                line++;
                character = 0;
                continue;
            }

            character++;
        }

        return new LspPosition(Line: line, Character: character);
    }

    private static bool IsIdentifierPart(char value) =>
        char.IsLetterOrDigit(c: value) || value is '_' or '@';

    private async Task<TResult> UseDocumentAsync<TResult>(
        TextDocumentState document,
        int templateOffset,
        IReadOnlyList<Compilation> projectCompilations,
        TResult fallback,
        Func<DocumentCacheEntry, int, CancellationToken, Task<TResult>> action,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(argument: action);
        DocumentCacheEntry? entry = null;
        try
        {
            entry = GetOrCreateDocument(document: document, projectCompilations: projectCompilations);
            if (entry is null
                || !entry.VirtualDocument.TryMapToVirtual(templateOffset: templateOffset, virtualOffset: out var virtualOffset))
            {
                return fallback;
            }

            return await action!(arg1: entry, arg2: virtualOffset, arg3: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        }
        finally
        {
            if (entry is not null)
            {
                ReleaseDocument(entry: entry);
            }
        }
    }

    private DocumentCacheEntry? GetOrCreateDocument(
        TextDocumentState document,
        IReadOnlyList<Compilation> projectCompilations)
    {
        DocumentCacheEntry? entryToDispose = null;
        DocumentCacheEntry? entry;
        lock (_gate)
        {
            if (_isDisposed)
            {
                return null;
            }

            if (_cache is not null
                && string.Equals(a: _cache.Uri, b: document.Uri, comparisonType: StringComparison.OrdinalIgnoreCase)
                && string.Equals(a: _cache.Text, b: document.Text, comparisonType: StringComparison.Ordinal)
                && CompilationsEqual(first: _cache.ProjectCompilations, second: projectCompilations))
            {
                _cache.UseCount++;
                return _cache;
            }

            var virtualDocument = EmbeddedCSharpDocument.Create(templateText: document.Text);
            if (virtualDocument is null)
            {
                return null;
            }

            entry = CreateDocumentCacheEntry(document: document, virtualDocument: virtualDocument, projectCompilations: projectCompilations);
            entry.UseCount++;
            if (_cache is not null)
            {
                _cache.IsRetired = true;
                if (_cache.UseCount == 0)
                {
                    entryToDispose = _cache;
                }
            }

            _cache = entry;
        }

        entryToDispose?.Workspace.Dispose();
        return entry;
    }

    private void ReleaseDocument(DocumentCacheEntry entry)
    {
        DocumentCacheEntry? entryToDispose = null;
        lock (_gate)
        {
            entry.UseCount--;
            if (entry.UseCount == 0 && entry.IsRetired)
            {
                entryToDispose = entry;
            }
        }

#pragma warning disable IDISP007 // Don't dispose injected
        entryToDispose?.Workspace.Dispose();
#pragma warning restore IDISP007 // Don't dispose injected
    }

    private static class CompletionKind
    {
        public const int Method = 2;
        public const int Field = 5;
        public const int Variable = 6;
        public const int Class = 7;
        public const int Interface = 8;
        public const int Module = 9;
        public const int Property = 10;
        public const int Enum = 13;
        public const int Struct = 22;
        public const int Event = 23;
        public const int EnumMember = 20;
        public const int Constant = 21;
    }

    private sealed class DocumentCacheEntry
    {
        public DocumentCacheEntry(
            string uri,
            string text,
            EmbeddedCSharpDocument virtualDocument,
            IReadOnlyList<Compilation> projectCompilations,
            AdhocWorkspace workspace,
            Document document)
        {
            Uri = uri;
            Text = text;
            VirtualDocument = virtualDocument;
            ProjectCompilations = projectCompilations;
            Workspace = workspace;
            Document = document;
        }

        public string Uri { get; }

        public string Text { get; }

        public EmbeddedCSharpDocument VirtualDocument { get; }

        public IReadOnlyList<Compilation> ProjectCompilations { get; }

        public AdhocWorkspace Workspace { get; }

        public Document Document { get; }

        public int UseCount { get; set; }

        public bool IsRetired { get; set; }
    }
}
