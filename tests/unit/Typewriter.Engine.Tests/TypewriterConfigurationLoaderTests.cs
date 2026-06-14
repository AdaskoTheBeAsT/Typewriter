using Typewriter.Abstractions;
using Typewriter.Engine;
using Xunit;

namespace Typewriter.Engine.Tests;

public sealed class TypewriterConfigurationLoaderTests
{
    [Fact]
    public async Task LoadAsyncMergesWorkspaceAndProjectConfiguration()
    {
        var root = CreateProjectDirectory();
        try
        {
            var projectDirectory = Path.Combine(path1: root, path2: "src");
            Directory.CreateDirectory(path: projectDirectory);
            var projectPath = Path.Combine(path1: projectDirectory, path2: "Sample.csproj");
            await File.WriteAllTextAsync(path: projectPath, contents: "<Project />");
            await File.WriteAllTextAsync(
                path: Path.Combine(path1: root, path2: "typewriter.json"),
                contents: """
                          {
                            "templates": [ "templates/**/*.tst" ],
                            "output": {
                              "newline": "crlf",
                              "fileNameConvention": "kebab"
                            }
                          }
                          """);
            await File.WriteAllTextAsync(
                path: Path.Combine(path1: projectDirectory, path2: "typewriter.json"),
                contents: """
                          {
                            "defaultTargetFramework": "net10.0",
                            "diagnostics": {
                              "failOnWarning": true
                            }
                          }
                          """);

            var configuration = await TypewriterConfigurationLoader.LoadAsync(
                workspacePath: root,
                projectPath: projectPath,
                cancellationToken: CancellationToken.None);

            Assert.Equal(expected: ["templates/**/*.tst"], actual: configuration.Templates);
            Assert.Equal(expected: "crlf", actual: configuration.Output.Newline);
            Assert.Equal(expected: FileNameConvention.Kebab, actual: configuration.Output.FileNameConvention);
            Assert.Equal(expected: "net10.0", actual: configuration.DefaultTargetFramework);
            Assert.True(condition: configuration.Diagnostics.FailOnWarning);
        }
        finally
        {
            Directory.Delete(path: root, recursive: true);
        }
    }

    [Fact]
    public async Task LoadAsyncReadsFormattingOptions()
    {
        var root = CreateProjectDirectory();
        try
        {
            await File.WriteAllTextAsync(
                path: Path.Combine(path1: root, path2: "typewriter.json"),
                contents: """
                          {
                            "output": {
                              "indentStyle": "space",
                              "indentSize": 2,
                              "insertFinalNewline": true,
                              "trimTrailingWhitespace": true,
                              "quoteStyle": "backtick"
                            }
                          }
                          """);

            var configuration = await TypewriterConfigurationLoader.LoadAsync(
                workspacePath: root,
                projectPath: null,
                cancellationToken: CancellationToken.None);

            Assert.Equal(expected: IndentStyle.Space, actual: configuration.Output.IndentStyle);
            Assert.Equal(expected: 2, actual: configuration.Output.IndentSize);
            Assert.True(condition: configuration.Output.InsertFinalNewline);
            Assert.True(condition: configuration.Output.TrimTrailingWhitespace);
            Assert.Equal(expected: QuoteStyle.Backtick, actual: configuration.Output.QuoteStyle);
        }
        finally
        {
            Directory.Delete(path: root, recursive: true);
        }
    }

    [Fact]
    public async Task LoadAsyncKeepsFormattingDefaultsWhenNotConfigured()
    {
        var root = CreateProjectDirectory();
        try
        {
            await File.WriteAllTextAsync(
                path: Path.Combine(path1: root, path2: "typewriter.json"),
                contents: """
                          {
                            "output": {
                              "newline": "crlf"
                            }
                          }
                          """);

            var configuration = await TypewriterConfigurationLoader.LoadAsync(
                workspacePath: root,
                projectPath: null,
                cancellationToken: CancellationToken.None);

            Assert.Equal(expected: IndentStyle.Preserve, actual: configuration.Output.IndentStyle);
            Assert.Equal(expected: 4, actual: configuration.Output.IndentSize);
            Assert.False(condition: configuration.Output.InsertFinalNewline);
            Assert.False(condition: configuration.Output.TrimTrailingWhitespace);
            Assert.Equal(expected: QuoteStyle.Double, actual: configuration.Output.QuoteStyle);
        }
        finally
        {
            Directory.Delete(path: root, recursive: true);
        }
    }

    [Fact]
    public async Task LoadAsyncReadsStrictNullAndEncoding()
    {
        var root = CreateProjectDirectory();
        try
        {
            await File.WriteAllTextAsync(
                path: Path.Combine(path1: root, path2: "typewriter.json"),
                contents: """
                          {
                            "output": {
                              "strictNull": false,
                              "encoding": "utf-8-bom"
                            }
                          }
                          """);

            var configuration = await TypewriterConfigurationLoader.LoadAsync(
                workspacePath: root,
                projectPath: null,
                cancellationToken: CancellationToken.None);

            Assert.False(condition: configuration.Output.StrictNull);
            Assert.Equal(expected: "utf-8-bom", actual: configuration.Output.Encoding);
        }
        finally
        {
            Directory.Delete(path: root, recursive: true);
        }
    }

    private static string CreateProjectDirectory()
    {
        var directory = Path.Combine(
            path1: FindRepositoryRoot(),
            path2: "tmp",
            path3: "TestProjects",
            path4: Guid.NewGuid().ToString(format: "N"));
        Directory.CreateDirectory(path: directory);
        return directory;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(path: AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(path: Path.Combine(path1: directory.FullName, path2: "AdaskoTheBeAsT.Typewriter.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return AppContext.BaseDirectory;
    }
}
