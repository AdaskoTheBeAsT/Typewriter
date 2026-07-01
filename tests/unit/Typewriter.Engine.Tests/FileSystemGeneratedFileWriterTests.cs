using Typewriter.Abstractions;
using Xunit;

namespace Typewriter.Engine.Tests;

public sealed class FileSystemGeneratedFileWriterTests
{
    [Fact]
    public async Task WriteAsyncDoesNotCreateDiffForUnchangedFile()
    {
        var directory = Directory.CreateTempSubdirectory(prefix: "typewriter-writer-tests").FullName;
        try
        {
            const string content = "export const value = 1;\n";
            var path = Path.Combine(path1: directory, path2: "output.ts");
#pragma warning disable SEC0116
            await File.WriteAllTextAsync(path: path, contents: content);
#pragma warning restore SEC0116
            var request = new GenerationRequest(
                WorkspacePath: directory,
                ProjectPath: null,
                TemplatePath: null,
                Mode: GenerationMode.Generate,
                Configuration: TypewriterConfiguration.Default)
            {
                IncludeDiff = true,
            };
            var writer = new FileSystemGeneratedFileWriter();

            var result = await writer.WriteAsync(
                file: new GeneratedFile(Path: path, Content: content, Changed: true),
                request: request,
                cancellationToken: CancellationToken.None);

            result.Changed.Should().BeFalse();
            result.Diff.Should().BeNull();
        }
        finally
        {
            Directory.Delete(path: directory, recursive: true);
        }
    }
}
