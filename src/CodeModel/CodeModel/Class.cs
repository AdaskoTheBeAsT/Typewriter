﻿using Typewriter.CodeModel.Attributes;

namespace Typewriter.CodeModel
{
    /// <summary>
    /// Represents a class.
    /// </summary>
    [Context(nameof(Class), "Classes")]
    public abstract class Class : Item
    {
        /// <summary>
        /// All attributes defined on the class.
        /// </summary>
        public abstract IAttributeCollection Attributes { get; }

        /// <summary>
        /// The base class of the class.
        /// </summary>
        public abstract Class BaseClass { get; }

        /// <summary>
        /// All constants defined in the class.
        /// </summary>
        public abstract IConstantCollection Constants { get; }

        /// <summary>
        /// The containing class of the class if it's nested.
        /// </summary>
        public abstract Class ContainingClass { get; }

        /// <summary>
        /// All delegates defined in the class.
        /// </summary>
        public abstract IDelegateCollection Delegates { get; }

        /// <summary>
        /// The XML documentation comment of the class.
        /// </summary>
        public abstract DocComment DocComment { get; }

        /// <summary>
        /// All events defined in the class.
        /// </summary>
        public abstract IEventCollection Events { get; }

        /// <summary>
        /// All fields defined in the class.
        /// </summary>
        public abstract IFieldCollection Fields { get; }

        /// <summary>
        /// The full original name of the class including namespace and containing class names.
        /// </summary>
        public abstract string FullName { get; }

        /// <summary>
        /// The name of the assembly containing the attribute.
        /// </summary>
        public abstract string AssemblyName { get; }

        /// <summary>
        /// All interfaces implemented by the class.
        /// </summary>
        public abstract IInterfaceCollection Interfaces { get; }

        /// <summary>
        /// Determines if the class is abstract.
        /// </summary>
        public abstract bool IsAbstract { get; }

        /// <summary>
        /// Determines if the class is generic.
        /// </summary>
        public abstract bool IsGeneric { get; }

        /// <summary>
        /// All methods defined in the class.
        /// </summary>
        public abstract IMethodCollection Methods { get; }

        /// <summary>
        /// The name of the class (camelCased).
        /// </summary>
#pragma warning disable SA1300 // Element should begin with upper-case letter
#pragma warning disable IDE1006 // Naming Styles

        // ReSharper disable once InconsistentNaming
        public abstract string name { get; }
#pragma warning restore IDE1006 // Naming Styles
#pragma warning restore SA1300 // Element should begin with upper-case letter

        /// <summary>
        /// The name of the class.
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// The namespace of the class.
        /// </summary>
        public abstract string Namespace { get; }

        /// <summary>
        /// All classes defined in the class.
        /// </summary>
        public abstract IClassCollection NestedClasses { get; }

        /// <summary>
        /// All enums defined in the class.
        /// </summary>
        public abstract IEnumCollection NestedEnums { get; }

        /// <summary>
        /// All interfaces defined in the class.
        /// </summary>
        public abstract IInterfaceCollection NestedInterfaces { get; }

        /// <summary>
        /// The parent context of the class.
        /// </summary>
        public abstract Item Parent { get; }

        /// <summary>
        /// All properties defined in the class.
        /// </summary>
        public abstract IPropertyCollection Properties { get; }

        /// <summary>
        /// All generic type arguments of the class.
        /// TypeArguments are the specified arguments for the TypeParameters on a generic class e.g. &lt;string&gt;.
        /// (In Visual Studio 2013 TypeParameters and TypeArguments are the same).
        /// </summary>
        public abstract ITypeCollection TypeArguments { get; }

        /// <summary>
        /// All generic type parameters of the class.
        /// TypeParameters are the type placeholders of a generic class e.g. &lt;T&gt;.
        /// (In Visual Studio 2013 TypeParameters and TypeArguments are the same).
        /// </summary>
        public abstract ITypeParameterCollection TypeParameters { get; }

        /// <summary>
        /// Represents a <see cref="Typewriter.CodeModel.Type"/>.
        /// </summary>
        protected abstract Type Type { get; }

        /// <summary>
        /// Converts the current instance to string.
        /// </summary>
        public static implicit operator string(Class instance)
        {
            return instance.ToString();
        }

        /// <summary>
        /// Converts the current instance to a Type.
        /// </summary>
        public static implicit operator Type(Class instance)
        {
            return instance?.Type;
        }
    }
}