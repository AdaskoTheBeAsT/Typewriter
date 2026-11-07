using Typewriter.Abstractions;

namespace Typewriter.Engine.Tests;

internal static class AdapterTestData
{
    public static TypeMetadata CreateType(string name = "Model") =>
        new(
            Name: name,
            FullName: "Sample." + name,
            Namespace: "Sample",
            Kind: TypeMetadataKind.Class,
            Accessibility: MetadataAccessibility.Public,
            Properties: [],
            Attributes: [],
            BaseTypes: [],
            EnumValues: [],
            IsNullableAware: true);

    public static TypeMetadataReference CreateReference(string name = "String", string fullName = "System.String") =>
        new(
            Name: name,
            FullName: fullName,
            Namespace: "System",
            IsNullable: false,
            IsCollection: false,
            IsDictionary: false,
            IsEnum: false,
            IsPrimitive: true,
            IsDateLike: false,
            ElementType: null,
            TypeArguments: []);

    public static ProjectMetadata CreateProject(IReadOnlyList<TypeMetadata> types, IReadOnlyList<SourceFileMetadata>? sources = null) =>
        new(ProjectPath: "Sample.csproj", SourceFiles: sources ?? [], Types: types, Diagnostics: []);
}
