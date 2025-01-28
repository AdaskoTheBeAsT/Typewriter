using System.Collections.Generic;
using Typewriter.CodeModel.Attributes;
using Typewriter.Configuration;

namespace Typewriter.CodeModel
{
    /// <summary>
    /// Represents a type.
    /// </summary>
    [Context(nameof(Type), "Types")]
    public abstract class Type : Item
    {
        /// <summary>
        /// The name of the assembly containing the attribute.
        /// </summary>
        public abstract string AssemblyName { get; }

        /// <summary>
        /// The file paths this type is defined in.  There may be more than one if the type is a partial class.
        /// </summary>
        public abstract IEnumerable<string> FileLocations { get; }

        /// <summary>
        /// All attributes defined on the type.
        /// </summary>
        public abstract IAttributeCollection Attributes { get; }

        /// <summary>
        /// The base class of the type.
        /// </summary>
        public abstract Class BaseClass { get; }

        /// <summary>
        /// All constants defined in the type.
        /// </summary>
        public abstract IConstantCollection Constants { get; }

        /// <summary>
        /// The containing class of the type if it's nested.
        /// </summary>
        public abstract Class ContainingClass { get; }

        /// <summary>
        /// All delegates defined in the type.
        /// </summary>
        public abstract IDelegateCollection Delegates { get; }

        /// <summary>
        /// The XML documentation comment of the type.
        /// </summary>
        public abstract DocComment DocComment { get; }

        /// <summary>
        /// All fields defined in the type.
        /// </summary>
        public abstract IFieldCollection Fields { get; }

        /// <summary>
        /// The full original name of the type including namespace and containing class names.
        /// </summary>
        public abstract string FullName { get; }

        /// <summary>
        /// All interfaces implemented by the type.
        /// (In Visual Studio 2013 Interfaces are not available on Types).
        /// </summary>
        public abstract IInterfaceCollection Interfaces { get; }

        /// <summary>
        /// Determines if the type is a DateTime.
        /// </summary>
        public abstract bool IsDate { get; }

        /// <summary>
        /// Determines if the type is defined in the current solution.
        /// (In Visual Studio 2013 IsDefined returns false for generic types).
        /// </summary>
        public abstract bool IsDefined { get; }

        /// <summary>
        /// Determines if the type is an <see cref="Dictionary{TKey,TValue}"/> or <see cref="IDictionary{TKey,TValue}"/>.
        /// </summary>
        public abstract bool IsDictionary { get; }

        /// <summary>
        /// Determines if the type is an dynamic.
        /// </summary>
        public abstract bool IsDynamic { get; }

        /// <summary>
        /// Determines if the type is an enum.
        /// </summary>
        public abstract bool IsEnum { get; }

        /// <summary>
        /// Determines if the type is enumerable.
        /// </summary>
        public abstract bool IsEnumerable { get; }

        /// <summary>
        /// Determines if the type is generic.
        /// </summary>
        public abstract bool IsGeneric { get; }

        /// <summary>
        /// Determines if the type is a Guid.
        /// </summary>
        public abstract bool IsGuid { get; }

        /// <summary>
        /// Determines if the type is nullable.
        /// </summary>
        public abstract bool IsNullable { get; }

        /// <summary>
        /// Determines if the type is primitive.
        /// </summary>
        public abstract bool IsPrimitive { get; }

        /// <summary>
        /// Determines if the type is a Task.
        /// </summary>
        public abstract bool IsTask { get; }

        /// <summary>
        /// Determines if the type is a TimeSpan.
        /// </summary>
        public abstract bool IsTimeSpan { get; }

        /// <summary>
        /// Determines if the type is a ValueTuple.
        /// </summary>
        public abstract bool IsValueTuple { get; }

        /// <summary>
        /// All methods defined in the type.
        /// </summary>
        public abstract IMethodCollection Methods { get; }

        /// <summary>
        /// The name of the type (camelCased).
        /// </summary>
#pragma warning disable SA1300 // Element should begin with upper-case letter
#pragma warning disable IDE1006 // Naming Styles

        // ReSharper disable once InconsistentNaming
        public abstract string name { get; }
#pragma warning restore IDE1006 // Naming Styles
#pragma warning restore SA1300 // Element should begin with upper-case letter

        /// <summary>
        /// The name of the type.
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// The namespace of the type.
        /// </summary>
        public abstract string Namespace { get; }

        /// <summary>
        /// All classes defined in the type.
        /// </summary>
        public abstract IClassCollection NestedClasses { get; }

        /// <summary>
        /// All enums defined in the type.
        /// </summary>
        public abstract IEnumCollection NestedEnums { get; }

        /// <summary>
        /// All interfaces defined in the type.
        /// </summary>
        public abstract IInterfaceCollection NestedInterfaces { get; }

        /// <summary>
        /// The original C# name of the type.
        /// </summary>
        public abstract string OriginalName { get; }

        /// <summary>
        /// The parent context of the type.
        /// </summary>
        public abstract Item Parent { get; }

        /// <summary>
        /// All properties defined in the type.
        /// </summary>
        public abstract IPropertyCollection Properties { get; }

        /// <summary>
        /// All static readonly fields defined in the type.
        /// </summary>
        public abstract IStaticReadOnlyFieldCollection StaticReadOnlyFields { get; }

        /// <summary>
        /// All generic type parameters of the type.
        /// TypeArguments are the specified arguments for the TypeParameters on a generic type e.g. &lt;string&gt;.
        /// (In Visual Studio 2013 TypeParameters and TypeArguments are the same).
        /// </summary>
        public abstract ITypeCollection TypeArguments { get; }

        /// <summary>
        /// All generic type parameters of the type.
        /// TypeParameters are the type placeholders of a generic type e.g. &lt;T&gt;.
        /// (In Visual Studio 2013 TypeParameters and TypeArguments are the same).
        /// </summary>
        public abstract ITypeParameterCollection TypeParameters { get; }

        /// <summary>
        /// The named ValueTuple fields of the type.
        /// </summary>
        public abstract IFieldCollection TupleElements { get; }

        /// <summary>
        /// Default value for the type.
        /// </summary>
        public abstract string DefaultValue { get; }

        /// <summary>
        /// Settings for the type.
        /// </summary>
        public abstract Settings Settings { get; }

        /// <summary>
        /// If this type is an array (the primitive kind), the type of the elements, else null.
        /// </summary>
        public abstract Type ElementType { get; }

        /// <summary>
        /// Converts the current instance to string.
        /// </summary>
        public static implicit operator string(Type instance)
        {
            return instance.ToString();
        }
    }
}