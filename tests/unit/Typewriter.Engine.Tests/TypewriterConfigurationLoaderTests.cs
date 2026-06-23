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
                            "inputExtensions": [ "cs" ],
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
                            "inputExtensions": [ "razor" ],
                            "diagnostics": {
                              "failOnWarning": true
                            }
                          }
                          """);

            var configuration = await TypewriterConfigurationLoader.LoadAsync(
                workspacePath: root,
                projectPath: projectPath,
                cancellationToken: CancellationToken.None);

            configuration.Templates.Should().Equal("templates/**/*.tst");
            configuration.InputExtensions.Should().Equal(".razor");
            configuration.Output.Newline.Should().Be("crlf");
            configuration.Output.FileNameConvention.Should().Be(FileNameConvention.Kebab);
            configuration.DefaultTargetFramework.Should().Be("net10.0");
            configuration.Diagnostics.FailOnWarning.Should().BeTrue();
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

            configuration.Output.IndentStyle.Should().Be(IndentStyle.Space);
            configuration.Output.IndentSize.Should().Be(2);
            configuration.Output.InsertFinalNewline.Should().BeTrue();
            configuration.Output.TrimTrailingWhitespace.Should().BeTrue();
            configuration.Output.QuoteStyle.Should().Be(QuoteStyle.Backtick);
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

            configuration.Output.IndentStyle.Should().Be(IndentStyle.Preserve);
            configuration.Output.IndentSize.Should().Be(4);
            configuration.Output.InsertFinalNewline.Should().BeFalse();
            configuration.Output.TrimTrailingWhitespace.Should().BeFalse();
            configuration.Output.QuoteStyle.Should().Be(QuoteStyle.Double);
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

            configuration.Output.StrictNull.Should().BeFalse();
            configuration.Output.Encoding.Should().Be("utf-8-bom");
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
