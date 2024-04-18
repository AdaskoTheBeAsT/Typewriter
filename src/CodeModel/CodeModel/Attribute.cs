using Typewriter.CodeModel.Attributes;

namespace Typewriter.CodeModel
{
    /// <summary>
    /// Represents an attribute.
    /// </summary>
    [Context(nameof(Attribute), nameof(Attributes))]
    public abstract class Attribute : Item
    {
        /// <summary>
        /// The full original name of the attribute including namespace and containing class names.
        /// </summary>
        public abstract string FullName { get; }

        /// <summary>
        /// The name of the attribute (camelCased).
        /// </summary>
#pragma warning disable SA1300 // Element should begin with upper-case letter
#pragma warning disable IDE1006 // Naming Styles

        // ReSharper disable once InconsistentNaming
        public abstract string name { get; }
#pragma warning restore IDE1006 // Naming Styles
#pragma warning restore SA1300 // Element should begin with upper-case letter

        /// <summary>
        /// The name of the attribute.
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// The name of the assembly containing the attribute.
        /// </summary>
        public abstract string AssemblyName { get; }

        /// <summary>
        /// The parent context of the attribute.
        /// </summary>
        public abstract Item Parent { get; }

        /// <summary>
        /// The value of the attribute as string.
        /// </summary>
        public abstract string Value { get; }

        /// <summary>
        /// The arguments of the attribute.
        /// </summary>
        public abstract IAttributeArgumentCollection Arguments { get; }

        /// <summary>
        /// The type of the attribute.
        /// </summary>
        public abstract Type Type { get; }

        /// <summary>
        /// Converts the current instance to string.
        /// </summary>
        public static implicit operator string(Attribute instance)
        {
            return instance.ToString();
        }
    }
}