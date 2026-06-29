using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Typewriter.Engine.Tests;

public sealed class TemplateAssemblyLoadContextTests
{
    [Fact]
    public void LoadFromAssemblyNameReturnsExistingAssemblyForDuplicateIdentity()
    {
        var directory = CreateProjectDirectory();
        try
        {
            var firstPath = Path.Combine(path1: directory, path2: "first", path3: "DuplicateReference.dll");
            var secondPath = Path.Combine(path1: directory, path2: "second", path3: "DuplicateReference.dll");
            Directory.CreateDirectory(path: Path.GetDirectoryName(path: firstPath)!);
            Directory.CreateDirectory(path: Path.GetDirectoryName(path: secondPath)!);
            EmitAssembly(path: firstPath, assemblyName: "DuplicateReference");
            File.Copy(sourceFileName: firstPath, destFileName: secondPath);

            var contextReference = LoadDuplicateReference(firstPath: firstPath, secondPath: secondPath);
            ForceCollectibleContextCollection(contextReference: contextReference);

            contextReference.IsAlive.Should().BeFalse();
        }
        finally
        {
            DeleteDirectoryWithRetry(directory: directory);
        }
    }

    private static void EmitAssembly(
        string path,
        string assemblyName)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName: assemblyName,
            syntaxTrees:
            [
                CSharpSyntaxTree.ParseText(text: "namespace DuplicateReference; public sealed class Marker { }"),
            ],
            references: GetTrustedReferences(),
            options: new CSharpCompilationOptions(outputKind: OutputKind.DynamicallyLinkedLibrary));

        using var stream = File.Create(path: path);
        var result = compilation.Emit(peStream: stream);
        result.Success.Should().BeTrue(because: string.Join(separator: Environment.NewLine, values: result.Diagnostics));
    }

    private static WeakReference LoadDuplicateReference(
        string firstPath,
        string secondPath)
    {
        var context = new TemplateAssemblyLoadContext(name: "Typewriter.Tests", referencePaths: [secondPath]);
        var loaded = context.LoadFromAssemblyPath(assemblyPath: firstPath);
        var resolved = context.LoadFromAssemblyName(new AssemblyName("DuplicateReference"));
        resolved.Should().BeSameAs(loaded);
        var contextReference = new WeakReference(target: context);
        context.Unload();
        return contextReference;
    }

    private static IReadOnlyList<MetadataReference> GetTrustedReferences()
    {
        var trustedAssemblies = (string?)AppContext.GetData(name: "TRUSTED_PLATFORM_ASSEMBLIES");
        trustedAssemblies.Should().NotBeNullOrWhiteSpace();
        return trustedAssemblies!
            .Split(separator: Path.PathSeparator, options: StringSplitOptions.RemoveEmptyEntries)
            .Select(selector: path => MetadataReference.CreateFromFile(path: path))
            .ToArray();
    }

    private static string CreateProjectDirectory()
    {
        var directory = Path.Combine(
            path1: Path.GetTempPath(),
            path2: "Typewriter.Engine.Tests",
            path3: Guid.NewGuid().ToString(format: "N"));
        Directory.CreateDirectory(path: directory);
        return directory;
    }

#pragma warning disable S1215
    private static void ForceCollectibleContextCollection(WeakReference contextReference)
    {
        for (var attempt = 0; attempt < 10 && contextReference.IsAlive; attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
    }

    private static void DeleteDirectoryWithRetry(string directory)
    {
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            try
            {
                if (Directory.Exists(path: directory))
                {
                    Directory.Delete(path: directory, recursive: true);
                }

                return;
            }
            catch (IOException) when (attempt < 5)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
            catch (UnauthorizedAccessException) when (attempt < 5)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }
    }
#pragma warning restore S1215
}
