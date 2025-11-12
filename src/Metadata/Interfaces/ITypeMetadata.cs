using System.Collections.Generic;

namespace Typewriter.Metadata.Interfaces
{
    public interface ITypeMetadata : IClassMetadata
    {
        bool IsDictionary { get; }

        bool IsDynamic { get; }

        bool IsEnum { get; }

        bool IsEnumerable { get; }

        bool IsNullable { get; }

        bool IsTask { get; }

        bool IsDefined { get; }

        bool IsValueTuple { get; }

        ITypeMetadata ElementType { get; }

        IEnumerable<IFieldMetadata> TupleElements { get; }

        IEnumerable<string> FileLocations { get; }

        string DefaultValue { get; }
    }
}