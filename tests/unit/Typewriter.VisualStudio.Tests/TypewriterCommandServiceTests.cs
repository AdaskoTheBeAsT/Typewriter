using System;
using System.IO;
using AwesomeAssertions;
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

    [Fact]
    public void CSharpBlockScannerFindsBalancedEndWithEscapedAndStringBracesIssue114()
    {
        const string template =
            "${\n"
            + "    string ModelName(Class model) => model.Name;\n\n"
            + "    string Foo(Class model)\n"
            + "    {\n"
            + "        var methodName = \"foo\";\n"
            + "        var sb = new StringBuilder();\n"
            + "        sb.AppendLine($\"public {methodName}() {{\");\n"
            + "        sb.AppendLine(\"}\");\n"
            + "        return sb.ToString();\n"
            + "    }\n"
            + "}\n"
            + "$Classes[export class $ModelName {\n"
            + "    $Foo\n"
            + "}]";

        var closeIndex = CSharpBlockScanner.FindBalancedEnd(content: template, openIndex: 1);

        closeIndex.Should().BeGreaterThan(2);
        var blockContent = template.Substring(startIndex: 2, length: closeIndex - 2);
        CSharpBlockScanner.IsCSharpBlock(block: blockContent, allowPartial: false).Should().BeTrue();
        template.Substring(startIndex: closeIndex + 1).TrimStart().Should().StartWith("$Classes[export class $ModelName");
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
