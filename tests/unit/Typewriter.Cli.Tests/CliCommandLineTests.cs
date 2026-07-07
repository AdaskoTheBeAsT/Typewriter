using Xunit;

namespace Typewriter.Cli.Tests;

public sealed class CliCommandLineTests
{
    [Fact]
    public async Task InvokeAsyncReadsGenerateOptions()
    {
        CliOptions? capturedOptions = null;
        var command = CliCommandLine.CreateRootCommand(
            executeAsync: (options, _) =>
            {
                capturedOptions = options;
                return Task.FromResult(result: 42);
            });

        var exitCode = await command.Parse(
            args:
            [
                "generate",
                "--project",
                "sample.csproj",
                "--template",
                "models.tst",
                "--framework",
                "net10.0",
                "--output",
                "json",
                "--dry-run",
                "--all-projects",
                "--diff",
            ]).InvokeAsync(configuration: null, cancellationToken: CancellationToken.None);

        exitCode.Should().Be(42);
        capturedOptions.Should().NotBeNull();
        capturedOptions!.Command.Should().Be(CliCommand.Generate);
        capturedOptions.ProjectPath.Should().Be("sample.csproj");
        capturedOptions.TemplatePath.Should().Be("models.tst");
        capturedOptions.Framework.Should().Be("net10.0");
        capturedOptions.Output.Should().Be("json");
        capturedOptions.DryRun.Should().BeTrue();
        capturedOptions.AllProjects.Should().BeTrue();
        capturedOptions.Diff.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsyncReadsRepeatedChangedOptions()
    {
        CliOptions? capturedOptions = null;
        var command = CliCommandLine.CreateRootCommand(
            executeAsync: (options, _) =>
            {
                capturedOptions = options;
                return Task.FromResult(result: 0);
            });

        await command.Parse(
            args:
            [
                "generate",
                "--project",
                "sample.csproj",
                "--changed",
                "Models/UserDto.cs",
                "--changed",
                "Models/OrderDto.cs",
            ]).InvokeAsync(configuration: null, cancellationToken: CancellationToken.None);

        capturedOptions.Should().NotBeNull();
        capturedOptions!.ChangedPaths.Should().Equal("Models/UserDto.cs", "Models/OrderDto.cs");
    }

    [Fact]
    public async Task InvokeAsyncDefaultsChangedPathsToEmptyWhenOptionIsOmitted()
    {
        CliOptions? capturedOptions = null;
        var command = CliCommandLine.CreateRootCommand(
            executeAsync: (options, _) =>
            {
                capturedOptions = options;
                return Task.FromResult(result: 0);
            });

        await command.Parse(
            args:
            [
                "generate",
                "--project",
                "sample.csproj",
            ]).InvokeAsync(configuration: null, cancellationToken: CancellationToken.None);

        capturedOptions.Should().NotBeNull();
        capturedOptions!.ChangedPaths.Should().BeEmpty();
    }

    [Fact]
    public async Task InvokeAsyncDefaultsToGenerateWhenCommandIsOmitted()
    {
        CliOptions? capturedOptions = null;
        var command = CliCommandLine.CreateRootCommand(
            executeAsync: (options, _) =>
            {
                capturedOptions = options;
                return Task.FromResult(result: 0);
            });

        await command.Parse(
            args:
            [
                "--project",
                "sample.csproj",
                "--template",
                "models.tst",
            ]).InvokeAsync(configuration: null, cancellationToken: CancellationToken.None);

        capturedOptions.Should().NotBeNull();
        capturedOptions!.Command.Should().Be(CliCommand.Generate);
        capturedOptions.ProjectPath.Should().Be("sample.csproj");
        capturedOptions.TemplatePath.Should().Be("models.tst");
    }

    [Fact]
    public async Task InvokeAsyncReadsInitOptions()
    {
        CliOptions? capturedOptions = null;
        var command = CliCommandLine.CreateRootCommand(
            executeAsync: (options, _) =>
            {
                capturedOptions = options;
                return Task.FromResult(result: 0);
            });

        await command.Parse(
            args:
            [
                "init",
                "--workspace",
                "src",
                "--force",
            ]).InvokeAsync(configuration: null, cancellationToken: CancellationToken.None);

        capturedOptions.Should().NotBeNull();
        capturedOptions!.Command.Should().Be(CliCommand.Init);
        capturedOptions.WorkspacePath.Should().Be("src");
        capturedOptions.Force.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsyncReadsListTemplatesOptions()
    {
        CliOptions? capturedOptions = null;
        var command = CliCommandLine.CreateRootCommand(
            executeAsync: (options, _) =>
            {
                capturedOptions = options;
                return Task.FromResult(result: 0);
            });

        await command.Parse(
            args:
            [
                "list-templates",
                "--workspace",
                "src",
                "--output",
                "text",
            ]).InvokeAsync(configuration: null, cancellationToken: CancellationToken.None);

        capturedOptions.Should().NotBeNull();
        capturedOptions!.Command.Should().Be(CliCommand.ListTemplates);
        capturedOptions.WorkspacePath.Should().Be("src");
        capturedOptions.Output.Should().Be("text");
    }

    [Fact]
    public async Task InvokeAsyncReadsWatchOptions()
    {
        CliOptions? capturedOptions = null;
        var command = CliCommandLine.CreateRootCommand(
            executeAsync: (options, _) =>
            {
                capturedOptions = options;
                return Task.FromResult(result: 0);
            });

        await command.Parse(
            args:
            [
                "watch",
                "--workspace",
                "src",
                "--all-projects",
            ]).InvokeAsync(configuration: null, cancellationToken: CancellationToken.None);

        capturedOptions.Should().NotBeNull();
        capturedOptions!.Command.Should().Be(CliCommand.Watch);
        capturedOptions.WorkspacePath.Should().Be("src");
        capturedOptions.AllProjects.Should().BeTrue();
    }
}
