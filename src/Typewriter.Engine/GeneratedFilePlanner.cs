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

#pragma warning disable CC0091,MA0051,S107,S2325
    public bool TryPlan(
        WorkspaceContext workspace,
        TemplateDocument template,
        string? outputPath,
        string content,
        FileNameConvention fileNameConvention,
        bool? utf8Bom,
        out GeneratedFile? generatedFile,
        out GenerationDiagnostic? diagnostic)
#pragma warning restore CC0091,MA0051,S107,S2325
    {
        ArgumentNullException.ThrowIfNull(argument: workspace);
        ArgumentNullException.ThrowIfNull(argument: template);

        generatedFile = null;
        diagnostic = null;

        var resolvedOutputPath = ResolveOutputPath(template: template, configuredOutputPath: outputPath, fileNameConvention: fileNameConvention);

        if (resolvedOutputPath.Split(separator: [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], options: StringSplitOptions.None)
            .Any(predicate: GeneratedOutputOwnership.IsStateDirectoryName))
        {
            diagnostic = new GenerationDiagnostic(
                File: resolvedOutputPath,
                Line: null,
                Column: null,
                Severity: DiagnosticSeverity.Error,
                Message: $"Cannot generate files in the reserved '{GeneratedOutputOwnership.StateDirectoryName}' directory.",
                Code: DiagnosticCodes.GeneratedFileConflict);
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

        string? existing = null;
        if (File.Exists(path: resolvedOutputPath))
        {
#pragma warning disable SCS0018,SEC0116
            existing = File.ReadAllText(path: resolvedOutputPath);
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
        if (existing is not null)
        {
            GeneratedFileExistingContentCache.Set(file: generatedFile, content: existing);
        }

        return true;
    }

    internal static string ResolveOutputPath(
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

    internal static bool HasGeneratedHeader(ReadOnlySpan<char> content) =>
        HasHeader(content: content, header: GeneratedFileHeader.Value)
        || HasHeader(content: content, header: CrLfGeneratedFileHeader);

    private static string EnsureGeneratedHeader(string content)
    {
        if (HasGeneratedHeader(content: content))
        {
            return content;
        }

        return $"{GeneratedFileHeader.Value}\n\n{content.TrimStart('\r', '\n')}";
    }

    private static bool HasHeader(ReadOnlySpan<char> content, string header) =>
        content.StartsWith(value: header, comparisonType: StringComparison.Ordinal)
        && (content.Length == header.Length || content[header.Length] is '\r' or '\n');
}
