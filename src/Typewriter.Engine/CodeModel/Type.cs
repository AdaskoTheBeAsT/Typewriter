using Typewriter.Configuration;

namespace Typewriter.CodeModel;

/// <summary>
/// A type reference in the code model, for example the type of a property, parameter,
/// or method return value. Rendering it (template shorthand <c>$Type</c>) produces the
/// mapped TypeScript type, for example <c>number</c> for C# <c>int</c>.
/// </summary>
public class Type : Item
{
    /// <summary>
    /// Gets the attributes applied to the type declaration.
    /// </summary>
    public virtual IAttributeCollection Attributes { get; init; } = new AttributeCollection();

    /// <summary>
    /// Gets the direct base class of the type, or <c>null</c> when the type inherits
    /// only from <c>object</c>. Useful for walking inheritance chains, for example
    /// <c>$BaseClass[ extends $Name]</c>.
    /// </summary>
    public virtual Class? BaseClass { get; init; }

    /// <summary>
    /// Gets the <c>const</c> members declared on the type.
    /// </summary>
    public virtual IConstantCollection Constants { get; init; } = new ConstantCollection();

    /// <summary>
    /// Gets the class this type is nested inside, or <c>null</c> for top-level types.
    /// </summary>
    public virtual Class? ContainingClass { get; init; }

    /// <summary>
    /// Gets the delegates declared inside the type.
    /// </summary>
    public virtual IDelegateCollection Delegates { get; init; } = new DelegateCollection();

    /// <summary>
    /// Gets the XML documentation comment of the type, or <c>null</c> when the type
    /// has no doc comment. Use <c>$DocComment.Summary</c> in templates.
    /// </summary>
    public virtual DocComment? DocComment { get; init; }

    /// <summary>
    /// Gets the element type of arrays and enumerable types, for example <c>string</c>
    /// for <c>string[]</c> or <c>List&lt;string&gt;</c>; <c>null</c> for non-enumerable types.
    /// </summary>
    public virtual Type? ElementType { get; init; }

    /// <summary>
    /// Gets the instance fields declared on the type.
    /// </summary>
    public virtual IFieldCollection Fields { get; init; } = new FieldCollection();

    /// <summary>
    /// Gets the source file paths where the type is declared (multiple for partial types).
    /// </summary>
    public virtual IEnumerable<string> FileLocations { get; init; } = [];

    /// <summary>
    /// Gets the interfaces implemented by the type.
    /// </summary>
    public virtual IInterfaceCollection Interfaces { get; init; } = new InterfaceCollection();

    /// <summary>
    /// Gets a value indicating whether the type represents date or time data,
    /// for example <c>DateTime</c>, <c>DateTimeOffset</c>, <c>DateOnly</c>, or NodaTime types.
    /// </summary>
    public virtual bool IsDate { get; init; }

    /// <summary>
    /// Gets a value indicating whether the type is defined in the loaded workspace source
    /// (as opposed to a referenced assembly or framework type).
    /// </summary>
    public virtual bool IsDefined { get; init; }

    /// <summary>
    /// Gets a value indicating whether the type is a dictionary,
    /// for example <c>Dictionary&lt;string, int&gt;</c>.
    /// </summary>
    public virtual bool IsDictionary { get; init; }

    /// <summary>
    /// Gets a value indicating whether the type is <c>dynamic</c>.
    /// </summary>
    public virtual bool IsDynamic { get; init; }

    /// <summary>
    /// Gets a value indicating whether the type is an enum.
    /// </summary>
    public virtual bool IsEnum { get; init; }

    /// <summary>
    /// Gets a value indicating whether the type is enumerable (arrays, <c>List&lt;T&gt;</c>,
    /// <c>IEnumerable&lt;T&gt;</c>, ...). <c>string</c> is not treated as enumerable.
    /// </summary>
    public virtual bool IsEnumerable { get; init; }

    /// <summary>
    /// Gets a value indicating whether the type is generic.
    /// </summary>
    public virtual bool IsGeneric { get; init; }

    /// <summary>
    /// Gets a value indicating whether the type is <c>System.Guid</c>.
    /// </summary>
    public virtual bool IsGuid { get; init; }

    /// <summary>
    /// Gets a value indicating whether the type is nullable (<c>T?</c> or
    /// <c>Nullable&lt;T&gt;</c>). Template shorthand: <c>$IsNullable[...][...]</c>.
    /// </summary>
    public virtual bool IsNullable { get; init; }

    /// <summary>
    /// Gets a value indicating whether the type maps to a primitive TypeScript type
    /// (<c>string</c>, <c>number</c>, <c>boolean</c>, ...).
    /// </summary>
    public virtual bool IsPrimitive { get; init; }

    /// <summary>
    /// Gets a value indicating whether the type is a struct (value type).
    /// </summary>
    public virtual bool IsStruct { get; init; }

    /// <summary>
    /// Gets a value indicating whether the type is <c>Task</c> or <c>Task&lt;T&gt;</c>.
    /// Await-unwrapped rendering uses the inner type.
    /// </summary>
    public virtual bool IsTask { get; init; }

    /// <summary>
    /// Gets a value indicating whether the type is <c>System.TimeSpan</c>.
    /// </summary>
    public virtual bool IsTimeSpan { get; init; }

    /// <summary>
    /// Gets a value indicating whether the type is a value tuple, for example <c>(int, string)</c>.
    /// </summary>
    public virtual bool IsValueTuple { get; init; }

    /// <summary>
    /// Gets the methods declared on the type.
    /// </summary>
    public virtual IMethodCollection Methods { get; init; } = new MethodCollection();

    /// <summary>
    /// Gets the namespace that contains the type.
    /// </summary>
    public virtual string Namespace { get; init; } = string.Empty;

    /// <summary>
    /// Gets the classes nested inside the type.
    /// </summary>
    public virtual IClassCollection NestedClasses { get; init; } = new ClassCollection();

    /// <summary>
    /// Gets the enums nested inside the type.
    /// </summary>
    public virtual IEnumCollection NestedEnums { get; init; } = new EnumCollection();

    /// <summary>
    /// Gets the interfaces nested inside the type.
    /// </summary>
    public virtual IInterfaceCollection NestedInterfaces { get; init; } = new InterfaceCollection();

    /// <summary>
    /// Gets the records nested inside the type.
    /// </summary>
    public virtual IRecordCollection NestedRecords { get; init; } = new RecordCollection();

    /// <summary>
    /// Gets the structs nested inside the type.
    /// </summary>
    public virtual IStructCollection NestedStructs { get; init; } = new StructCollection();

    /// <summary>
    /// Gets the original C# name of the type before TypeScript mapping,
    /// for example <c>Int32</c> when <see cref="Item.Name"/> renders <c>number</c>.
    /// Template shorthand: <c>$OriginalName</c>.
    /// </summary>
    public virtual string OriginalName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the properties declared on the type.
    /// </summary>
    public virtual IPropertyCollection Properties { get; init; } = new PropertyCollection();

    /// <summary>
    /// Gets the <c>static readonly</c> fields declared on the type.
    /// </summary>
    public virtual IStaticReadOnlyFieldCollection StaticReadOnlyFields { get; init; } = new StaticReadOnlyFieldCollection();

    /// <summary>
    /// Gets the generic type arguments of the type, for example <c>string</c> and <c>int</c>
    /// in <c>Dictionary&lt;string, int&gt;</c>. Template shorthand: <c>$TypeArguments[...]</c>.
    /// </summary>
    public virtual ITypeCollection TypeArguments { get; init; } = new TypeCollection();

    /// <summary>
    /// Gets the generic type parameters declared by the type.
    /// </summary>
    public virtual ITypeParameterCollection TypeParameters { get; init; } = new TypeParameterCollection();

    /// <summary>
    /// Gets the elements of a value tuple type, for example <c>Item1</c> and <c>Item2</c>.
    /// </summary>
    public virtual IFieldCollection TupleElements { get; init; } = new FieldCollection();

    /// <summary>
    /// Gets the default TypeScript value for the type, for example <c>0</c> for <c>number</c>
    /// and <c>null</c> for nullable types. Template shorthand: <c>$Type[$Default]</c>.
    /// </summary>
    public virtual string DefaultValue { get; init; } = "null";

    /// <summary>
    /// Gets the template settings in effect for the current render, or <c>null</c> outside rendering.
    /// </summary>
    public virtual Settings? Settings { get; init; }

    /// <summary>
    /// Converts the type to its rendered TypeScript name, with strict-null suffixes,
    /// parentheses, and array brackets removed.
    /// </summary>
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
