using System;
using System.Collections.Generic;
using Typewriter.Metadata.Interfaces;

namespace Typewriter.Metadata.Roslyn
{
    public class RoslynVoidTaskMetadata
        : ITypeMetadata
    {
        public string DocComment => null;

        public string Name => "Void";

        public string FullName => "System.Void";

        public string AssemblyName => "System.Private.CoreLib";

        public bool IsAbstract => false;

        public bool IsDictionary => false;

        public bool IsDynamic => false;

        public bool IsEnum => false;

        public bool IsEnumerable => false;

        public bool IsGeneric => false;

        public bool IsStatic => false;

        public bool IsNullable => false;

        public bool IsTask => true;

        public bool IsDefined => false;

        public bool IsValueTuple => false;

        public string Namespace => "System";

        public ITypeMetadata Type => null;

        public ITypeMetadata ElementType => null;

        public string DefaultValue => "void(0)";

        public IEnumerable<IAttributeMetadata> Attributes => Array.Empty<IAttributeMetadata>();

        public IClassMetadata BaseClass => null;

        public IClassMetadata ContainingClass => null;

        public IEnumerable<IConstantMetadata> Constants => Array.Empty<IConstantMetadata>();

        public IEnumerable<IDelegateMetadata> Delegates => Array.Empty<IDelegateMetadata>();

        public IEnumerable<IEventMetadata> Events => Array.Empty<IEventMetadata>();

        public IEnumerable<IFieldMetadata> Fields => Array.Empty<IFieldMetadata>();

        public IEnumerable<IInterfaceMetadata> Interfaces => Array.Empty<IInterfaceMetadata>();

        public IEnumerable<IMethodMetadata> Methods => Array.Empty<IMethodMetadata>();

        public IEnumerable<IPropertyMetadata> Properties => Array.Empty<IPropertyMetadata>();

        public IEnumerable<IClassMetadata> NestedClasses => Array.Empty<IClassMetadata>();

        public IEnumerable<IEnumMetadata> NestedEnums => Array.Empty<IEnumMetadata>();

        public IEnumerable<IInterfaceMetadata> NestedInterfaces => Array.Empty<IInterfaceMetadata>();

        public IEnumerable<IStaticReadOnlyFieldMetadata> StaticReadOnlyFields => Array.Empty<IStaticReadOnlyFieldMetadata>();

        public IEnumerable<ITypeMetadata> TypeArguments => Array.Empty<ITypeMetadata>();

        public IEnumerable<ITypeParameterMetadata> TypeParameters => Array.Empty<ITypeParameterMetadata>();

        public IEnumerable<IFieldMetadata> TupleElements => Array.Empty<IFieldMetadata>();

        public IEnumerable<string> FileLocations => Type.FileLocations;
    }
}