using System.Runtime.CompilerServices;
using AwesomeAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Moq;
using Typewriter.Abstractions;
using Typewriter.Engine;
using Xunit;

namespace Typewriter.Engine.Tests;

public sealed class TypeScriptTypeMapperTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MapDoesNotRetainPreviousMetadataSnapshots(bool semantic)
    {
        var mapper = new TypeScriptTypeMapper();
        var snapshots = Enumerable.Range(start: 0, count: 5)
            .Select(selector: _ => MapSnapshot(mapper: mapper, semantic: semantic))
            .ToArray();

        for (var attempt = 0; attempt < 10 && snapshots.Any(predicate: snapshot => snapshot.IsAlive); attempt++)
        {
            ForceGarbageCollection();
        }

        snapshots.Select(selector: snapshot => snapshot.IsAlive).Should().OnlyContain(predicate: isAlive => !isAlive);
        GC.KeepAlive(obj: mapper);
    }

    [MethodImpl(methodImplOptions: MethodImplOptions.NoInlining)]
    private static WeakReference MapSnapshot(TypeScriptTypeMapper mapper, bool semantic)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName: "Snapshot",
            syntaxTrees:
            [
                CSharpSyntaxTree.ParseText(text: "public interface IContract { } public sealed class Value : IContract { }"),
            ],
            references: [MetadataReference.CreateFromFile(path: typeof(object).Assembly.Location)],
            options: new CSharpCompilationOptions(outputKind: OutputKind.DynamicallyLinkedLibrary));
        var symbol = compilation.GetTypeByMetadataName(fullyQualifiedMetadataName: "Value")!;
        var interfaces = new Mock<IReadOnlyList<TypeMetadataReference>>(behavior: MockBehavior.Strict);
        interfaces.SetupGet(expression: list => list.Count).Returns(valueFunction: () => symbol.AllInterfaces.Length);
        var type = new TypeMetadataReference(
            Name: "Value",
            FullName: "Value",
            Namespace: string.Empty,
            IsNullable: false,
            IsCollection: false,
            IsDictionary: false,
            IsEnum: false,
            IsPrimitive: false,
            IsDateLike: false,
            ElementType: null,
            TypeArguments: [])
        {
            Interfaces = interfaces.Object,
        };

        var mapped = semantic
            ? mapper.Map(
                type: type,
                strictNull: true,
                dateMapping: TypeScriptDateMapping.Legacy(dateType: "Date", dateOnlyType: "Date", timeOnlyType: "string"),
                decimalType: null,
                guidType: null)
            : mapper.Map(type: type);

        mapped.Should().Be("Value");
        interfaces.VerifyNoOtherCalls();
        return new WeakReference(target: compilation);
    }

#pragma warning disable S1215
    private static void ForceGarbageCollection()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }
#pragma warning restore S1215
}
