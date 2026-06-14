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
        var changed = await HasChangedAsync(path: file.Path, content: content, cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        if (request.Configuration.Output.DryRun || request.Mode == GenerationMode.Validate)
        {
            return file with
            {
                Content = content,
                Changed = changed,
            };
        }

        if (!changed && request.Configuration.Output.WriteOnlyWhenChanged)
        {
            return file with
            {
                Content = content,
                Changed = false,
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
        };
    }

    private static async Task<bool> HasChangedAsync(
        string path,
        string content,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path: path))
        {
            return true;
        }

#pragma warning disable SCS0018 // Potential Path Traversal vulnerability was found where '{0}' in '{1}' may be tainted by user-controlled data from '{2}' in method '{3}'.
        var existing = await File.ReadAllTextAsync(path: path, cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
#pragma warning restore SCS0018 // Potential Path Traversal vulnerability was found where '{0}' in '{1}' may be tainted by user-controlled data from '{2}' in method '{3}'.
        return !string.Equals(a: existing, b: content, comparisonType: StringComparison.Ordinal);
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
