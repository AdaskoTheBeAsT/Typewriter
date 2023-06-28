using System;
using System.Collections.Generic;
using System.Linq;
using Typewriter.CodeModel.Collections;
using Typewriter.Configuration;
using Typewriter.Metadata.Interfaces;
using static Typewriter.CodeModel.Helpers;

namespace Typewriter.CodeModel.Implementation
{
    public sealed class TypeImpl : Type
    {
        private readonly ITypeMetadata _metadata;
        private readonly Lazy<string> _lazyName;
        private readonly Lazy<string> _lazyOriginalName;

        private TypeImpl(ITypeMetadata metadata, Item parent, Settings settings)
        {
            _metadata = metadata;
            Parent = parent;
            _lazyName = new Lazy<string>(() => GetTypeScriptName(metadata));
            _lazyOriginalName = new Lazy<string>(() => GetOriginalName(metadata));
            Settings = settings;
        }

        public override Item Parent { get; }

        public override string name => CamelCase(_lazyName.Value.TrimStart('@'));

        public override string Name => _lazyName.Value.TrimStart('@');

        public override string OriginalName => _lazyOriginalName.Value;

        public override string FullName => _metadata.FullName;

        public override string Namespace => _metadata.Namespace;

        public override bool IsGeneric => _metadata.IsGeneric;

        public override bool IsEnum => _metadata.IsEnum;

        public override bool IsEnumerable => _metadata.IsEnumerable;

        public override bool IsNullable => _metadata.IsNullable;

        public override bool IsTask => _metadata.IsTask;

        public override bool IsPrimitive => IsPrimitive(_metadata);

        public override bool IsDate => string.Equals(Name, "Date", StringComparison.OrdinalIgnoreCase) || string.Equals(Name, "Date | null", StringComparison.OrdinalIgnoreCase);

        public override bool IsDefined => _metadata.IsDefined;

        public override bool IsGuid => string.Equals(FullName, "System.Guid", StringComparison.OrdinalIgnoreCase) || string.Equals(FullName, "System.Guid?", StringComparison.OrdinalIgnoreCase);

        public override bool IsTimeSpan => string.Equals(FullName, "System.TimeSpan", StringComparison.OrdinalIgnoreCase) || string.Equals(FullName, "System.TimeSpan?", StringComparison.OrdinalIgnoreCase);

        public override bool IsValueTuple => _metadata.IsValueTuple;

        public override string DefaultValue => _metadata.DefaultValue;

        private IAttributeCollection _attributes;

        public override IAttributeCollection Attributes => _attributes ?? (_attributes = AttributeImpl.FromMetadata(_metadata.Attributes, this, Settings));

        private DocComment _docComment;

        public override DocComment DocComment => _docComment ?? (_docComment = DocCommentImpl.FromXml(_metadata.DocComment, this));

        private IConstantCollection _constants;

        public override IConstantCollection Constants => _constants ?? (_constants = ConstantImpl.FromMetadata(_metadata.Constants, this, Settings));

        private IDelegateCollection _delegates;

        public override IDelegateCollection Delegates => _delegates ?? (_delegates = DelegateImpl.FromMetadata(_metadata.Delegates, this, Settings));

        private IFieldCollection _fields;

        public override IFieldCollection Fields => _fields ?? (_fields = FieldImpl.FromMetadata(_metadata.Fields, this, Settings));

        private Class _baseClass;

        public override Class BaseClass => _baseClass ?? (_baseClass = ClassImpl.FromMetadata(_metadata.BaseClass, this, Settings));

        private Class _containingClass;

        public override Class ContainingClass => _containingClass ?? (_containingClass = ClassImpl.FromMetadata(_metadata.ContainingClass, this, Settings));

        private IInterfaceCollection _interfaces;

        public override IInterfaceCollection Interfaces => _interfaces ?? (_interfaces = InterfaceImpl.FromMetadata(_metadata.Interfaces, this, Settings));

        private IMethodCollection _methods;

        public override IMethodCollection Methods => _methods ?? (_methods = MethodImpl.FromMetadata(_metadata.Methods, this, Settings));

        private IPropertyCollection _properties;

        public override IPropertyCollection Properties => _properties ?? (_properties = PropertyImpl.FromMetadata(_metadata.Properties, this, Settings));

        private ITypeCollection _typeArguments;

        public override ITypeCollection TypeArguments => _typeArguments ?? (_typeArguments = FromMetadata(_metadata.TypeArguments, this, Settings));

        private ITypeParameterCollection _typeParameters;

        public override ITypeParameterCollection TypeParameters => _typeParameters ?? (_typeParameters = TypeParameterImpl.FromMetadata(_metadata.TypeParameters, this));

        private IFieldCollection _tupleElements;

        public override IFieldCollection TupleElements => _tupleElements ?? (_tupleElements = FieldImpl.FromMetadata(_metadata.TupleElements, this, Settings));

        private IClassCollection _nestedClasses;

        public override IClassCollection NestedClasses => _nestedClasses ?? (_nestedClasses = ClassImpl.FromMetadata(_metadata.NestedClasses, this, Settings));

        private IEnumCollection _nestedEnums;

        public override IEnumCollection NestedEnums => _nestedEnums ?? (_nestedEnums = EnumImpl.FromMetadata(_metadata.NestedEnums, this, Settings));

        private IInterfaceCollection _nestedInterfaces;

        public override IInterfaceCollection NestedInterfaces => _nestedInterfaces ?? (_nestedInterfaces = InterfaceImpl.FromMetadata(_metadata.NestedInterfaces, this, Settings));

        public override Settings Settings { get; }

        public override string ToString()
        {
            return Name;
        }

        public static ITypeCollection FromMetadata(IEnumerable<ITypeMetadata> metadata, Item parent, Settings settings)
        {
            return new TypeCollectionImpl(metadata.Select(t => new TypeImpl(t, parent, settings)));
        }

        public static Type FromMetadata(ITypeMetadata metadata, Item parent, Settings settings)
        {
            return metadata == null ? null : new TypeImpl(metadata, parent, settings);
        }
    }
}