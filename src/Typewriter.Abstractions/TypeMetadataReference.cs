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
    private readonly int? _arity;

    public int Arity
    {
        get => _arity ?? TypeArguments.Count;
        init => _arity = value;
    }

    public string AssemblyName { get; init; } = string.Empty;

    public IReadOnlyList<TypeMetadataReference> Interfaces { get; init; } = [];

    public bool IsValueTuple { get; init; }

    public IReadOnlyList<FieldMetadata> TupleElements { get; init; } = [];

    public IReadOnlyList<EnumValueMetadata> EnumValues { get; init; } = [];
}
