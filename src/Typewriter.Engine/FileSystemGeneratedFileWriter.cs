using System.Text;
using Typewriter.Abstractions;

namespace Typewriter.Engine;

public sealed class FileSystemGeneratedFileWriter : IGeneratedFileWriter
{
    public async Task<GeneratedFile> WriteAsync(
        GeneratedFile file,
        GenerationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(argument: file);
        ArgumentNullException.ThrowIfNull(argument: request);

        var content = OutputContentFormatter.Format(content: file.Content, output: request.Configuration.Output);
        var (changed, existingContent) = await GetChangeStateAsync(file: file, content: content, cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        var diff = request.IncludeDiff ? UnifiedDiffBuilder.Build(path: file.Path, oldContent: existingContent ?? string.Empty, newContent: content) : null;
        if (request.Configuration.Output.DryRun || request.Mode == GenerationMode.Validate)
        {
            return file with
            {
                Content = content,
                Changed = changed,
                Diff = diff,
            };
        }

        if (!changed && request.Configuration.Output.WriteOnlyWhenChanged)
        {
            return file with
            {
                Content = content,
                Changed = false,
                Diff = diff,
            };
        }

        var directory = Path.GetDirectoryName(path: file.Path);
        if (!string.IsNullOrWhiteSpace(value: directory))
        {
            Directory.CreateDirectory(path: directory);
        }

#pragma warning disable SCS0018 // Potential Path Traversal vulnerability was found where '{0}' in '{1}' may be tainted by user-controlled data from '{2}' in method '{3}'.
        await File.WriteAllTextAsync(
            path: file.Path,
            contents: content,
            encoding: CreateEncoding(utf8Bom: file.Utf8Bom, configuredEncoding: request.Configuration.Output.Encoding),
            cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
#pragma warning restore SCS0018 // Potential Path Traversal vulnerability was found where '{0}' in '{1}' may be tainted by user-controlled data from '{2}' in method '{3}'.

        return file with
        {
            Content = content,
            Changed = changed,
            Diff = diff,
        };
    }

    private static async Task<(bool Changed, string? ExistingContent)> GetChangeStateAsync(
        GeneratedFile file,
        string content,
        CancellationToken cancellationToken)
    {
        var path = file.Path;
        if (!File.Exists(path: path))
        {
            return (Changed: true, ExistingContent: null);
        }

        var existing = GeneratedFileExistingContentCache.TryGet(file: file, content: out var cached)
            ? cached
            : await ReadExistingContentAsync(path: path, cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);

        return (Changed: !string.Equals(a: existing, b: content, comparisonType: StringComparison.Ordinal), ExistingContent: existing);
    }

    private static Task<string> ReadExistingContentAsync(
        string path,
        CancellationToken cancellationToken)
    {
#pragma warning disable SCS0018 // Potential Path Traversal vulnerability was found where '{0}' in '{1}' may be tainted by user-controlled data from '{2}' in method '{3}'.
        return File.ReadAllTextAsync(path: path, cancellationToken: cancellationToken);
#pragma warning restore SCS0018 // Potential Path Traversal vulnerability was found where '{0}' in '{1}' may be tainted by user-controlled data from '{2}' in method '{3}'.
    }

    private static Encoding CreateEncoding(
        bool? utf8Bom,
        string configuredEncoding)
    {
        var emitBom = utf8Bom
            ?? configuredEncoding.Equals(value: "utf-8-bom", comparisonType: StringComparison.OrdinalIgnoreCase);
        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: emitBom);
    }
}
