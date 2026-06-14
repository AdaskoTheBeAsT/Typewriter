using Typewriter.Abstractions;

namespace Typewriter.LanguageServer;

internal static class LspDiagnosticMapper
{
    public static IReadOnlyList<LspDiagnostic> Map(
        TextDocumentState document,
        IReadOnlyList<GenerationDiagnostic> diagnostics)
    {
        var mapped = diagnostics
            .Where(predicate: diagnostic => AppliesToDocument(document: document, diagnostic: diagnostic))
            .Select(selector: ToLspDiagnostic)
            .ToList();

        if (mapped.Count > 0 || diagnostics.Count == 0)
        {
            return mapped;
        }

        return diagnostics
            .Select(selector: diagnostic => ToLspDiagnostic(diagnostic: diagnostic with { File = document.Path, Line = 1, Column = 1 }))
            .ToList();
    }

#pragma warning disable CC0021 // Use nameof
    public static LspDiagnostic ToErrorDiagnostic(string message) =>
        new(
            Range: new LspRange(
                Start: new LspPosition(Line: 0, Character: 0),
                End: new LspPosition(Line: 0, Character: 1)),
            Message: message,
            Severity: 1,
            Source: "Typewriter",
            Code: null);
#pragma warning restore CC0021 // Use nameof

    private static bool AppliesToDocument(
        TextDocumentState document,
        GenerationDiagnostic diagnostic)
    {
        if (string.IsNullOrWhiteSpace(value: diagnostic.File))
        {
            return true;
        }

        return IsSamePath(left: document.Path, right: diagnostic.File);
    }

#pragma warning disable CC0021 // Use nameof
    private static LspDiagnostic ToLspDiagnostic(GenerationDiagnostic diagnostic)
    {
        var line = Math.Max(val1: (diagnostic.Line ?? 1) - 1, val2: 0);
        var column = Math.Max(val1: (diagnostic.Column ?? 1) - 1, val2: 0);
        return new LspDiagnostic(
            Range: new LspRange(
                Start: new LspPosition(Line: line, Character: column),
                End: new LspPosition(Line: line, Character: column + 1)),
            Message: diagnostic.Message,
            Severity: MapSeverity(severity: diagnostic.Severity),
            Source: "Typewriter",
            Code: diagnostic.Code);
    }
#pragma warning restore CC0021 // Use nameof

    private static int MapSeverity(DiagnosticSeverity severity) =>
        severity switch
        {
            DiagnosticSeverity.Error => 1,
            DiagnosticSeverity.Warning => 2,
            _ => 3,
        };

    private static bool IsSamePath(
        string left,
        string right)
    {
        try
        {
            return string.Equals(
                a: Path.GetFullPath(path: left),
                b: Path.GetFullPath(path: right),
                comparisonType: StringComparison.OrdinalIgnoreCase);
        }
        catch (ArgumentException)
        {
            return string.Equals(a: left, b: right, comparisonType: StringComparison.OrdinalIgnoreCase);
        }
        catch (NotSupportedException)
        {
            return string.Equals(a: left, b: right, comparisonType: StringComparison.OrdinalIgnoreCase);
        }
    }
}
