using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using AwesomeAssertions;
using Microsoft.CodeAnalysis;
using Typewriter.Abstractions;
using Typewriter.Configuration;
using Typewriter.Extensions.Documentation;
using Typewriter.Extensions.WebApi;
using Typewriter.VisualStudio;
using Xunit;
using CodeModelFile = Typewriter.CodeModel.File;

namespace Typewriter.Engine.Tests;

public sealed class PublicApiCompatibilityTests
{
    [Fact]
    public void GeneratedFileRetainsPreviousConstructorSignature()
    {
        var constructor = typeof(GeneratedFile).GetConstructor(
            types: [typeof(string), typeof(string), typeof(bool), typeof(bool?)]);

        constructor.Should().NotBeNull();
    }

    [Fact]
    public void GenerationRequestRetainsPreviousConstructorSignature()
    {
        var constructor = typeof(GenerationRequest).GetConstructor(
            types:
            [
                typeof(string),
                typeof(string),
                typeof(string),
                typeof(GenerationMode),
                typeof(TypewriterConfiguration),
                typeof(bool),
                typeof(string),
            ]);

        constructor.Should().NotBeNull();
    }

    [Fact]
    public void CodeModelContractsResideInNetStandard20TypewriterCodeModelAssembly()
    {
        var codeModelAssembly = typeof(CodeModelFile).Assembly;
        var targetFramework = codeModelAssembly.GetCustomAttribute<TargetFrameworkAttribute>();

        codeModelAssembly.GetName().Name.Should().Be("Typewriter.CodeModel");
        targetFramework.Should().NotBeNull();
        targetFramework.FrameworkName.Should().Be(".NETStandard,Version=v2.0");

        typeof(Settings).Assembly.Should().BeSameAs(codeModelAssembly);
        typeof(DocCommentExtensions).Assembly.Should().BeSameAs(codeModelAssembly);
        typeof(Typewriter.Extensions.Types.TypeExtensions).Assembly.Should().BeSameAs(codeModelAssembly);
        typeof(UrlExtensions).Assembly.Should().BeSameAs(codeModelAssembly);
        typeof(ILog).Assembly.Should().BeSameAs(codeModelAssembly);
    }

    [Fact]
    public void TemplateReferenceResolverDeduplicatesExternalCodeModelAssemblyPath()
    {
        var hostCodeModelPath = typeof(CodeModelFile).Assembly.Location;
        var tempDirectory = Directory.CreateTempSubdirectory(prefix: "typewriter-codemodel-ref-");
        try
        {
            var duplicateCodeModelPath = Path.Combine(
                path1: tempDirectory.FullName,
                path2: "Typewriter.CodeModel.dll");
            File.Copy(
                sourceFileName: hostCodeModelPath,
                destFileName: duplicateCodeModelPath,
                overwrite: true);

            var diagnostics = new List<GenerationDiagnostic>();
            var resolver = new TemplateReferenceResolver(
                templatePath: Path.Combine(path1: tempDirectory.FullName, path2: "Model.tst"),
                diagnostics: diagnostics);
            var references = resolver.CreateReferences(
                referenceDirectives: [duplicateCodeModelPath]);

            diagnostics.Should().BeEmpty();

            var codeModelReferences = references
                .OfType<PortableExecutableReference>()
                .Where(predicate: reference =>
                    string.Equals(
                        a: Path.GetFileName(path: reference.FilePath),
                        b: "Typewriter.CodeModel.dll",
                        comparisonType: StringComparison.OrdinalIgnoreCase))
                .ToList();

            codeModelReferences.Should().ContainSingle();
            codeModelReferences[0].FilePath.Should().Be(hostCodeModelPath);
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }
}
