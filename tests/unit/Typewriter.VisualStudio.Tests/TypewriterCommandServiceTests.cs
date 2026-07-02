using System;
using System.IO;
using Xunit;

namespace Typewriter.VisualStudio.Tests;

public sealed class TypewriterCommandServiceTests
{
    [Fact]
    public void ShouldSkipSavedInputWithoutTemplatesReturnsFalseWhenWorkspaceContainsTemplateOutsideProject()
    {
        var directory = CreateDirectory();
        try
        {
            var projectDirectory = Directory.CreateDirectory(path: Path.Combine(path1: directory, path2: "backend")).FullName;
            var projectPath = Path.Combine(path1: projectDirectory, path2: "Backend.csproj");
#pragma warning disable SEC0116
            File.WriteAllText(path: projectPath, contents: "<Project />");
            var templateDirectory = Directory.CreateDirectory(path: Path.Combine(path1: directory, path2: "templates")).FullName;
            File.WriteAllText(path: Path.Combine(path1: templateDirectory, path2: "Models.tst"), contents: "$Classes[$Name]");
#pragma warning restore SEC0116

            var shouldSkip = TypewriterCommandService.ShouldSkipSavedInputWithoutTemplates(
                projectPath: projectPath,
                workspacePath: directory);

            shouldSkip.Should().BeFalse();
        }
        finally
        {
            Directory.Delete(path: directory, recursive: true);
        }
    }

    [Fact]
    public void ShouldSkipSavedInputWithoutTemplatesReturnsTrueWhenAllKnownScopesLackTemplates()
    {
        var directory = CreateDirectory();
        try
        {
            var projectDirectory = Directory.CreateDirectory(path: Path.Combine(path1: directory, path2: "backend")).FullName;
            var projectPath = Path.Combine(path1: projectDirectory, path2: "Backend.csproj");
#pragma warning disable SEC0116
            File.WriteAllText(path: projectPath, contents: "<Project />");
#pragma warning restore SEC0116

            var shouldSkip = TypewriterCommandService.ShouldSkipSavedInputWithoutTemplates(
                projectPath: projectPath,
                workspacePath: directory);

            shouldSkip.Should().BeTrue();
        }
        finally
        {
            Directory.Delete(path: directory, recursive: true);
        }
    }

    [Fact]
    public void ShouldSkipSavedInputWithoutTemplatesReturnsFalseWhenNoScopeIsAvailable()
    {
        var shouldSkip = TypewriterCommandService.ShouldSkipSavedInputWithoutTemplates(
            projectPath: null,
            workspacePath: null);

        shouldSkip.Should().BeFalse();
    }

    private static string CreateDirectory()
    {
        var directory = Path.Combine(
            path1: Path.GetTempPath(),
            path2: "Typewriter.VisualStudio.Tests",
            path3: Guid.NewGuid().ToString(format: "N"));
        Directory.CreateDirectory(path: directory);
        return directory;
    }
}
