using AwesomeAssertions;
using Typewriter.Abstractions;
using Typewriter.Engine;
using Xunit;

namespace Typewriter.Engine.Tests;

public sealed class ProjectMetadataIndexTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GetAffectedSourceFilesIncludesInterfacesAndTheirTypeArguments(bool changeTypeArgument)
    {
        var changedPath = Path.GetFullPath(path: "Changed.cs");
        var holderPath = Path.GetFullPath(path: "Holder.cs");
        var changedType = CreateType(
            name: changeTypeArgument ? "Payload" : "IContract",
            kind: changeTypeArgument ? TypeMetadataKind.Class : TypeMetadataKind.Interface);
        var contract = CreateReference(fullName: "Sample.IContract") with
        {
            TypeArguments = [CreateReference(fullName: "Sample.Payload")],
        };
        var reference = CreateReference(fullName: "External.Container") with
        {
            Interfaces = [contract],
        };
        var holder = CreateType(name: "Holder", kind: TypeMetadataKind.Class) with
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
    }

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
