using Typewriter.Configuration;

namespace Typewriter.CodeModel;

public class Type : Item
{
    public virtual IAttributeCollection Attributes { get; init; } = new AttributeCollection();

    public virtual Class? BaseClass { get; init; }

    public virtual IConstantCollection Constants { get; init; } = new ConstantCollection();

    public virtual Class? ContainingClass { get; init; }

    public virtual IDelegateCollection Delegates { get; init; } = new DelegateCollection();

    public virtual DocComment? DocComment { get; init; }

    public virtual Type? ElementType { get; init; }

    public virtual IFieldCollection Fields { get; init; } = new FieldCollection();

    public virtual IEnumerable<string> FileLocations { get; init; } = [];

    public virtual IInterfaceCollection Interfaces { get; init; } = new InterfaceCollection();

    public virtual bool IsDate { get; init; }

    public virtual bool IsDefined { get; init; }

    public virtual bool IsDictionary { get; init; }

    public virtual bool IsDynamic { get; init; }

    public virtual bool IsEnum { get; init; }

    public virtual bool IsEnumerable { get; init; }

    public virtual bool IsGeneric { get; init; }

    public virtual bool IsGuid { get; init; }

    public virtual bool IsNullable { get; init; }

    public virtual bool IsPrimitive { get; init; }

    public virtual bool IsStruct { get; init; }

    public virtual bool IsTask { get; init; }

    public virtual bool IsTimeSpan { get; init; }

    public virtual bool IsValueTuple { get; init; }

    public virtual IMethodCollection Methods { get; init; } = new MethodCollection();

    public virtual string Namespace { get; init; } = string.Empty;

    public virtual IClassCollection NestedClasses { get; init; } = new ClassCollection();

    public virtual IEnumCollection NestedEnums { get; init; } = new EnumCollection();

    public virtual IInterfaceCollection NestedInterfaces { get; init; } = new InterfaceCollection();

    public virtual IRecordCollection NestedRecords { get; init; } = new RecordCollection();

    public virtual IStructCollection NestedStructs { get; init; } = new StructCollection();

    public virtual string OriginalName { get; init; } = string.Empty;

    public virtual IPropertyCollection Properties { get; init; } = new PropertyCollection();

    public virtual IStaticReadOnlyFieldCollection StaticReadOnlyFields { get; init; } = new StaticReadOnlyFieldCollection();

    public virtual ITypeCollection TypeArguments { get; init; } = new TypeCollection();

    public virtual ITypeParameterCollection TypeParameters { get; init; } = new TypeParameterCollection();

    public virtual IFieldCollection TupleElements { get; init; } = new FieldCollection();

    public virtual string DefaultValue { get; init; } = "null";

    public virtual Settings? Settings { get; init; }

    public static implicit operator string(Type? type) => NormalizeName(name: type?.ToString());

    private static string NormalizeName(string? name)
    {
        return (name ?? string.Empty)
            .Replace(oldValue: " | null", newValue: string.Empty, comparisonType: StringComparison.Ordinal)
            .Replace(oldValue: "(", newValue: string.Empty, comparisonType: StringComparison.Ordinal)
            .Replace(oldValue: ")", newValue: string.Empty, comparisonType: StringComparison.Ordinal)
            .TrimEnd('[', ']');
    }
}
