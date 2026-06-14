using Typewriter.Abstractions;
using Typewriter.Engine;
using Xunit;

namespace Typewriter.Engine.Tests;

public sealed class OutputContentFormatterTests
{
    [Fact]
    public void FormatPreservesContentByDefault()
    {
        const string content = "export class User {\n    name: string;   \n}";

        var formatted = OutputContentFormatter.Format(content: content, output: OutputConfiguration.Default);

        Assert.Equal(expected: content, actual: formatted);
    }

    [Fact]
    public void FormatConvertsFourSpaceIndentationToTwoSpaces()
    {
        const string content = "export class User {\n    constructor(\n        public name: string\n    ) {\n    }\n}";
        var output = CreateOutput(indentStyle: IndentStyle.Space, indentSize: 2);

        var formatted = OutputContentFormatter.Format(content: content, output: output);

        Assert.Equal(
            expected: "export class User {\n  constructor(\n    public name: string\n  ) {\n  }\n}",
            actual: formatted);
    }

    [Fact]
    public void FormatConvertsSpacesToTabs()
    {
        const string content = "export class User {\n    constructor(\n        public name: string\n    ) {\n    }\n}";
        var output = CreateOutput(indentStyle: IndentStyle.Tab);

        var formatted = OutputContentFormatter.Format(content: content, output: output);

        Assert.Equal(
            expected: "export class User {\n\tconstructor(\n\t\tpublic name: string\n\t) {\n\t}\n}",
            actual: formatted);
    }

    [Fact]
    public void FormatConvertsTabsToSpaces()
    {
        const string content = "export class User {\n\tconstructor(\n\t\tpublic name: string\n\t) {\n\t}\n}";
        var output = CreateOutput(indentStyle: IndentStyle.Space, indentSize: 2);

        var formatted = OutputContentFormatter.Format(content: content, output: output);

        Assert.Equal(
            expected: "export class User {\n  constructor(\n    public name: string\n  ) {\n  }\n}",
            actual: formatted);
    }

    [Fact]
    public void FormatLeavesWhitespaceOnlyLinesUnchangedWhenReindenting()
    {
        const string content = "export class User {\n    \n    name: string;\n}";
        var output = CreateOutput(indentStyle: IndentStyle.Tab);

        var formatted = OutputContentFormatter.Format(content: content, output: output);

        Assert.Equal(expected: "export class User {\n    \n\tname: string;\n}", actual: formatted);
    }

    [Fact]
    public void FormatTrimsTrailingWhitespace()
    {
        const string content = "export class User {  \n    name: string;\t\n   \n}";
        var output = OutputConfiguration.Default with { TrimTrailingWhitespace = true };

        var formatted = OutputContentFormatter.Format(content: content, output: output);

        Assert.Equal(expected: "export class User {\n    name: string;\n\n}", actual: formatted);
    }

    [Fact]
    public void FormatInsertsFinalNewlineOnce()
    {
        var output = OutputConfiguration.Default with { InsertFinalNewline = true };

        Assert.Equal(expected: "export const a = 1;\n", actual: OutputContentFormatter.Format(content: "export const a = 1;", output: output));
        Assert.Equal(expected: "export const a = 1;\n", actual: OutputContentFormatter.Format(content: "export const a = 1;\n", output: output));
        Assert.Equal(expected: string.Empty, actual: OutputContentFormatter.Format(content: string.Empty, output: output));
    }

    [Fact]
    public void FormatAppliesCrlfAfterFormatting()
    {
        const string content = "export class User {\n    name: string;  \n}";
        var output = OutputConfiguration.Default with
        {
            Newline = "crlf",
            IndentStyle = IndentStyle.Tab,
            TrimTrailingWhitespace = true,
            InsertFinalNewline = true,
        };

        var formatted = OutputContentFormatter.Format(content: content, output: output);

        Assert.Equal(expected: "export class User {\r\n\tname: string;\r\n}\r\n", actual: formatted);
    }

    [Fact]
    public async Task WriterAppliesFormattingConfiguration()
    {
        var directory = Directory.CreateTempSubdirectory(prefix: "typewriter-format-tests").FullName;
        try
        {
            var writer = new FileSystemGeneratedFileWriter();
            var path = Path.Combine(path1: directory, path2: "user.ts");
            var configuration = TypewriterConfiguration.Default with
            {
                Output = OutputConfiguration.Default with
                {
                    IndentStyle = IndentStyle.Space,
                    IndentSize = 2,
                    TrimTrailingWhitespace = true,
                    InsertFinalNewline = true,
                },
            };
            var request = new GenerationRequest(
                WorkspacePath: directory,
                ProjectPath: null,
                TemplatePath: null,
                Mode: GenerationMode.Generate,
                Configuration: configuration);

            await writer.WriteAsync(
                file: new GeneratedFile(Path: path, Content: "export class User {\n    name: string;  \n}", Changed: true),
                request: request,
                cancellationToken: CancellationToken.None);

            Assert.Equal(
                expected: "export class User {\n  name: string;\n}\n",
                actual: await File.ReadAllTextAsync(path: path));
        }
        finally
        {
            Directory.Delete(path: directory, recursive: true);
        }
    }

    private static OutputConfiguration CreateOutput(
        IndentStyle indentStyle,
        int indentSize = 4) =>
        OutputConfiguration.Default with
        {
            IndentStyle = indentStyle,
            IndentSize = indentSize,
        };
}
