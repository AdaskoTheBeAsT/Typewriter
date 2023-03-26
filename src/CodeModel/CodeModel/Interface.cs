﻿using Typewriter.CodeModel.Attributes;

namespace Typewriter.CodeModel
{
    /// <summary>
    /// Represents an interface.
    /// </summary>
    [Context(nameof(Interface), nameof(Interfaces))]
    public abstract class Interface : Item
    {
        /// <summary>
        /// All attributes defined on the interface.
        /// </summary>
        public abstract IAttributeCollection Attributes { get; }

        /// <summary>
        /// The containing class of the interface if it is nested.
        /// </summary>
        public abstract Class ContainingClass { get; }

        /// <summary>
        /// The XML documentation comment of the interface.
        /// </summary>
        public abstract DocComment DocComment { get; }

        /// <summary>
        /// All events defined in the interface.
        /// </summary>
        public abstract IEventCollection Events { get; }

        /// <summary>
        /// The full original name of the interface including namespace and containing class names.
        /// </summary>
        public abstract string FullName { get; }

        /// <summary>
        /// All interfaces implemented by the interface.
        /// </summary>
        public abstract IInterfaceCollection Interfaces { get; }

        /// <summary>
        /// Determines if the interface is generic.
        /// </summary>
        public abstract bool IsGeneric { get; }

        /// <summary>
        /// All methods defined in the interface.
        /// </summary>
        public abstract IMethodCollection Methods { get; }

        /// <summary>
        /// The name of the interface (camelCased).
        /// </summary>
#pragma warning disable SA1300 // Element should begin with upper-case letter
#pragma warning disable IDE1006 // Naming Styles

        // ReSharper disable once InconsistentNaming
        public abstract string name { get; }
#pragma warning restore IDE1006 // Naming Styles
#pragma warning restore SA1300 // Element should begin with upper-case letter

        /// <summary>
        /// The name of the interface.
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// The namespace of the interface.
        /// </summary>
        public abstract string Namespace { get; }

        /// <summary>
        /// The parent context of the interface.
        /// </summary>
        public abstract Item Parent { get; }

        /// <summary>
        /// All properties defined in the interface.
        /// </summary>
        public abstract IPropertyCollection Properties { get; }

        /// <summary>
        /// All generic type arguments of the interface.
        /// TypeArguments are the specified arguments for the TypeParameters on a generic interface e.g. &lt;string&gt;.
        /// (In Visual Studio 2013 TypeParameters and TypeArguments are the same).
        /// </summary>
        public abstract ITypeCollection TypeArguments { get; }

        /// <summary>
        /// All generic type parameters of the interface.
        /// TypeParameters are the type placeholders of a generic interface e.g. &lt;T&gt;.
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
        public static implicit operator string(Interface instance)
        {
            return instance.ToString();
        }

        /// <summary>
        /// Converts the current instance to a Type.
        /// </summary>
        public static implicit operator Type(Interface instance)
        {
            return instance?.Type;
        }
    }
}