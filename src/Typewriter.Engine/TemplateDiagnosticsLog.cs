using System.Globalization;
using Typewriter.Abstractions;
using Typewriter.VisualStudio;

namespace Typewriter.Engine;

internal sealed class TemplateDiagnosticsLog : ILog
{
    private readonly ICollection<GenerationDiagnostic> _diagnostics;
    private readonly string _templatePath;

    public TemplateDiagnosticsLog(
        string templatePath,
        ICollection<GenerationDiagnostic> diagnostics)
    {
        _templatePath = templatePath;
        _diagnostics = diagnostics;
    }

    public void LogDebug(
        string message,
        params object[] parameters) =>
        Add(severity: DiagnosticSeverity.Info, message: message, parameters: parameters);

    public void LogInfo(
        string message,
        params object[] parameters) =>
        Add(severity: DiagnosticSeverity.Info, message: message, parameters: parameters);

    public void LogWarning(
        string message,
        params object[] parameters) =>
        Add(severity: DiagnosticSeverity.Warning, message: message, parameters: parameters);

    public void LogError(
        string message,
        params object[] parameters) =>
        Add(severity: DiagnosticSeverity.Error, message: message, parameters: parameters);

    private static string Format(
        string message,
        object[] parameters)
    {
        if (parameters is not { Length: > 0 })
        {
            return message;
        }

        try
        {
            return string.Format(provider: CultureInfo.InvariantCulture, format: message, args: parameters);
        }
        catch (FormatException)
        {
            return message;
        }
    }

    private void Add(
        DiagnosticSeverity severity,
        string message,
        object[] parameters)
    {
        _diagnostics.Add(
            item: new GenerationDiagnostic(
                File: _templatePath,
                Line: null,
                Column: null,
                Severity: severity,
                Message: Format(message: message, parameters: parameters),
                Code: DiagnosticCodes.TemplateLog));
    }
}
