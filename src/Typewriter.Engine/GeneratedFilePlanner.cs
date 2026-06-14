using Typewriter.Abstractions;

namespace Typewriter.Engine;

public sealed class GeneratedFilePlanner
{
    private static readonly string CrLfGeneratedFileHeader = GeneratedFileHeader.Value.Replace(
        oldValue: "\n",
        newValue: "\r\n",
        comparisonType: StringComparison.Ordinal);

    public bool TryPlan(
        WorkspaceContext workspace,
        TemplateDocument template,
        string content,
        out GeneratedFile? generatedFile,
        out GenerationDiagnostic? diagnostic) =>
        TryPlan(
            workspace: workspace,
            template: template,
            outputPath: template.OutputPath,
            content: content,
            fileNameConvention: FileNameConvention.Preserve,
            utf8Bom: null,
            generatedFile: out generatedFile,
            diagnostic: out diagnostic);

    public bool TryPlan(
        WorkspaceContext workspace,
        TemplateDocument template,
        string? outputPath,
        string content,
        FileNameConvention fileNameConvention,
        out GeneratedFile? generatedFile,
        out GenerationDiagnostic? diagnostic) =>
        TryPlan(
            workspace: workspace,
            template: template,
            outputPath: outputPath,
            content: content,
            fileNameConvention: fileNameConvention,
            utf8Bom: null,
            generatedFile: out generatedFile,
            diagnostic: out diagnostic);

#pragma warning disable CC0091,S107,S2325
    public bool TryPlan(
        WorkspaceContext workspace,
        TemplateDocument template,
        string? outputPath,
        string content,
        FileNameConvention fileNameConvention,
        bool? utf8Bom,
        out GeneratedFile? generatedFile,
        out GenerationDiagnostic? diagnostic)
#pragma warning restore CC0091,S107,S2325
    {
        ArgumentNullException.ThrowIfNull(argument: workspace);
        ArgumentNullException.ThrowIfNull(argument: template);

        generatedFile = null;
        diagnostic = null;

        var resolvedOutputPath = ResolveOutputPath(template: template, configuredOutputPath: outputPath, fileNameConvention: fileNameConvention);
        if (!IsInsideWorkspace(workspacePath: workspace.RootPath, outputPath: resolvedOutputPath))
        {
            diagnostic = new GenerationDiagnostic(
                File: template.Path,
                Line: null,
                Column: null,
                Severity: DiagnosticSeverity.Error,
                Message: $"Output path is outside the workspace: {resolvedOutputPath}.",
                Code: DiagnosticCodes.OutputPathOutsideWorkspace);
            return false;
        }

        if (string.Equals(
            a: Path.GetFullPath(path: template.Path),
            b: resolvedOutputPath,
            comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            diagnostic = new GenerationDiagnostic(
                File: template.Path,
                Line: null,
                Column: null,
                Severity: DiagnosticSeverity.Error,
                Message: "Output filename cannot match template filename.",
                Code: DiagnosticCodes.GeneratedFileConflict);
            return false;
        }

        if (File.Exists(path: resolvedOutputPath))
        {
#pragma warning disable SCS0018,SEC0116
            var existing = File.ReadAllText(path: resolvedOutputPath);
#pragma warning restore SCS0018,SEC0116
            if (!HasGeneratedHeader(content: existing)
                && !string.Equals(a: existing, b: content, comparisonType: StringComparison.Ordinal))
            {
                diagnostic = new GenerationDiagnostic(
                    File: resolvedOutputPath,
                    Line: null,
                    Column: null,
                    Severity: DiagnosticSeverity.Error,
                    Message: "Refusing to overwrite an existing file without a Typewriter generated-file header.",
                    Code: DiagnosticCodes.GeneratedFileConflict);
                return false;
            }
        }

        generatedFile = new GeneratedFile(
            Path: resolvedOutputPath,
            Content: EnsureGeneratedHeader(content: content),
            Changed: true,
            Utf8Bom: utf8Bom);
        return true;
    }

    private static string ResolveOutputPath(
        TemplateDocument template,
        string? configuredOutputPath,
        FileNameConvention fileNameConvention)
    {
        var templateDirectory = Path.GetDirectoryName(path: Path.GetFullPath(path: template.Path))
            ?? Environment.CurrentDirectory;
        var output = configuredOutputPath;
        if (string.IsNullOrWhiteSpace(value: output))
        {
            output = FileNameConventionFormatter.Format(
                    value: Path.GetFileNameWithoutExtension(path: template.Path),
                    convention: fileNameConvention)
                + ".ts";
        }

        var combined = Path.IsPathRooted(path: output)
            ? output
            : Path.Combine(path1: templateDirectory, path2: output);

        return Path.GetFullPath(path: combined);
    }

    private static bool IsInsideWorkspace(
        string workspacePath,
        string outputPath)
    {
        var root = Path.GetFullPath(path: workspacePath);
        if (File.Exists(path: root))
        {
            root = Path.GetDirectoryName(path: root) ?? root;
        }

        if (!root.EndsWith(value: Path.DirectorySeparatorChar))
        {
            root += Path.DirectorySeparatorChar;
        }

        return outputPath.StartsWith(value: root, comparisonType: StringComparison.OrdinalIgnoreCase);
    }

    private static string EnsureGeneratedHeader(string content)
    {
        if (HasGeneratedHeader(content: content))
        {
            return content;
        }

        return $"{GeneratedFileHeader.Value}\n\n{content.TrimStart('\r', '\n')}";
    }

    private static bool HasGeneratedHeader(string content) =>
        content.StartsWith(value: GeneratedFileHeader.Value, comparisonType: StringComparison.Ordinal)
        || content.StartsWith(value: CrLfGeneratedFileHeader, comparisonType: StringComparison.Ordinal);
}
