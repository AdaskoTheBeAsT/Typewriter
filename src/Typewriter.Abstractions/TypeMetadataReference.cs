namespace Typewriter.Abstractions;

public sealed record TypeMetadataReference(
    string Name,
    string FullName,
    string Namespace,
    bool IsNullable,
    bool IsCollection,
    bool IsDictionary,
    bool IsEnum,
    bool IsPrimitive,
    bool IsDateLike,
    TypeMetadataReference? ElementType,
    IReadOnlyList<TypeMetadataReference> TypeArguments)
{
    public string AssemblyName { get; init; } = string.Empty;

    public bool IsValueTuple { get; init; }

    public IReadOnlyList<FieldMetadata> TupleElements { get; init; } = [];

    public IReadOnlyList<EnumValueMetadata> EnumValues { get; init; } = [];
}
