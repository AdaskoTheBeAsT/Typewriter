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
    public void ShouldSkipSavedInputWithoutTemplatesReturnsTrueForSolutionWhenTemplatesExistOnlyInUnrelatedSubdirectories()
    {
        var directory = CreateDirectory();
        try
        {
            var projectDirectory = Directory.CreateDirectory(path: Path.Combine(path1: directory, path2: "src", path3: "App")).FullName;
            var samplesDirectory = Directory.CreateDirectory(path: Path.Combine(path1: directory, path2: "samples", path3: "SampleApp")).FullName;
            var solutionPath = Path.Combine(path1: directory, path2: "Workspace.slnx");
            var projectPath = Path.Combine(path1: projectDirectory, path2: "App.csproj");
#pragma warning disable SEC0116
            File.WriteAllText(
                path: solutionPath,
                contents: """
                          <Solution>
                            <Project Path="src/App/App.csproj" />
                          </Solution>
                          """);
            File.WriteAllText(path: projectPath, contents: "<Project />");
            File.WriteAllText(path: Path.Combine(path1: samplesDirectory, path2: "Models.tst"), contents: "$Classes[$Name]");
#pragma warning restore SEC0116

            var shouldSkip = TypewriterCommandService.ShouldSkipSavedInputWithoutTemplates(
                projectPath: projectPath,
                workspacePath: solutionPath);

            shouldSkip.Should().BeTrue();
        }
        finally
        {
            Directory.Delete(path: directory, recursive: true);
        }
    }

    [Fact]
    public void IsIgnoredInputPathAllowsProjectUnderAncestorNamedGeneratedOrBinAndHonorsConfiguredExcludeIssue121()
    {
        var baseDirectory = CreateDirectory();
        try
        {
            var ancestorGenerated = Directory.CreateDirectory(path: Path.Combine(path1: baseDirectory, path2: "Generated", path3: "bin", path4: "Workspace")).FullName;
            var nestedGenerated = Directory.CreateDirectory(path: Path.Combine(path1: ancestorGenerated, path2: "Generated")).FullName;
            var binDirectory = Directory.CreateDirectory(path: Path.Combine(path1: ancestorGenerated, path2: "bin")).FullName;
            var projectPath = Path.Combine(path1: ancestorGenerated, path2: "App.csproj");
            var modelInGeneratedFolder = Path.Combine(path1: nestedGenerated, path2: "Model.cs");
            var modelInBinFolder = Path.Combine(path1: binDirectory, path2: "Compiled.cs");
            var templateInGeneratedFolder = Path.Combine(path1: nestedGenerated, path2: "Models.tst");
#pragma warning disable SEC0116
            File.WriteAllText(path: projectPath, contents: "<Project />");
            File.WriteAllText(path: modelInGeneratedFolder, contents: "public class Model {}");
            File.WriteAllText(path: modelInBinFolder, contents: "public class Compiled {}");
            File.WriteAllText(path: templateInGeneratedFolder, contents: "$Classes[$Name]");
#pragma warning restore SEC0116

            TypewriterPathFilter.IsIgnoredInputPath(
                path: modelInGeneratedFolder,
                rootDirectory: ancestorGenerated).Should().BeFalse();
            TypewriterPathFilter.IsIgnoredInputPath(
                path: modelInBinFolder,
                rootDirectory: ancestorGenerated).Should().BeTrue();
            TypewriterPathFilter.ContainsTemplateFile(
                directory: ancestorGenerated).Should().BeTrue();

#pragma warning disable SEC0116
            File.WriteAllText(
                path: Path.Combine(path1: ancestorGenerated, path2: "typewriter.json"),
                contents: """
                          {
                            "exclude": [ "**/bin/**", "**/obj/**", "Generated" ]
                          }
                          """);
#pragma warning restore SEC0116

            TypewriterPathFilter.IsIgnoredInputPath(
                path: modelInGeneratedFolder,
                rootDirectory: ancestorGenerated).Should().BeTrue();
            TypewriterPathFilter.ContainsTemplateFile(
                directory: ancestorGenerated).Should().BeFalse();
        }
        finally
        {
            Directory.Delete(path: baseDirectory, recursive: true);
        }
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
