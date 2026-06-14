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
            ]).InvokeAsync(configuration: null, cancellationToken: CancellationToken.None);

        Assert.Equal(expected: 42, actual: exitCode);
        Assert.NotNull(@object: capturedOptions);
        Assert.Equal(expected: CliCommand.Generate, actual: capturedOptions.Command);
        Assert.Equal(expected: "sample.csproj", actual: capturedOptions.ProjectPath);
        Assert.Equal(expected: "models.tst", actual: capturedOptions.TemplatePath);
        Assert.Equal(expected: "net10.0", actual: capturedOptions.Framework);
        Assert.Equal(expected: "json", actual: capturedOptions.Output);
        Assert.True(condition: capturedOptions.DryRun);
        Assert.True(condition: capturedOptions.AllProjects);
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

        Assert.NotNull(@object: capturedOptions);
        Assert.Equal(expected: CliCommand.Generate, actual: capturedOptions.Command);
        Assert.Equal(expected: "sample.csproj", actual: capturedOptions.ProjectPath);
        Assert.Equal(expected: "models.tst", actual: capturedOptions.TemplatePath);
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

        Assert.NotNull(@object: capturedOptions);
        Assert.Equal(expected: CliCommand.Init, actual: capturedOptions.Command);
        Assert.Equal(expected: "src", actual: capturedOptions.WorkspacePath);
        Assert.True(condition: capturedOptions.Force);
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

        Assert.NotNull(@object: capturedOptions);
        Assert.Equal(expected: CliCommand.ListTemplates, actual: capturedOptions.Command);
        Assert.Equal(expected: "src", actual: capturedOptions.WorkspacePath);
        Assert.Equal(expected: "text", actual: capturedOptions.Output);
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

        Assert.NotNull(@object: capturedOptions);
        Assert.Equal(expected: CliCommand.Watch, actual: capturedOptions.Command);
        Assert.Equal(expected: "src", actual: capturedOptions.WorkspacePath);
        Assert.True(condition: capturedOptions.AllProjects);
    }
}
