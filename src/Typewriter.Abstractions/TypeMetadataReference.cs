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

    /// <summary>Gets the effective accessibility of a named type when known.</summary>
    public MetadataAccessibility? Accessibility { get; init; }

    /// <summary>Gets the identity of the declaring parameter for a generic parameter reference.</summary>
    public string? TypeParameterKey { get; init; }

    public TypeMetadataReference? ContainingType { get; init; }

    public IReadOnlyList<string> FileLocations { get; init; } = [];

    public IReadOnlyList<TypeMetadataReference> Interfaces { get; init; } = [];

    public bool IsValueTuple { get; init; }

    public IReadOnlyList<FieldMetadata> TupleElements { get; init; } = [];

    public IReadOnlyList<EnumValueMetadata> EnumValues { get; init; } = [];
}
