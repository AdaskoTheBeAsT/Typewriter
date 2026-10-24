using AwesomeAssertions;
using Moq;
using Typewriter.Abstractions;
using Typewriter.Engine;
using Xunit;

namespace Typewriter.Engine.Tests;

public sealed class ProjectMetadataIndexTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void GetAffectedSourceFilesIncludesInterfacesAndTheirTypeArguments(bool changeTypeArgument, bool recursive)
    {
        var changedPath = Path.GetFullPath(path: "Changed.cs");
        var holderPath = Path.GetFullPath(path: "Holder.cs");
        var changedType = CreateType(
            name: changeTypeArgument ? "Payload" : "IContract",
            kind: changeTypeArgument ? TypeMetadataKind.Class : TypeMetadataKind.Interface);
        var recursiveInterfaces = new Mock<IReadOnlyList<TypeMetadataReference>>(behavior: MockBehavior.Strict);
        var payload = CreateReference(fullName: "Sample.Payload");
        var typeArgument = recursive
            ? CreateReference(fullName: "External.Container") with
            {
                TypeArguments = [payload],
                Interfaces = recursiveInterfaces.Object,
            }
            : payload;
        var contract = CreateReference(fullName: "Sample.IContract") with
        {
            TypeArguments = [typeArgument],
        };
        var reference = CreateReference(fullName: "External.Container") with
        {
            TypeArguments = [CreateReference(fullName: "System.Object")],
            Interfaces = [contract],
        };
        var holder = CreateHolder(reference: reference);
        var metadata = new ProjectMetadata(
            ProjectPath: "Sample.csproj",
            SourceFiles:
            [
                new SourceFileMetadata(Path: changedPath, Types: [changedType]),
                new SourceFileMetadata(Path: holderPath, Types: [holder]),
            ],
            Types: [changedType, holder],
            Diagnostics: []);
        var index = ProjectMetadataIndex.Create(metadata: metadata);

        var affected = index.GetAffectedSourceFiles(changedSourcePaths: [changedPath]);

        affected.Should().HaveCount(2);
        affected.Should().Contain(changedPath);
        affected.Should().Contain(holderPath);
        recursiveInterfaces.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("A")]
    [InlineData("B")]
    public void GetAffectedSourceFilesTracksNestedContainingTypeArguments(string changedType)
    {
        TypeMetadataReference[] arguments =
        [
            CreateReference(fullName: "Sample.A"),
            CreateReference(fullName: "Sample.B"),
        ];
        var sourceFiles = arguments
            .Select(
                selector: argument => new SourceFileMetadata(
                    Path: Path.GetFullPath(path: argument.Name + ".cs"),
                    Types: [CreateType(name: argument.Name, kind: TypeMetadataKind.Class)]))
            .ToArray();
        var nestedTypes = arguments
            .Select(
                selector: argument => CreateReference(fullName: "External.Outer.Inner") with
                {
                    AssemblyName = "ExternalLibrary",
                    ContainingType = CreateReference(fullName: "External.Outer") with
                    {
                        AssemblyName = "ExternalLibrary",
                        TypeArguments = [argument],
                    },
                    Interfaces =
                    [
                        CreateReference(fullName: "External.IContract") with
                        {
                            TypeArguments = [argument],
                        },
                    ],
                })
            .ToArray();
        var holder = CreateHolder(
            reference: CreateReference(fullName: "System.Tuple") with
            {
                TypeArguments = nestedTypes,
            });
        var holderPath = Path.GetFullPath(path: "Holder.cs");
        var metadata = new ProjectMetadata(
            ProjectPath: "Sample.csproj",
            SourceFiles: [.. sourceFiles, new SourceFileMetadata(Path: holderPath, Types: [holder])],
            Types: [.. sourceFiles.SelectMany(selector: file => file.Types), holder],
            Diagnostics: []);
        var index = ProjectMetadataIndex.Create(metadata: metadata);
        var changedPath = Path.GetFullPath(path: changedType + ".cs");

        var affected = index.GetAffectedSourceFiles(changedSourcePaths: [changedPath]);

        affected.Should().HaveCount(2);
        affected.Should().Contain(changedPath);
        affected.Should().Contain(holderPath);
    }

    private static TypeMetadata CreateHolder(TypeMetadataReference reference) =>
        CreateType(name: "Holder", kind: TypeMetadataKind.Class) with
        {
            Properties =
            [
                new PropertyMetadata(
                    Name: "Value",
                    FullName: "Sample.Holder.Value",
                    Type: reference,
                    Accessibility: MetadataAccessibility.Public,
                    HasGetter: true,
                    HasSetter: true,
                    IsRequired: false,
                    Attributes: []),
            ],
        };

    private static TypeMetadata CreateType(string name, TypeMetadataKind kind) =>
        new(
            Name: name,
            FullName: "Sample." + name,
            Namespace: "Sample",
            Kind: kind,
            Accessibility: MetadataAccessibility.Public,
            Properties: [],
            Attributes: [],
            BaseTypes: [],
            EnumValues: [],
            IsNullableAware: true);

    private static TypeMetadataReference CreateReference(string fullName) =>
        new(
            Name: fullName[(fullName.LastIndexOf(value: '.') + 1)..],
            FullName: fullName,
            Namespace: fullName[..fullName.LastIndexOf(value: '.')],
            IsNullable: false,
            IsCollection: false,
            IsDictionary: false,
            IsEnum: false,
            IsPrimitive: false,
            IsDateLike: false,
            ElementType: null,
            TypeArguments: []);
}
