﻿using Typewriter.CodeModel.Attributes;

namespace Typewriter.CodeModel
{
    /// <summary>
    /// Represents a file.
    /// </summary>
    [Context(nameof(File), "Files")]
    public abstract class File : Item
    {
        /// <summary>
        /// All public classes defined in the file.
        /// </summary>
        public abstract IClassCollection Classes { get; }

        /// <summary>
        /// All public records defined in the file.
        /// </summary>
        public abstract IRecordCollection Records { get; }

        /// <summary>
        /// All public delegates defined in the file.
        /// </summary>
        public abstract IDelegateCollection Delegates { get; }

        /// <summary>
        /// All public enums defined in the file.
        /// </summary>
        public abstract IEnumCollection Enums { get; }

        /// <summary>
        /// All public interfaces defined in the file.
        /// </summary>
        public abstract IInterfaceCollection Interfaces { get; }

        /// <summary>
        /// The full path of the file.
        /// </summary>
        public abstract string FullName { get; }

        /// <summary>
        /// The name of the file.
        /// </summary>
        public abstract string Name { get; }
    }
}