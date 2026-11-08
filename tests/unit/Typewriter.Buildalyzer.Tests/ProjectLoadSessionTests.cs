using AwesomeAssertions;
using Typewriter.Abstractions;
using Xunit;

namespace Typewriter.Buildalyzer.Tests;

public sealed class ProjectLoadSessionTests
{
    [Fact]
    public async Task SessionReusesOnlyItsWorkspaceAndFreshSessionRereadsProjectShape()
    {
        var directory = Path.Combine(path1: Path.GetTempPath(), path2: "Typewriter.Session-" + Guid.NewGuid().ToString(format: "N"));
        Directory.CreateDirectory(path: directory);
        try
        {
            var source = Path.Combine(path1: directory, path2: "Model.cs");
            await File.WriteAllTextAsync(path: source, contents: "public class Model { }");
            var firstPath = Path.Combine(path1: directory, path2: "First.csproj");
            var secondPath = Path.Combine(path1: directory, path2: "Second.csproj");
            const string project = "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>";
            await File.WriteAllTextAsync(path: firstPath, contents: project);
            await File.WriteAllTextAsync(path: secondPath, contents: project);
            await File.WriteAllTextAsync(
                path: Path.Combine(path1: directory, path2: "Workspace.sln"),
                contents: """
                          Microsoft Visual Studio Solution File, Format Version 12.00
                          # Visual Studio Version 17
                          Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "First", "First.csproj", "{2846F27D-E478-40B0-9477-D55A8B7B0D54}"
                          EndProject
                          Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Second", "Second.csproj", "{414A3607-F495-493A-B74C-F711A77ED763}"
                          EndProject
                          Global
                          EndGlobal
                          """);
            IProjectWorkspaceLoader loader = new MsBuildProjectLoader();
            var session = loader.CreateSession().Should().BeOfType<MsBuildProjectLoader.LoadSession>().Which;
            var context = new ProjectContext(ProjectPath: firstPath, WorkspacePath: directory, TargetFramework: "net10.0");
            using var canceled = new CancellationTokenSource();
            await canceled.CancelAsync();
            var action = async () => await session.LoadAsync(project: context, cancellationToken: canceled.Token);
            await action.Should().ThrowAsync<OperationCanceledException>();
            var first = await session.LoadAsync(project: context, cancellationToken: CancellationToken.None);
            first.Diagnostics.Should().BeEmpty();
            first.SourceFiles.Should().Contain(source);
            first.TargetFramework.Should().Be("net10.0");
            var manager = session.GetManager(workspacePath: directory);
            var second = await session.LoadAsync(project: context with { ProjectPath = secondPath }, cancellationToken: CancellationToken.None);
            second.Diagnostics.Should().BeEmpty();
            second.SourceFiles.Should().Contain(source);
            session.GetManager(workspacePath: directory).Should().BeSameAs(manager);

            await File.WriteAllTextAsync(path: firstPath, contents: project.Replace(oldValue: "</Project>", newValue: "<ItemGroup><Compile Remove=\"Model.cs\" /></ItemGroup></Project>", comparisonType: StringComparison.Ordinal));
            var freshSession = loader.CreateSession().Should().BeOfType<MsBuildProjectLoader.LoadSession>().Which;
            var changed = await freshSession.LoadAsync(project: context, cancellationToken: CancellationToken.None);
            changed.Diagnostics.Should().BeEmpty();
            changed.SourceFiles.Should().NotContain(source);
            freshSession.GetManager(workspacePath: directory).Should().NotBeSameAs(manager);

            var other = Path.Combine(path1: directory, path2: "Other");
            Directory.CreateDirectory(path: other);
            var moved = await session.LoadAsync(project: context with { WorkspacePath = other }, cancellationToken: CancellationToken.None);
            moved.Diagnostics.Should().BeEmpty();
            session.GetManager(workspacePath: other).Should().NotBeSameAs(manager);
        }
        finally
        {
            Directory.Delete(path: directory, recursive: true);
        }
    }
}
