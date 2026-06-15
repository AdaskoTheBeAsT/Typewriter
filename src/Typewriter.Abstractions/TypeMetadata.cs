namespace Typewriter.Abstractions;

public sealed record TypeMetadata(
    string Name,
    string FullName,
    string Namespace,
    TypeMetadataKind Kind,
    MetadataAccessibility Accessibility,
    IReadOnlyList<PropertyMetadata> Properties,
    IReadOnlyList<AttributeMetadata> Attributes,
    IReadOnlyList<TypeMetadataReference> BaseTypes,
    IReadOnlyList<EnumValueMetadata> EnumValues,
    bool IsNullableAware)
{
    public bool IsStatic { get; init; }

    public bool IsAbstract { get; init; }

    public string AssemblyName { get; init; } = string.Empty;

    public SourceLocation? Location { get; init; }

    public IReadOnlyList<string> FileLocations { get; init; } = [];

    public string? Documentation { get; init; }

    public DocCommentMetadata? DocComment { get; init; }

    public string ContainingTypeFullName { get; init; } = string.Empty;

    public IReadOnlyList<MethodMetadata> Methods { get; init; } = [];

    public IReadOnlyList<ConstantMetadata> Constants { get; init; } = [];

    public IReadOnlyList<FieldMetadata> Fields { get; init; } = [];

    public IReadOnlyList<StaticReadOnlyFieldMetadata> StaticReadOnlyFields { get; init; } = [];

    public IReadOnlyList<EventMetadata> Events { get; init; } = [];

    public IReadOnlyList<DelegateMetadata> Delegates { get; init; } = [];

    public IReadOnlyList<TypeParameterMetadata> TypeParameters { get; init; } = [];

    public IReadOnlyList<TypeMetadataReference> TypeArguments { get; init; } = [];

    public IReadOnlyList<TypeMetadata> NestedClasses { get; init; } = [];

    public IReadOnlyList<TypeMetadata> NestedRecords { get; init; } = [];

    public IReadOnlyList<TypeMetadata> NestedStructs { get; init; } = [];

    public IReadOnlyList<TypeMetadata> NestedEnums { get; init; } = [];

    public IReadOnlyList<TypeMetadata> NestedInterfaces { get; init; } = [];
}
