using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using AwesomeAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
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
    public void RelocatedPublicTypesAreForwardedFromEngineAndAbstractionsAssemblies()
    {
        var codeModelTypes = typeof(CodeModelFile).Assembly.GetExportedTypes();
        var engineForwardedTypes = typeof(TemplateRenderer).Assembly.GetForwardedTypes();
        var abstractionsForwardedTypes = typeof(ProjectMetadata).Assembly.GetForwardedTypes();

        engineForwardedTypes.Should().Contain(codeModelTypes);
        abstractionsForwardedTypes.Should().Contain(typeof(DateLibrary));
    }

    [Fact]
    public void PrecompiledHelperReferencingLegacyEngineAndAbstractionsAssembliesContinuesToWork()
    {
        var tempDirectory = Directory.CreateTempSubdirectory(prefix: "typewriter-forwarder-ref-");
        try
        {
            var runtimeAssemblyPath = typeof(object).Assembly.Location;
            var legacyAbstractionsTree = CSharpSyntaxTree.ParseText(
                text: """
                      namespace Typewriter.Configuration;

                      public enum DateLibrary
                      {
                          Legacy = 0,
                          Temporal = 2,
                      }
                      """);
            var legacyAbstractionsCompilation = CSharpCompilation.Create(
                assemblyName: "Typewriter.Abstractions",
                syntaxTrees: [legacyAbstractionsTree],
                references: [MetadataReference.CreateFromFile(path: runtimeAssemblyPath)],
                options: new CSharpCompilationOptions(outputKind: OutputKind.DynamicallyLinkedLibrary));
            using var legacyAbstractionsStream = new MemoryStream();
            legacyAbstractionsCompilation.Emit(peStream: legacyAbstractionsStream).Success.Should().BeTrue();
            var legacyAbstractionsImage = MetadataReference.CreateFromImage(peImage: legacyAbstractionsStream.ToArray());

            var legacyEngineTree = CSharpSyntaxTree.ParseText(
                text: """
                      using Typewriter.Configuration;

                      namespace Typewriter.CodeModel
                      {
                          public class Item
                          {
                          }

                          public class Class : Item
                          {
                              public string Name { get; set; } = string.Empty;
                          }
                      }

                      namespace Typewriter.Configuration
                      {
                          public class Settings
                          {
                              public Settings UseDateLibrary(DateLibrary dateLibrary) => this;
                          }
                      }
                      """);
            var legacyEngineCompilation = CSharpCompilation.Create(
                assemblyName: "Typewriter.Engine",
                syntaxTrees: [legacyEngineTree],
                references:
                [
                    MetadataReference.CreateFromFile(path: runtimeAssemblyPath),
                    legacyAbstractionsImage,
                ],
                options: new CSharpCompilationOptions(outputKind: OutputKind.DynamicallyLinkedLibrary));
            using var legacyEngineStream = new MemoryStream();
            legacyEngineCompilation.Emit(peStream: legacyEngineStream).Success.Should().BeTrue();
            var legacyEngineImage = MetadataReference.CreateFromImage(peImage: legacyEngineStream.ToArray());

            var helperDllPath = Path.Combine(path1: tempDirectory.FullName, path2: "LegacyCompiledHelper.dll");
            var helperTree = CSharpSyntaxTree.ParseText(
                text: """
                      using Typewriter.CodeModel;
                      using Typewriter.Configuration;

                      namespace LegacyHelperLib;

                      public static class LegacyModelHelpers
                      {
                          public static void Configure(Settings settings) =>
                              settings.UseDateLibrary(DateLibrary.Temporal);

                          public static string FormatClass(Class c) => $"Legacy:{c.Name}";
                      }
                      """);
            var helperCompilation = CSharpCompilation.Create(
                assemblyName: "LegacyCompiledHelper",
                syntaxTrees: [helperTree],
                references:
                [
                    MetadataReference.CreateFromFile(path: runtimeAssemblyPath),
                    legacyAbstractionsImage,
                    legacyEngineImage,
                ],
                options: new CSharpCompilationOptions(outputKind: OutputKind.DynamicallyLinkedLibrary));
            helperCompilation.Emit(outputPath: helperDllPath).Success.Should().BeTrue();

            var output = RenderLegacyHelperTemplate(
                tempDirectoryPath: tempDirectory.FullName,
                helperDllPath: helperDllPath,
                diagnostics: out var diagnostics);

            diagnostics.Should().BeEmpty();
            output.Trim().Should().Be("Legacy:OrderDto");
        }
        finally
        {
            DeleteDirectoryWithRetry(directory: tempDirectory.FullName);
        }
    }

    [Fact]
    public void TemplateReferenceResolverDeduplicatesByAssemblyMetadataNameAndKeepsDistinctSameFilenameHelpers()
    {
        var hostCodeModelPath = typeof(CodeModelFile).Assembly.Location;
        var runtimeAssemblyPath = typeof(object).Assembly.Location;
        var tempDirectory = Directory.CreateTempSubdirectory(prefix: "typewriter-codemodel-ref-");
        try
        {
            var renamedCodeModelPath = Path.Combine(
                path1: tempDirectory.FullName,
                path2: "CustomCodeModelCopy.dll");
            File.Copy(
                sourceFileName: hostCodeModelPath,
                destFileName: renamedCodeModelPath,
                overwrite: true);

            var dirA = Directory.CreateDirectory(path: Path.Combine(path1: tempDirectory.FullName, path2: "a")).FullName;
            var dirB = Directory.CreateDirectory(path: Path.Combine(path1: tempDirectory.FullName, path2: "b")).FullName;
            var helperAPath = Path.Combine(path1: dirA, path2: "helpers.dll");
            var helperBPath = Path.Combine(path1: dirB, path2: "helpers.dll");

            EmitEmptyHelperAssembly(assemblyName: "HelperAssemblyA", outputPath: helperAPath, runtimeAssemblyPath: runtimeAssemblyPath);
            EmitEmptyHelperAssembly(assemblyName: "HelperAssemblyB", outputPath: helperBPath, runtimeAssemblyPath: runtimeAssemblyPath);

            var diagnostics = new List<GenerationDiagnostic>();
            var resolver = new TemplateReferenceResolver(
                templatePath: Path.Combine(path1: tempDirectory.FullName, path2: "Model.tst"),
                diagnostics: diagnostics);
            var references = resolver.CreateReferences(
                referenceDirectives: [renamedCodeModelPath, helperAPath, helperBPath]);

            diagnostics.Should().BeEmpty();

            var filePaths = references
                .OfType<PortableExecutableReference>()
                .Select(selector: reference => reference.FilePath)
                .Where(predicate: path => path is not null)
                .ToList();

            filePaths.Should().Contain(hostCodeModelPath);
            filePaths.Should().NotContain(renamedCodeModelPath);
            filePaths.Should().Contain(helperAPath);
            filePaths.Should().Contain(helperBPath);
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string RenderLegacyHelperTemplate(
        string tempDirectoryPath,
        string helperDllPath,
        out List<GenerationDiagnostic> diagnostics)
    {
        var templatePath = Path.Combine(path1: tempDirectoryPath, path2: "Model.tst");
        var templateText = $$"""
                             ${
                                 #r "{{helperDllPath.Replace(oldChar: '\\', newChar: '/')}}"
                                 using LegacyHelperLib;

                                 Template(Settings settings)
                                 {
                                     LegacyModelHelpers.Configure(settings);
                                 }

                                 string LegacyName(Class c) => LegacyModelHelpers.FormatClass(c);
                             }
                             $Classes[$LegacyName]
                             """;
        diagnostics = [];
        var document = TemplateDocument.Parse(
            template: new TemplateFile(Path: templatePath, Content: templateText),
            diagnostics: diagnostics);
        var metadata = new ProjectMetadata(
            ProjectPath: Path.Combine(path1: tempDirectoryPath, path2: "Sample.csproj"),
            SourceFiles: [],
            Types:
            [
                new TypeMetadata(
                    Name: "OrderDto",
                    FullName: "Sample.OrderDto",
                    Namespace: "Sample",
                    Kind: TypeMetadataKind.Class,
                    Accessibility: MetadataAccessibility.Public,
                    Properties: [],
                    Attributes: [],
                    BaseTypes: [],
                    EnumValues: [],
                    IsNullableAware: true),
            ],
            Diagnostics: []);
        var renderer = new TemplateRenderer(typeMapper: new TypeScriptTypeMapper());
        return renderer.Render(template: document, metadata: metadata, diagnostics: diagnostics);
    }

#pragma warning disable S1215
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
                GC.Collect();
            }
            catch (UnauthorizedAccessException) when (attempt < 5)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
        }
    }
#pragma warning restore S1215

    private static void EmitEmptyHelperAssembly(
        string assemblyName,
        string outputPath,
        string runtimeAssemblyPath)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(text: $"namespace {assemblyName} {{ public static class Marker {{ }} }}");
        var compilation = CSharpCompilation.Create(
            assemblyName: assemblyName,
            syntaxTrees: [syntaxTree],
            references: [MetadataReference.CreateFromFile(path: runtimeAssemblyPath)],
            options: new CSharpCompilationOptions(outputKind: OutputKind.DynamicallyLinkedLibrary));
        compilation.Emit(outputPath: outputPath).Success.Should().BeTrue();
    }
}
