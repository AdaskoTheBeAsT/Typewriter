using AwesomeAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Typewriter.CompilerServices;
using Xunit;

#pragma warning disable SEC0116 // Tests create and replace assemblies inside their own temporary directories.

namespace Typewriter.Tests;

public sealed class StampedAssemblyReferenceCacheTests
{
    [Fact]
    public async Task ConcurrentLookupsReuseReferencesAndPreserveProperties()
    {
        var cache = new StampedAssemblyReferenceCache();
        var path = typeof(object).Assembly.Location;
        var references = await Task.WhenAll(
            tasks: Enumerable.Range(start: 0, count: 32).Select(selector: _ => Task.Run(function: () => cache.GetReference(path: path))));
        references.Should().OnlyContain(predicate: reference => ReferenceEquals(reference, references[0]));
        var aliasedProperties = MetadataReferenceProperties.Assembly.WithAliases(aliases: ["custom"]);
        var aliased = cache.GetReference(path: path, properties: aliasedProperties);
        aliased.Properties.Aliases.Should().Equal("custom");
        cache.GetReference(path: path, properties: aliasedProperties).Should().BeSameAs(expected: aliased);
        aliased.Should().NotBeSameAs(unexpected: references[0]);
        cache.GetReference(path: path).Should().BeSameAs(expected: references[0]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void EvictionDoesNotInvalidateReferencesInUse(int capacity)
    {
        var cache = new StampedAssemblyReferenceCache(capacity: capacity);
        var original = cache.GetReference(path: typeof(object).Assembly.Location);
        cache.GetReference(path: typeof(StampedAssemblyReferenceCacheTests).Assembly.Location);
        cache.GetReference(path: typeof(CSharpCompilation).Assembly.Location);
        cache.Count.Should().BeLessThanOrEqualTo(expected: capacity);
        var compilation = CSharpCompilation.Create(assemblyName: "UseEvicted", references: [original]);
        compilation.GetSpecialType(specialType: SpecialType.System_Object).TypeKind.Should().NotBe(unexpected: TypeKind.Error);
    }

    [Fact]
    public void ChangedDllRebindsWithoutLockingTheOldFile()
    {
        var directory = Path.Combine(path1: Path.GetTempPath(), path2: "Typewriter.References-" + Guid.NewGuid().ToString(format: "N"));
        Directory.CreateDirectory(path: directory);
        try
        {
            var path = Path.Combine(path1: directory, path2: "Replace.dll");
            var cache = new StampedAssemblyReferenceCache();
            Emit(path: path, typeName: "Original");
            var original = cache.GetReference(path: path);
            cache.GetAssemblyIdentityKey(path: path).Should().Be(expected: "assembly:ReferenceFixture");
            var oldStamp = File.GetLastWriteTimeUtc(path: path);
            File.Delete(path: path);
            Emit(path: path, typeName: "Replacement");
            File.SetLastWriteTimeUtc(path: path, lastWriteTimeUtc: oldStamp.AddSeconds(value: 2));
            var replacement = cache.GetReference(path: path);
            replacement.Should().NotBeSameAs(unexpected: original);
            CSharpCompilation.Create(assemblyName: "Old", references: [original]).GetTypeByMetadataName(fullyQualifiedMetadataName: "Original").Should().NotBeNull();
            CSharpCompilation.Create(assemblyName: "New", references: [replacement]).GetTypeByMetadataName(fullyQualifiedMetadataName: "Replacement").Should().NotBeNull();
            File.Delete(path: path);
            Action deleted = () => cache.GetReference(path: path);
            deleted.Should().Throw<FileNotFoundException>();
        }
        finally
        {
            Directory.Delete(path: directory, recursive: true);
        }
    }

    [Fact]
    public void DuplicateAssemblyNamesPreserveTheResolversFirstMatchOrder()
    {
        var directory = Path.Combine(path1: Path.GetTempPath(), path2: "Typewriter.ReferenceOrder-" + Guid.NewGuid().ToString(format: "N"));
        Directory.CreateDirectory(path: directory);
        try
        {
            var firstPath = Path.Combine(path1: directory, path2: "First.dll");
            var secondPath = Path.Combine(path1: directory, path2: "Second.dll");
            Emit(path: firstPath, typeName: "First");
            Emit(path: secondPath, typeName: "Second");
            var cache = new StampedAssemblyReferenceCache();
            new[] { firstPath, secondPath }.DistinctBy(
                keySelector: cache.GetAssemblyIdentityKey,
                comparer: StringComparer.OrdinalIgnoreCase).Should().Equal(firstPath);
            new[] { secondPath, firstPath }.DistinctBy(
                keySelector: cache.GetAssemblyIdentityKey,
                comparer: StringComparer.OrdinalIgnoreCase).Should().Equal(secondPath);
        }
        finally
        {
            Directory.Delete(path: directory, recursive: true);
        }
    }

    [Fact]
    public void MissingAndInvalidReferencesAreRetriedAfterRepair()
    {
        var directory = Path.Combine(path1: Path.GetTempPath(), path2: "Typewriter.ReferenceRepair-" + Guid.NewGuid().ToString(format: "N"));
        Directory.CreateDirectory(path: directory);
        try
        {
            var path = Path.Combine(path1: directory, path2: "Repair.dll");
            var cache = new StampedAssemblyReferenceCache();
            cache.GetAssemblyIdentityKey(path: path).Should().Be(expected: "path:" + path);
            Action missing = () => cache.GetReference(path: path);
            missing.Should().Throw<FileNotFoundException>();
            File.WriteAllText(path: path, contents: "not an assembly");
            cache.GetAssemblyIdentityKey(path: path).Should().Be(expected: "path:" + path);

            // Invalid images must produce the same compiler diagnostics as uncached references.
            var originalDiagnostics = CSharpCompilation.Create(
                assemblyName: "Uncached",
                references: [MetadataReference.CreateFromFile(path: path)],
                options: new CSharpCompilationOptions(outputKind: OutputKind.DynamicallyLinkedLibrary)).GetDiagnostics();
            var cachedDiagnostics = CSharpCompilation.Create(
                assemblyName: "Cached",
                references: [cache.GetReference(path: path)],
                options: new CSharpCompilationOptions(outputKind: OutputKind.DynamicallyLinkedLibrary)).GetDiagnostics();
            cachedDiagnostics.Select(selector: diagnostic => diagnostic.Id).Should().Equal(
                expected: originalDiagnostics.Select(selector: diagnostic => diagnostic.Id));
            cachedDiagnostics.Should().Contain(predicate: diagnostic => diagnostic.Id == "CS0009");
            Emit(path: path, typeName: "Repaired");
            cache.GetAssemblyIdentityKey(path: path).Should().Be(expected: "assembly:ReferenceFixture");
            var repaired = cache.GetReference(path: path);
            CSharpCompilation.Create(assemblyName: "Consumer", references: [repaired]).GetTypeByMetadataName(fullyQualifiedMetadataName: "Repaired").Should().NotBeNull();
        }
        finally
        {
            Directory.Delete(path: directory, recursive: true);
        }
    }

    private static void Emit(string path, string typeName)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName: "ReferenceFixture",
            syntaxTrees: [CSharpSyntaxTree.ParseText(text: "public class " + typeName + " { }")],
            references: [MetadataReference.CreateFromFile(path: typeof(object).Assembly.Location)],
            options: new CSharpCompilationOptions(outputKind: OutputKind.DynamicallyLinkedLibrary));
        using var stream = File.Create(path: path);
        compilation.Emit(peStream: stream).Success.Should().BeTrue();
    }
}
