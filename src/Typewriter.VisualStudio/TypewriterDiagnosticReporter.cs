using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Microsoft.VisualStudio.Shell;

namespace Typewriter.VisualStudio;

internal sealed class TypewriterDiagnosticReporter : IDisposable
{
    private readonly TypewriterPackage _package;
    private readonly TaskProvider _taskProvider;

    public TypewriterDiagnosticReporter(TypewriterPackage package)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        _package = package;
        _taskProvider = new TaskProvider(provider: package)
        {
            ProviderName = "Typewriter",
        };
    }

    public void Clear()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        _taskProvider.Tasks.Clear();
    }

    public void Publish(
        IEnumerable<CliDiagnostic> diagnostics,
        string workingDirectory)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        Clear();
        foreach (var diagnostic in diagnostics)
        {
            var task = new ErrorTask
            {
                Category = TaskCategory.BuildCompile,
                ErrorCategory = MapErrorCategory(severity: diagnostic.Severity),
                Text = FormatText(diagnostic: diagnostic),
                Document = ResolveDiagnosticPath(file: diagnostic.File, workingDirectory: workingDirectory),
                Line = Math.Max(val1: (diagnostic.Line ?? 1) - 1, val2: 0),
                Column = Math.Max(val1: (diagnostic.Column ?? 1) - 1, val2: 0),
            };
            task.Navigate += (_, _) =>
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                if (!string.IsNullOrWhiteSpace(value: task.Document) && File.Exists(path: task.Document))
                {
                    VsShellUtilities.OpenDocument(provider: _package, path: task.Document);
                }
            };

            _taskProvider.Tasks.Add(task: task);
        }

        _taskProvider.Show();
    }

    public void Dispose()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        _taskProvider.Dispose();
    }

    private static string FormatText(CliDiagnostic diagnostic)
    {
        var code = string.IsNullOrWhiteSpace(value: diagnostic.Code)
            ? string.Empty
            : diagnostic.Code + " ";
        return code + diagnostic.Message;
    }

    private static TaskErrorCategory MapErrorCategory(string severity) =>
        severity.Equals(value: "error", comparisonType: StringComparison.OrdinalIgnoreCase)
            ? TaskErrorCategory.Error
            : severity.Equals(value: "warning", comparisonType: StringComparison.OrdinalIgnoreCase)
                ? TaskErrorCategory.Warning
                : TaskErrorCategory.Message;

    private static string ResolveDiagnosticPath(
        string? file,
        string workingDirectory)
    {
        var diagnosticFile = file ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value: diagnosticFile))
        {
            return string.Empty;
        }

        return Path.IsPathRooted(path: diagnosticFile)
            ? diagnosticFile
            : Path.GetFullPath(path: Path.Combine(path1: workingDirectory, path2: diagnosticFile)) ?? string.Empty;
    }
}
