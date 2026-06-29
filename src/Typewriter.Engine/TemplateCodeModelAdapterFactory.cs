using Typewriter.Abstractions;
using CodeAttribute = Typewriter.CodeModel.Attribute;
using CodeClass = Typewriter.CodeModel.Class;
using CodeConstant = Typewriter.CodeModel.Constant;
using CodeDelegate = Typewriter.CodeModel.Delegate;
using CodeEnum = Typewriter.CodeModel.Enum;
using CodeEvent = Typewriter.CodeModel.Event;
using CodeField = Typewriter.CodeModel.Field;
using CodeFile = Typewriter.CodeModel.File;
using CodeInterface = Typewriter.CodeModel.Interface;
using CodeMethod = Typewriter.CodeModel.Method;
using CodeParameter = Typewriter.CodeModel.Parameter;
using CodeRecord = Typewriter.CodeModel.Record;
using CodeStaticReadOnlyField = Typewriter.CodeModel.StaticReadOnlyField;
using CodeStruct = Typewriter.CodeModel.Struct;
using CodeType = Typewriter.CodeModel.Type;

namespace Typewriter.Engine;

internal sealed class TemplateCodeModelAdapterFactory
{
    private readonly Typewriter.Configuration.Settings _settings;
    private readonly string _assemblyName;
    private readonly IReadOnlyDictionary<string, MethodMetadata> _methodsByFullName;
    private readonly IReadOnlyDictionary<string, PropertyMetadata> _propertiesByFullName;
    private readonly IReadOnlyDictionary<string, TypeMetadata> _typesByFullName;
    private readonly TypeScriptTypeMapper _typeMapper = new();
    private CodeFile? _cachedFile;
    private ProjectMetadata? _cachedFileProject;

    public TemplateCodeModelAdapterFactory(
        ProjectMetadata metadata,
        string templatePath)
        : this(
            metadata: metadata,
            settings: new Typewriter.Configuration.Settings
            {
                TemplatePath = templatePath,
            })
    {
    }

    public TemplateCodeModelAdapterFactory(
        ProjectMetadata metadata,
        Typewriter.Configuration.Settings settings)
        : this(metadata: metadata, settings: settings, metadataIndex: null)
    {
    }

    public TemplateCodeModelAdapterFactory(
        ProjectMetadata metadata,
        Typewriter.Configuration.Settings settings,
        ProjectMetadataIndex? metadataIndex)
    {
        _settings = settings;
        _assemblyName = Path.GetFileNameWithoutExtension(path: metadata.ProjectPath);
        var index = metadataIndex ?? ProjectMetadataIndex.Create(metadata: metadata);
        _typesByFullName = index.TypesByFullName;
        _methodsByFullName = index.MethodsByFullName;
        _propertiesByFullName = index.PropertiesByFullName;
    }

    public bool TryAdapt(
        object? context,
        System.Type targetType,
        out object? adapted)
    {
        adapted = null;
        if (context is null)
        {
            return !targetType.IsValueType || Nullable.GetUnderlyingType(nullableType: targetType) is not null;
        }

        if (targetType.IsInstanceOfType(o: context))
        {
            adapted = context;
            return true;
        }

        adapted = context switch
        {
            ProjectMetadata project => targetType.IsAssignableFrom(c: typeof(CodeFile)) ? CreateFile(project: project) : null,
            TypeMetadata type => AdaptType(type: type, targetType: targetType),
            PropertyMetadata property => CreateProperty(property: property, parent: null),
            MethodMetadata method => CreateMethod(method: method, parent: null),
            ParameterMetadata parameter => CreateParameter(parameter: parameter, parent: null),
            ConstantMetadata constant => CreateConstant(constant: constant, parent: null),
            FieldMetadata field => CreateField(field: field, parent: null),
            StaticReadOnlyFieldMetadata staticReadOnlyField => CreateStaticReadOnlyField(field: staticReadOnlyField, parent: null),
            EventMetadata @event => CreateEvent(@event: @event, parent: null),
            DelegateMetadata @delegate => CreateDelegate(@delegate: @delegate, parent: null),
            TypeMetadataReference typeReference => CreateType(type: typeReference),
            EnumValueMetadata enumValue => CreateEnumValue(enumValue: enumValue, parent: null),
            AttributeMetadata attribute => CreateAttribute(attribute: attribute, parent: null),
            AttributeArgumentMetadata argument => CreateAttributeArgument(argument: argument),
            _ => null,
        };

        return adapted is not null && targetType.IsInstanceOfType(o: adapted);
    }

    public CodeFile CreateFile(ProjectMetadata project)
    {
        if (ReferenceEquals(objA: _cachedFileProject, objB: project) && _cachedFile is not null)
        {
            return _cachedFile;
        }

        _cachedFile = (CodeFile)AdaptProject(project: project, targetType: typeof(CodeFile))!;
        _cachedFileProject = project;
        return _cachedFile;
    }

    private static Typewriter.CodeModel.DocComment? CreateDocComment(
        DocCommentMetadata? docComment,
        Typewriter.CodeModel.Item? parent)
    {
        return docComment is null
            ? null
            : new Typewriter.CodeModel.DocComment
            {
                Name = docComment.Summary,
                Parent = parent,
                Summary = docComment.Summary,
                Returns = docComment.Returns,
                Parameters = new Typewriter.CodeModel.ParameterCommentCollection(
                    items: docComment.Parameters.Select(
                        selector: parameter => new Typewriter.CodeModel.ParameterComment
                        {
                            Name = parameter.Name,
                            Description = parameter.Description,
                            Parent = parent,
                        })),
            };
    }

    private static Typewriter.CodeModel.TypeParameterCollection CreateTypeParameters(
        IEnumerable<TypeParameterMetadata> typeParameters,
        Typewriter.CodeModel.Item? parent)
    {
        return new Typewriter.CodeModel.TypeParameterCollection(
            items: typeParameters.Select(
                selector: parameter => new Typewriter.CodeModel.TypeParameter
                {
                    Name = parameter.Name,
                    FullName = parameter.FullName,
                    Parent = parent,
                }));
    }

    private static bool IsTaskLike(string fullName)
    {
        return fullName.Equals(value: "System.Threading.Tasks.Task", comparisonType: StringComparison.Ordinal)
               || fullName.Equals(value: "System.Threading.Tasks.ValueTask", comparisonType: StringComparison.Ordinal);
    }

    private static bool HasAttribute(
        IEnumerable<AttributeMetadata> attributes,
        string name)
    {
        return attributes.Any(
            predicate: attribute => attribute.Name.Equals(value: name, comparisonType: StringComparison.Ordinal)
                                    || attribute.FullName.Equals(value: name, comparisonType: StringComparison.Ordinal)
                                    || attribute.Name.Equals(value: name + "Attribute", comparisonType: StringComparison.Ordinal)
                                    || attribute.FullName.EndsWith(value: "." + name + "Attribute", comparisonType: StringComparison.Ordinal));
    }

    private static string GetLegacyTypeName(string mappedName)
    {
        return mappedName
            .Replace(oldValue: " | null", newValue: string.Empty, comparisonType: StringComparison.Ordinal)
            .Replace(oldValue: "(", newValue: string.Empty, comparisonType: StringComparison.Ordinal)
            .Replace(oldValue: ")", newValue: string.Empty, comparisonType: StringComparison.Ordinal);
    }

    private static bool IsDynamic(TypeMetadataReference type)
    {
        return type.FullName.Equals(value: "dynamic", comparisonType: StringComparison.OrdinalIgnoreCase)
               || type.FullName.Equals(value: "System.Object", comparisonType: StringComparison.Ordinal);
    }

    private static string FormatGenericFullName(TypeMetadata type)
    {
        if (type.TypeParameters.Count == 0
            || type.FullName.Contains(value: '<', comparisonType: StringComparison.Ordinal))
        {
            return type.FullName;
        }

        return string.Concat(
            str0: type.FullName,
            str1: "<",
            str2: string.Join(separator: ", ", values: type.TypeParameters.Select(selector: parameter => parameter.Name)),
            str3: ">");
    }

    private static bool IsPrimitiveType(TypeMetadataReference type)
    {
        var effectiveType = type.IsCollection && type.ElementType is not null
            ? type.ElementType
            : type;
        return effectiveType.IsPrimitive
            || effectiveType.IsDateLike
            || effectiveType.FullName.Equals(value: "System.Guid", comparisonType: StringComparison.Ordinal);
    }

    private static string GetDefaultValue(TypeMetadata type)
    {
        return type.Kind == TypeMetadataKind.Enum
            ? ResolveEnumDefault(enumName: type.Name, enumValues: type.EnumValues) ?? "0"
            : "null";
    }

    private static string? ResolveEnumDefault(
        string enumName,
        IReadOnlyList<EnumValueMetadata> enumValues)
    {
        return enumValues.Count == 0
            ? null
            : $"{enumName}.{enumValues[index: 0].Name}";
    }

    private object? AdaptProject(
        ProjectMetadata project,
        System.Type targetType)
    {
        if (!targetType.IsAssignableFrom(c: typeof(CodeFile)))
        {
            return null;
        }

        var filePath = project.SourceFiles.Count == 1
            ? project.SourceFiles[index: 0].Path
            : project.ProjectPath;

        var rootTypes = project.Types
            .Where(predicate: static type => string.IsNullOrWhiteSpace(value: type.ContainingTypeFullName))
            .ToArray();

        return new CodeFile
        {
            Name = Path.GetFileNameWithoutExtension(path: filePath),
            FullName = filePath,
            Path = filePath,
            Classes = new Typewriter.CodeModel.ClassCollection(
                items: rootTypes
                    .Where(predicate: type => type.Kind == TypeMetadataKind.Class)
                    .Select(selector: CreateClass)),
            Records = new Typewriter.CodeModel.RecordCollection(
                items: rootTypes
                    .Where(predicate: type => type.Kind == TypeMetadataKind.Record)
                    .Select(selector: CreateRecord)),
            Structs = new Typewriter.CodeModel.StructCollection(
                items: rootTypes
                    .Where(predicate: type => type.Kind == TypeMetadataKind.Struct)
                    .Select(selector: CreateStruct)),
            Delegates = new Typewriter.CodeModel.DelegateCollection(items: project.Delegates.Select(selector: @delegate => CreateDelegate(@delegate: @delegate, parent: null))),
            Interfaces = new Typewriter.CodeModel.InterfaceCollection(
                items: rootTypes
                    .Where(predicate: type => type.Kind == TypeMetadataKind.Interface)
                    .Select(selector: CreateInterface)),
            Enums = new Typewriter.CodeModel.EnumCollection(
                items: rootTypes
                    .Where(predicate: type => type.Kind == TypeMetadataKind.Enum)
                    .Select(selector: CreateEnum)),
            Types = new Typewriter.CodeModel.TypeCollection(items: rootTypes.Select(selector: CreateType)),
        };
    }

    private object? AdaptType(
        TypeMetadata type,
        System.Type targetType)
    {
        if (type.Kind == TypeMetadataKind.Class && targetType.IsAssignableFrom(c: typeof(CodeClass)))
        {
            return CreateClass(type: type);
        }

        if (type.Kind == TypeMetadataKind.Record && targetType.IsAssignableFrom(c: typeof(CodeRecord)))
        {
            return CreateRecord(type: type);
        }

        if (type.Kind == TypeMetadataKind.Struct && targetType.IsAssignableFrom(c: typeof(CodeStruct)))
        {
            return CreateStruct(type: type);
        }

        if (type.Kind == TypeMetadataKind.Interface && targetType.IsAssignableFrom(c: typeof(CodeInterface)))
        {
            return CreateInterface(type: type);
        }

        if (type.Kind == TypeMetadataKind.Enum && targetType.IsAssignableFrom(c: typeof(CodeEnum)))
        {
            return CreateEnum(type: type);
        }

        return targetType.IsAssignableFrom(c: typeof(CodeType))
            ? CreateType(type: type)
            : null;
    }

    private CodeClass CreateClass(TypeMetadata type)
    {
        return CreateClass(type: type, reference: null);
    }

    private CodeClass CreateClass(
        TypeMetadata type,
        TypeMetadataReference? reference)
    {
        var classType = reference is null ? CreateType(type: type) : CreateType(type: reference);
        return new CodeClass
        {
            AssemblyName = ResolveAssemblyName(assemblyName: type.AssemblyName),
            Name = type.Name,
            FullName = FormatGenericFullName(type: type),
            Namespace = type.Namespace,
            Attributes = CreateAttributes(attributes: type.Attributes, parent: null),
            BaseClass = CreateBaseClass(type: type),
            ContainingClass = CreateContainingClass(type: type),
            DocComment = CreateDocComment(docComment: type.DocComment, parent: null),
            Interfaces = CreateInterfaces(type: type),
            IsAbstract = type.IsAbstract,
            IsGeneric = classType.IsGeneric,
            IsStatic = type.IsStatic,
            Constants = new Typewriter.CodeModel.ConstantCollection(items: type.Constants.Select(selector: constant => CreateConstant(constant: constant, parent: null))),
            Delegates = new Typewriter.CodeModel.DelegateCollection(items: type.Delegates.Select(selector: @delegate => CreateDelegate(@delegate: @delegate, parent: null))),
            Events = new Typewriter.CodeModel.EventCollection(items: type.Events.Select(selector: @event => CreateEvent(@event: @event, parent: null))),
            Fields = new Typewriter.CodeModel.FieldCollection(items: type.Fields.Select(selector: field => CreateField(field: field, parent: null))),
            Methods = new Typewriter.CodeModel.MethodCollection(items: type.Methods.Select(selector: method => CreateMethod(method: method, parent: null))),
            NestedClasses = new Typewriter.CodeModel.ClassCollection(items: type.NestedClasses.Select(selector: CreateClass)),
            NestedEnums = new Typewriter.CodeModel.EnumCollection(items: type.NestedEnums.Select(selector: CreateEnum)),
            NestedInterfaces = new Typewriter.CodeModel.InterfaceCollection(items: type.NestedInterfaces.Select(selector: CreateInterface)),
            NestedRecords = new Typewriter.CodeModel.RecordCollection(items: type.NestedRecords.Select(selector: CreateRecord)),
            NestedStructs = new Typewriter.CodeModel.StructCollection(items: type.NestedStructs.Select(selector: CreateStruct)),
            Properties = new Typewriter.CodeModel.PropertyCollection(items: type.Properties.Select(selector: property => CreateProperty(property: property, parent: null))),
            StaticReadOnlyFields = new Typewriter.CodeModel.StaticReadOnlyFieldCollection(
                items: type.StaticReadOnlyFields.Select(selector: field => CreateStaticReadOnlyField(field: field, parent: null))),
            Type = classType,
            TypeArguments = classType.TypeArguments,
            TypeParameters = CreateTypeParameters(typeParameters: type.TypeParameters, parent: null),
        };
    }

    private CodeRecord CreateRecord(TypeMetadata type)
    {
        return CreateRecord(type: type, reference: null);
    }

    private CodeRecord CreateRecord(
        TypeMetadata type,
        TypeMetadataReference? reference)
    {
        var recordType = reference is null ? CreateType(type: type) : CreateType(type: reference);
        return new CodeRecord
        {
            AssemblyName = ResolveAssemblyName(assemblyName: type.AssemblyName),
            Name = type.Name,
            FullName = FormatGenericFullName(type: type),
            Namespace = type.Namespace,
            Attributes = CreateAttributes(attributes: type.Attributes, parent: null),
            BaseRecord = CreateBaseRecord(type: type),
            ContainingRecord = CreateContainingRecord(type: type),
            DocComment = CreateDocComment(docComment: type.DocComment, parent: null),
            Interfaces = CreateInterfaces(type: type),
            IsAbstract = type.IsAbstract,
            IsGeneric = recordType.IsGeneric,
            Constants = new Typewriter.CodeModel.ConstantCollection(items: type.Constants.Select(selector: constant => CreateConstant(constant: constant, parent: null))),
            Delegates = new Typewriter.CodeModel.DelegateCollection(items: type.Delegates.Select(selector: @delegate => CreateDelegate(@delegate: @delegate, parent: null))),
            Events = new Typewriter.CodeModel.EventCollection(items: type.Events.Select(selector: @event => CreateEvent(@event: @event, parent: null))),
            Fields = new Typewriter.CodeModel.FieldCollection(items: type.Fields.Select(selector: field => CreateField(field: field, parent: null))),
            Methods = new Typewriter.CodeModel.MethodCollection(items: type.Methods.Select(selector: method => CreateMethod(method: method, parent: null))),
            NestedClasses = new Typewriter.CodeModel.ClassCollection(items: type.NestedClasses.Select(selector: CreateClass)),
            NestedEnums = new Typewriter.CodeModel.EnumCollection(items: type.NestedEnums.Select(selector: CreateEnum)),
            NestedInterfaces = new Typewriter.CodeModel.InterfaceCollection(items: type.NestedInterfaces.Select(selector: CreateInterface)),
            NestedRecords = new Typewriter.CodeModel.RecordCollection(items: type.NestedRecords.Select(selector: CreateRecord)),
            NestedStructs = new Typewriter.CodeModel.StructCollection(items: type.NestedStructs.Select(selector: CreateStruct)),
            Properties = new Typewriter.CodeModel.PropertyCollection(items: type.Properties.Select(selector: property => CreateProperty(property: property, parent: null))),
            StaticReadOnlyFields = new Typewriter.CodeModel.StaticReadOnlyFieldCollection(
                items: type.StaticReadOnlyFields.Select(selector: field => CreateStaticReadOnlyField(field: field, parent: null))),
            Type = recordType,
            TypeArguments = recordType.TypeArguments,
            TypeParameters = CreateTypeParameters(typeParameters: type.TypeParameters, parent: null),
        };
    }

    private CodeInterface CreateInterface(TypeMetadata type)
    {
        return CreateInterface(type: type, reference: null);
    }

    private CodeInterface CreateInterface(
        TypeMetadata type,
        TypeMetadataReference? reference)
    {
        var interfaceType = reference is null ? CreateType(type: type) : CreateType(type: reference);
        return new CodeInterface
        {
            AssemblyName = ResolveAssemblyName(assemblyName: type.AssemblyName),
            Name = type.Name,
            FullName = FormatGenericFullName(type: type),
            Namespace = type.Namespace,
            Attributes = CreateAttributes(attributes: type.Attributes, parent: null),
            ContainingClass = CreateContainingClass(type: type),
            DocComment = CreateDocComment(docComment: type.DocComment, parent: null),
            Events = new Typewriter.CodeModel.EventCollection(items: type.Events.Select(selector: @event => CreateEvent(@event: @event, parent: null))),
            IsGeneric = interfaceType.IsGeneric,
            Interfaces = CreateInterfaces(type: type),
            Methods = new Typewriter.CodeModel.MethodCollection(items: type.Methods.Select(selector: method => CreateMethod(method: method, parent: null))),
            NestedStructs = new Typewriter.CodeModel.StructCollection(items: type.NestedStructs.Select(selector: CreateStruct)),
            Properties = new Typewriter.CodeModel.PropertyCollection(items: type.Properties.Select(selector: property => CreateProperty(property: property, parent: null))),
            Type = interfaceType,
            TypeArguments = interfaceType.TypeArguments,
            TypeParameters = CreateTypeParameters(typeParameters: type.TypeParameters, parent: null),
        };
    }

    private CodeStruct CreateStruct(TypeMetadata type)
    {
        return CreateStruct(type: type, reference: null);
    }

    private CodeStruct CreateStruct(
        TypeMetadata type,
        TypeMetadataReference? reference)
    {
        var structType = reference is null ? CreateType(type: type) : CreateType(type: reference);
        return new CodeStruct
        {
            AssemblyName = ResolveAssemblyName(assemblyName: type.AssemblyName),
            Name = type.Name,
            FullName = FormatGenericFullName(type: type),
            Namespace = type.Namespace,
            Attributes = CreateAttributes(attributes: type.Attributes, parent: null),
            ContainingClass = CreateContainingClass(type: type),
            ContainingStruct = CreateContainingStruct(type: type),
            DocComment = CreateDocComment(docComment: type.DocComment, parent: null),
            Interfaces = CreateInterfaces(type: type),
            IsGeneric = structType.IsGeneric,
            IsStatic = type.IsStatic,
            Constants = new Typewriter.CodeModel.ConstantCollection(items: type.Constants.Select(selector: constant => CreateConstant(constant: constant, parent: null))),
            Delegates = new Typewriter.CodeModel.DelegateCollection(items: type.Delegates.Select(selector: @delegate => CreateDelegate(@delegate: @delegate, parent: null))),
            Events = new Typewriter.CodeModel.EventCollection(items: type.Events.Select(selector: @event => CreateEvent(@event: @event, parent: null))),
            Fields = new Typewriter.CodeModel.FieldCollection(items: type.Fields.Select(selector: field => CreateField(field: field, parent: null))),
            Methods = new Typewriter.CodeModel.MethodCollection(items: type.Methods.Select(selector: method => CreateMethod(method: method, parent: null))),
            NestedClasses = new Typewriter.CodeModel.ClassCollection(items: type.NestedClasses.Select(selector: CreateClass)),
            NestedEnums = new Typewriter.CodeModel.EnumCollection(items: type.NestedEnums.Select(selector: CreateEnum)),
            NestedInterfaces = new Typewriter.CodeModel.InterfaceCollection(items: type.NestedInterfaces.Select(selector: CreateInterface)),
            NestedRecords = new Typewriter.CodeModel.RecordCollection(items: type.NestedRecords.Select(selector: CreateRecord)),
            NestedStructs = new Typewriter.CodeModel.StructCollection(items: type.NestedStructs.Select(selector: CreateStruct)),
            Properties = new Typewriter.CodeModel.PropertyCollection(items: type.Properties.Select(selector: property => CreateProperty(property: property, parent: null))),
            StaticReadOnlyFields = new Typewriter.CodeModel.StaticReadOnlyFieldCollection(
                items: type.StaticReadOnlyFields.Select(selector: field => CreateStaticReadOnlyField(field: field, parent: null))),
            Type = structType,
            TypeArguments = structType.TypeArguments,
            TypeParameters = CreateTypeParameters(typeParameters: type.TypeParameters, parent: null),
        };
    }

    private CodeEnum CreateEnum(TypeMetadata type)
    {
        var enumModel = new CodeEnum
        {
            AssemblyName = ResolveAssemblyName(assemblyName: type.AssemblyName),
            Name = type.Name,
            FullName = FormatGenericFullName(type: type),
            Namespace = type.Namespace,
            Attributes = CreateAttributes(attributes: type.Attributes, parent: null),
            ContainingClass = CreateContainingClass(type: type),
            DocComment = CreateDocComment(docComment: type.DocComment, parent: null),
            IsFlags = HasAttribute(attributes: type.Attributes, name: "Flags"),
            Type = CreateType(type: type),
            Values = new Typewriter.CodeModel.EnumValueCollection(items: type.EnumValues.Select(selector: value => CreateEnumValue(enumValue: value, parent: null))),
        };
        return enumModel;
    }

    private CodeType CreateType(TypeMetadata type)
    {
        return new CodeType
        {
            AssemblyName = ResolveAssemblyName(assemblyName: type.AssemblyName),
            Name = type.Name,
            FullName = FormatGenericFullName(type: type),
            Namespace = type.Namespace,
            Attributes = CreateAttributes(attributes: type.Attributes, parent: null),
            BaseClass = CreateBaseClass(type: type),
            ContainingClass = CreateContainingClass(type: type),
            DocComment = CreateDocComment(docComment: type.DocComment, parent: null),
            FileLocations = type.FileLocations,
            IsDefined = true,
            IsEnum = type.Kind == TypeMetadataKind.Enum,
            IsGeneric = type.TypeParameters.Count > 0 || type.TypeArguments.Count > 0,
            IsStruct = type.Kind == TypeMetadataKind.Struct,
            Interfaces = CreateInterfaces(type: type),
            Constants = new Typewriter.CodeModel.ConstantCollection(items: type.Constants.Select(selector: constant => CreateConstant(constant: constant, parent: null))),
            Delegates = new Typewriter.CodeModel.DelegateCollection(items: type.Delegates.Select(selector: @delegate => CreateDelegate(@delegate: @delegate, parent: null))),
            Fields = new Typewriter.CodeModel.FieldCollection(items: type.Fields.Select(selector: field => CreateField(field: field, parent: null))),
            Methods = new Typewriter.CodeModel.MethodCollection(items: type.Methods.Select(selector: method => CreateMethod(method: method, parent: null))),
            NestedClasses = new Typewriter.CodeModel.ClassCollection(items: type.NestedClasses.Select(selector: CreateClass)),
            NestedEnums = new Typewriter.CodeModel.EnumCollection(items: type.NestedEnums.Select(selector: CreateEnum)),
            NestedInterfaces = new Typewriter.CodeModel.InterfaceCollection(items: type.NestedInterfaces.Select(selector: CreateInterface)),
            NestedRecords = new Typewriter.CodeModel.RecordCollection(items: type.NestedRecords.Select(selector: CreateRecord)),
            NestedStructs = new Typewriter.CodeModel.StructCollection(items: type.NestedStructs.Select(selector: CreateStruct)),
            Properties = new Typewriter.CodeModel.PropertyCollection(items: type.Properties.Select(selector: property => CreateProperty(property: property, parent: null))),
            StaticReadOnlyFields = new Typewriter.CodeModel.StaticReadOnlyFieldCollection(
                items: type.StaticReadOnlyFields.Select(selector: field => CreateStaticReadOnlyField(field: field, parent: null))),
            TypeArguments = new Typewriter.CodeModel.TypeCollection(items: type.TypeArguments.Select(selector: CreateType)),
            TypeParameters = CreateTypeParameters(typeParameters: type.TypeParameters, parent: null),
            DefaultValue = GetDefaultValue(type: type),
            Settings = _settings,
        };
    }

    private CodeType CreateType(TypeMetadataReference type)
    {
        var typeArguments = new Typewriter.CodeModel.TypeCollection(items: type.TypeArguments.Select(selector: CreateType));
        var mappedName = _typeMapper.Map(
            type: type,
            strictNull: _settings.StrictNullGeneration,
            dateType: _settings.DateTypeGeneration,
            decimalType: _settings.DecimalTypeGeneration);
        var isStruct = _typesByFullName.TryGetValue(key: type.FullName, value: out var metadata)
                       && metadata.Kind == TypeMetadataKind.Struct;
        return new CodeType
        {
            AssemblyName = ResolveAssemblyName(assemblyName: type.AssemblyName),
            Name = GetLegacyTypeName(mappedName: mappedName),
            FullName = type.FullName,
            Namespace = type.Namespace,
            ElementType = type.ElementType is null ? null : CreateType(type: type.ElementType),
            IsDate = type.IsDateLike,
            IsDictionary = type.IsDictionary,
            IsDynamic = IsDynamic(type: type),
            IsEnum = type.IsEnum,
            IsEnumerable = type.IsCollection,
            IsGuid = type.FullName.Equals(value: "System.Guid", comparisonType: StringComparison.Ordinal),
            IsGeneric = typeArguments.Count > 0,
            IsNullable = type.IsNullable,
            IsPrimitive = IsPrimitiveType(type: type),
            IsStruct = isStruct,
            IsTask = IsTaskLike(fullName: type.FullName),
            IsTimeSpan = type.FullName.Equals(value: "System.TimeSpan", comparisonType: StringComparison.Ordinal),
            IsValueTuple = type.IsValueTuple,
            OriginalName = type.Name,
            TupleElements = new Typewriter.CodeModel.FieldCollection(items: type.TupleElements.Select(selector: field => CreateField(field: field, parent: null))),
            TypeArguments = typeArguments,
            DefaultValue = GetDefaultValue(type: type),
            Settings = _settings,
        };
    }

    private Typewriter.CodeModel.Property CreateProperty(
        PropertyMetadata property,
        Typewriter.CodeModel.Item? parent)
    {
        return CreateProperty(property: property, parent: parent, includeParameters: true);
    }

    private Typewriter.CodeModel.Property CreateProperty(
        PropertyMetadata property,
        Typewriter.CodeModel.Item? parent,
        bool includeParameters)
    {
        return new Typewriter.CodeModel.Property
        {
            AssemblyName = ResolveAssemblyName(assemblyName: property.AssemblyName),
            Name = property.Name,
            FullName = property.FullName,
            Parent = parent ?? CreateParentTypeItem(fullName: property.ParentTypeFullName),
            Attributes = CreateAttributes(attributes: property.Attributes, parent: null),
            DocComment = CreateDocComment(docComment: property.DocComment, parent: null),
            HasGetter = property.HasGetter,
            HasSetter = property.HasSetter,
            IsAbstract = property.IsAbstract,
            IsIndexer = property.IsIndexer,
            IsRequired = property.IsRequired,
            IsVirtual = property.IsVirtual,
            Parameters = includeParameters
                ? new Typewriter.CodeModel.ParameterCollection(items: property.Parameters.Select(selector: parameter => CreateParameter(parameter: parameter, parent: null)))
                : new Typewriter.CodeModel.ParameterCollection(),
            Type = CreateType(type: property.Type),
            Value = property.Value ?? string.Empty,
        };
    }

    private CodeMethod CreateMethod(
        MethodMetadata method,
        Typewriter.CodeModel.Item? parent)
    {
        return CreateMethod(method: method, parent: parent, includeParameters: true);
    }

    private CodeMethod CreateMethod(
        MethodMetadata method,
        Typewriter.CodeModel.Item? parent,
        bool includeParameters)
    {
        return new CodeMethod
        {
            AssemblyName = ResolveAssemblyName(assemblyName: method.AssemblyName),
            Name = method.Name,
            FullName = method.FullName,
            Parent = parent ?? CreateParentTypeItem(fullName: method.ParentTypeFullName),
            Attributes = CreateAttributes(attributes: method.Attributes, parent: null),
            DocComment = CreateDocComment(docComment: method.DocComment, parent: null),
            IsAbstract = method.IsAbstract,
            IsGeneric = method.IsGeneric,
            Parameters = includeParameters
                ? new Typewriter.CodeModel.ParameterCollection(items: method.Parameters.Select(selector: parameter => CreateParameter(parameter: parameter, parent: null)))
                : new Typewriter.CodeModel.ParameterCollection(),
            Type = CreateType(type: method.ReturnType),
            TypeParameters = CreateTypeParameters(typeParameters: method.TypeParameters, parent: null),
        };
    }

    private CodeParameter CreateParameter(
        ParameterMetadata parameter,
        Typewriter.CodeModel.Item? parent)
    {
        return new CodeParameter
        {
            AssemblyName = ResolveAssemblyName(assemblyName: parameter.AssemblyName),
            Name = parameter.Name,
            FullName = parameter.FullName,
            Parent = parent
                ?? (Typewriter.CodeModel.Item?)CreateParentMethod(fullName: parameter.ParentMethodFullName)
                ?? CreateParentProperty(fullName: parameter.ParentPropertyFullName),
            Attributes = CreateAttributes(attributes: parameter.Attributes, parent: null),
            DefaultValue = parameter.DefaultValue ?? string.Empty,
            HasDefaultValue = parameter.HasDefaultValue,
            Type = CreateType(type: parameter.Type),
        };
    }

    private CodeConstant CreateConstant(
        ConstantMetadata constant,
        Typewriter.CodeModel.Item? parent)
    {
        return new CodeConstant
        {
            AssemblyName = ResolveAssemblyName(assemblyName: constant.AssemblyName),
            Name = constant.Name,
            FullName = constant.FullName,
            Parent = parent ?? CreateParentTypeItem(fullName: constant.ParentTypeFullName),
            Attributes = CreateAttributes(attributes: constant.Attributes, parent: null),
            DocComment = CreateDocComment(docComment: constant.DocComment, parent: null),
            Type = CreateType(type: constant.Type),
            Value = constant.Value ?? string.Empty,
        };
    }

    private CodeField CreateField(
        FieldMetadata field,
        Typewriter.CodeModel.Item? parent)
    {
        return new CodeField
        {
            AssemblyName = ResolveAssemblyName(assemblyName: field.AssemblyName),
            Name = field.Name,
            FullName = field.FullName,
            Parent = parent ?? CreateParentTypeItem(fullName: field.ParentTypeFullName),
            Attributes = CreateAttributes(attributes: field.Attributes, parent: null),
            DocComment = CreateDocComment(docComment: field.DocComment, parent: null),
            Type = CreateType(type: field.Type),
            Value = field.Value ?? string.Empty,
        };
    }

    private CodeStaticReadOnlyField CreateStaticReadOnlyField(
        StaticReadOnlyFieldMetadata field,
        Typewriter.CodeModel.Item? parent)
    {
        return new CodeStaticReadOnlyField
        {
            AssemblyName = ResolveAssemblyName(assemblyName: field.AssemblyName),
            Name = field.Name,
            FullName = field.FullName,
            Parent = parent ?? CreateParentTypeItem(fullName: field.ParentTypeFullName),
            Attributes = CreateAttributes(attributes: field.Attributes, parent: null),
            DocComment = CreateDocComment(docComment: field.DocComment, parent: null),
            Type = CreateType(type: field.Type),
            Value = field.Value ?? string.Empty,
        };
    }

    private CodeEvent CreateEvent(
        EventMetadata @event,
        Typewriter.CodeModel.Item? parent)
    {
        return new CodeEvent
        {
            AssemblyName = ResolveAssemblyName(assemblyName: @event.AssemblyName),
            Name = @event.Name,
            FullName = @event.FullName,
            Parent = parent ?? CreateParentTypeItem(fullName: @event.ParentTypeFullName),
            Attributes = CreateAttributes(attributes: @event.Attributes, parent: null),
            DocComment = CreateDocComment(docComment: @event.DocComment, parent: null),
            Type = CreateType(type: @event.Type),
        };
    }

    private CodeDelegate CreateDelegate(
        DelegateMetadata @delegate,
        Typewriter.CodeModel.Item? parent)
    {
        return new CodeDelegate
        {
            AssemblyName = ResolveAssemblyName(assemblyName: @delegate.AssemblyName),
            Name = @delegate.Name,
            FullName = @delegate.FullName,
            Parent = parent ?? CreateParentTypeItem(fullName: @delegate.ParentTypeFullName),
            Attributes = CreateAttributes(attributes: @delegate.Attributes, parent: null),
            DocComment = CreateDocComment(docComment: @delegate.DocComment, parent: null),
            IsGeneric = @delegate.IsGeneric,
            Parameters = new Typewriter.CodeModel.ParameterCollection(items: @delegate.Parameters.Select(selector: parameter => CreateParameter(parameter: parameter, parent: null))),
            Type = CreateType(type: @delegate.ReturnType),
            TypeParameters = CreateTypeParameters(typeParameters: @delegate.TypeParameters, parent: null),
        };
    }

    private CodeAttribute CreateAttribute(
        AttributeMetadata attribute,
        Typewriter.CodeModel.Item? parent)
    {
        return new CodeAttribute
        {
            AssemblyName = ResolveAssemblyName(assemblyName: attribute.AssemblyName),
            Name = attribute.Name,
            FullName = attribute.FullName,
            Parent = parent,
            Arguments = new Typewriter.CodeModel.AttributeArgumentCollection(items: attribute.Arguments.Select(selector: CreateAttributeArgument)),
            Type = attribute.Type is null ? null : CreateType(type: attribute.Type),
            Value = TemplateAttributeValueFormatter.Format(attribute: attribute),
        };
    }

    private Typewriter.CodeModel.AttributeArgument CreateAttributeArgument(AttributeArgumentMetadata argument)
    {
        return new Typewriter.CodeModel.AttributeArgument
        {
            AssemblyName = ResolveAssemblyName(assemblyName: argument.AssemblyName),
            Name = argument.Name ?? string.Empty,
            Type = argument.Type is null ? null : CreateType(type: argument.Type),
            TypeValue = argument.TypeValue is null ? null : CreateType(type: argument.TypeValue),
            Value = argument.Value ?? string.Empty,
        };
    }

    private Typewriter.CodeModel.EnumValue CreateEnumValue(
        EnumValueMetadata enumValue,
        CodeEnum? parent)
    {
        return new Typewriter.CodeModel.EnumValue
        {
            AssemblyName = ResolveAssemblyName(assemblyName: enumValue.AssemblyName),
            Name = enumValue.Name,
            FullName = string.IsNullOrWhiteSpace(value: enumValue.ParentTypeFullName)
                ? enumValue.Name
                : string.Concat(str0: enumValue.ParentTypeFullName, str1: ".", str2: enumValue.Name),
            Parent = parent ?? CreateParentEnum(fullName: enumValue.ParentTypeFullName),
            Attributes = CreateAttributes(attributes: enumValue.Attributes, parent: null),
            DocComment = CreateDocComment(docComment: enumValue.DocComment, parent: null),
            Value = enumValue.Value ?? 0,
        };
    }

    private Typewriter.CodeModel.InterfaceCollection CreateInterfaces(TypeMetadata type)
    {
        return new Typewriter.CodeModel.InterfaceCollection(
            items: type.BaseTypes
                .Where(predicate: IsInterfaceReference)
                .Select(selector: CreateInterfaceReference));
    }

    private bool IsInterfaceReference(TypeMetadataReference reference)
    {
        if (_typesByFullName.TryGetValue(key: reference.FullName, value: out var metadata))
        {
            return metadata.Kind == TypeMetadataKind.Interface;
        }

        // Heuristic: names like "ISomething" (I + uppercase) are likely interfaces.
        return reference.Name.Length > 1
            && reference.Name[index: 0] == 'I'
            && char.IsUpper(c: reference.Name[index: 1]);
    }

    private CodeInterface CreateInterfaceReference(TypeMetadataReference reference)
    {
        if (_typesByFullName.TryGetValue(key: reference.FullName, value: out var metadata)
            && metadata.Kind == TypeMetadataKind.Interface)
        {
            return CreateInterface(type: metadata, reference: reference);
        }

        var interfaceType = CreateType(type: reference);
        return new CodeInterface
        {
            AssemblyName = ResolveAssemblyName(assemblyName: reference.AssemblyName),
            Name = reference.Name,
            FullName = reference.FullName,
            Namespace = reference.Namespace,
            IsGeneric = interfaceType.IsGeneric,
            Type = interfaceType,
            TypeArguments = interfaceType.TypeArguments,
        };
    }

    private Typewriter.CodeModel.AttributeCollection CreateAttributes(
        IEnumerable<AttributeMetadata> attributes,
        Typewriter.CodeModel.Item? parent)
    {
        return new Typewriter.CodeModel.AttributeCollection(items: attributes.Select(selector: attribute => CreateAttribute(attribute: attribute, parent: parent)));
    }

    private Typewriter.CodeModel.Item? CreateParentTypeItem(string fullName)
    {
        if (string.IsNullOrWhiteSpace(value: fullName)
            || !_typesByFullName.TryGetValue(key: fullName, value: out var type))
        {
            return null;
        }

        return type.Kind switch
        {
            TypeMetadataKind.Class => CreateShallowClass(type: type),
            TypeMetadataKind.Record => CreateShallowRecord(type: type),
            TypeMetadataKind.Struct => CreateShallowStruct(type: type),
            TypeMetadataKind.Interface => CreateShallowInterface(type: type),
            TypeMetadataKind.Enum => CreateShallowEnum(type: type),
            _ => null,
        };
    }

    private CodeMethod? CreateParentMethod(string fullName)
    {
        return !string.IsNullOrWhiteSpace(value: fullName)
            && _methodsByFullName.TryGetValue(key: fullName, value: out var method)
                ? CreateMethod(method: method, parent: CreateParentTypeItem(fullName: method.ParentTypeFullName), includeParameters: false)
                : null;
    }

    private Typewriter.CodeModel.Property? CreateParentProperty(string fullName)
    {
        return !string.IsNullOrWhiteSpace(value: fullName)
            && _propertiesByFullName.TryGetValue(key: fullName, value: out var property)
                ? CreateProperty(property: property, parent: CreateParentTypeItem(fullName: property.ParentTypeFullName), includeParameters: false)
                : null;
    }

    private CodeEnum? CreateParentEnum(string fullName)
    {
        return !string.IsNullOrWhiteSpace(value: fullName)
            && _typesByFullName.TryGetValue(key: fullName, value: out var type)
            && type.Kind == TypeMetadataKind.Enum
                ? CreateShallowEnum(type: type)
                : null;
    }

    private CodeClass? CreateContainingClass(TypeMetadata type)
    {
        return !string.IsNullOrWhiteSpace(value: type.ContainingTypeFullName)
            && _typesByFullName.TryGetValue(key: type.ContainingTypeFullName, value: out var containingType)
            && containingType.Kind == TypeMetadataKind.Class
                ? CreateShallowClass(type: containingType)
                : null;
    }

    private CodeRecord? CreateContainingRecord(TypeMetadata type)
    {
        return !string.IsNullOrWhiteSpace(value: type.ContainingTypeFullName)
            && _typesByFullName.TryGetValue(key: type.ContainingTypeFullName, value: out var containingType)
            && containingType.Kind == TypeMetadataKind.Record
                ? CreateShallowRecord(type: containingType)
                : null;
    }

    private CodeStruct? CreateContainingStruct(TypeMetadata type)
    {
        return !string.IsNullOrWhiteSpace(value: type.ContainingTypeFullName)
            && _typesByFullName.TryGetValue(key: type.ContainingTypeFullName, value: out var containingType)
            && containingType.Kind == TypeMetadataKind.Struct
                ? CreateShallowStruct(type: containingType)
                : null;
    }

    private CodeClass CreateShallowClass(TypeMetadata type)
    {
        return new CodeClass
        {
            AssemblyName = ResolveAssemblyName(assemblyName: type.AssemblyName),
            Name = type.Name,
            FullName = FormatGenericFullName(type: type),
            Namespace = type.Namespace,
            Attributes = CreateAttributes(attributes: type.Attributes, parent: null),
            IsAbstract = type.IsAbstract,
            IsGeneric = type.TypeParameters.Count > 0 || type.TypeArguments.Count > 0,
            IsStatic = type.IsStatic,
            Type = CreateTypeShell(type: type),
            TypeArguments = new Typewriter.CodeModel.TypeCollection(items: type.TypeArguments.Select(selector: CreateType)),
            TypeParameters = CreateTypeParameters(typeParameters: type.TypeParameters, parent: null),
        };
    }

    private CodeRecord CreateShallowRecord(TypeMetadata type)
    {
        return new CodeRecord
        {
            AssemblyName = ResolveAssemblyName(assemblyName: type.AssemblyName),
            Name = type.Name,
            FullName = FormatGenericFullName(type: type),
            Namespace = type.Namespace,
            Attributes = CreateAttributes(attributes: type.Attributes, parent: null),
            IsAbstract = type.IsAbstract,
            IsGeneric = type.TypeParameters.Count > 0 || type.TypeArguments.Count > 0,
            Type = CreateTypeShell(type: type),
            TypeArguments = new Typewriter.CodeModel.TypeCollection(items: type.TypeArguments.Select(selector: CreateType)),
            TypeParameters = CreateTypeParameters(typeParameters: type.TypeParameters, parent: null),
        };
    }

    private CodeStruct CreateShallowStruct(TypeMetadata type)
    {
        return new CodeStruct
        {
            AssemblyName = ResolveAssemblyName(assemblyName: type.AssemblyName),
            Name = type.Name,
            FullName = FormatGenericFullName(type: type),
            Namespace = type.Namespace,
            Attributes = CreateAttributes(attributes: type.Attributes, parent: null),
            IsGeneric = type.TypeParameters.Count > 0 || type.TypeArguments.Count > 0,
            IsStatic = type.IsStatic,
            Type = CreateTypeShell(type: type),
            TypeArguments = new Typewriter.CodeModel.TypeCollection(items: type.TypeArguments.Select(selector: CreateType)),
            TypeParameters = CreateTypeParameters(typeParameters: type.TypeParameters, parent: null),
        };
    }

    private CodeInterface CreateShallowInterface(TypeMetadata type)
    {
        return new CodeInterface
        {
            AssemblyName = ResolveAssemblyName(assemblyName: type.AssemblyName),
            Name = type.Name,
            FullName = FormatGenericFullName(type: type),
            Namespace = type.Namespace,
            Attributes = CreateAttributes(attributes: type.Attributes, parent: null),
            IsGeneric = type.TypeParameters.Count > 0 || type.TypeArguments.Count > 0,
            Type = CreateTypeShell(type: type),
            TypeArguments = new Typewriter.CodeModel.TypeCollection(items: type.TypeArguments.Select(selector: CreateType)),
            TypeParameters = CreateTypeParameters(typeParameters: type.TypeParameters, parent: null),
        };
    }

    private CodeEnum CreateShallowEnum(TypeMetadata type)
    {
        return new CodeEnum
        {
            AssemblyName = ResolveAssemblyName(assemblyName: type.AssemblyName),
            Name = type.Name,
            FullName = FormatGenericFullName(type: type),
            Namespace = type.Namespace,
            Attributes = CreateAttributes(attributes: type.Attributes, parent: null),
            IsFlags = HasAttribute(attributes: type.Attributes, name: "Flags"),
            Type = CreateTypeShell(type: type),
        };
    }

    private CodeType CreateTypeShell(TypeMetadata type)
    {
        return new CodeType
        {
            AssemblyName = ResolveAssemblyName(assemblyName: type.AssemblyName),
            Name = type.Name,
            FullName = FormatGenericFullName(type: type),
            Namespace = type.Namespace,
            Attributes = CreateAttributes(attributes: type.Attributes, parent: null),
            FileLocations = type.FileLocations,
            IsDefined = true,
            IsEnum = type.Kind == TypeMetadataKind.Enum,
            IsGeneric = type.TypeParameters.Count > 0 || type.TypeArguments.Count > 0,
            IsStruct = type.Kind == TypeMetadataKind.Struct,
            DefaultValue = GetDefaultValue(type: type),
            Settings = _settings,
            TypeArguments = new Typewriter.CodeModel.TypeCollection(items: type.TypeArguments.Select(selector: CreateType)),
            TypeParameters = CreateTypeParameters(typeParameters: type.TypeParameters, parent: null),
        };
    }

    private CodeClass? CreateBaseClass(TypeMetadata type)
    {
        var baseType = type.BaseTypes.FirstOrDefault(predicate: reference => !IsInterfaceReference(reference: reference));
        if (baseType is null)
        {
            return null;
        }

        return _typesByFullName.TryGetValue(key: baseType.FullName, value: out var baseMetadata)
            && baseMetadata.Kind == TypeMetadataKind.Class
                ? CreateClass(type: baseMetadata, reference: baseType)
                : new CodeClass
                {
                    AssemblyName = ResolveAssemblyName(assemblyName: baseType.AssemblyName),
                    Name = baseType.Name,
                    FullName = baseType.FullName,
                    Namespace = baseType.Namespace,
                    IsGeneric = baseType.TypeArguments.Count > 0,
                    Type = CreateType(type: baseType),
                    TypeArguments = new Typewriter.CodeModel.TypeCollection(items: baseType.TypeArguments.Select(selector: CreateType)),
                };
    }

    private CodeRecord? CreateBaseRecord(TypeMetadata type)
    {
        var baseType = type.BaseTypes.FirstOrDefault(predicate: IsRecordBaseReference);
        if (baseType is null)
        {
            return null;
        }

        return _typesByFullName.TryGetValue(key: baseType.FullName, value: out var baseMetadata)
            && baseMetadata.Kind == TypeMetadataKind.Record
                ? CreateRecord(type: baseMetadata, reference: baseType)
                : new CodeRecord
                {
                    AssemblyName = ResolveAssemblyName(assemblyName: baseType.AssemblyName),
                    Name = baseType.Name,
                    FullName = baseType.FullName,
                    Namespace = baseType.Namespace,
                    IsGeneric = baseType.TypeArguments.Count > 0,
                    Type = CreateType(type: baseType),
                    TypeArguments = new Typewriter.CodeModel.TypeCollection(items: baseType.TypeArguments.Select(selector: CreateType)),
                };
    }

    private bool IsRecordBaseReference(TypeMetadataReference reference)
    {
        if (_typesByFullName.TryGetValue(key: reference.FullName, value: out var metadata))
        {
            return metadata.Kind == TypeMetadataKind.Record;
        }

        // Heuristic: exclude names that look like interfaces (I + uppercase) and
        // System.I* types, which are far more likely to be interfaces than records.
        if (reference.Name.Length > 1
            && reference.Name[index: 0] == 'I'
            && char.IsUpper(c: reference.Name[index: 1]))
        {
            return false;
        }

        if (reference.FullName.StartsWith(value: "System.I", comparisonType: StringComparison.Ordinal))
        {
            return false;
        }

        // Non-interface, non-class external base is conservatively treated as not a record
        // since the fallback CreateBaseRecord already handles the shell case.
        return false;
    }

    private string ResolveAssemblyName(string? assemblyName)
    {
        return string.IsNullOrWhiteSpace(value: assemblyName)
            ? _assemblyName
            : assemblyName;
    }

    private string GetDefaultValue(TypeMetadataReference type)
    {
        if (type.IsNullable)
        {
            return "null";
        }

        if (type.IsDictionary)
        {
            return "{}";
        }

        if (type.IsCollection)
        {
            return "[]";
        }

        var stringLiteralCharacter = _settings.StringLiteralCharacter;
        if (type.FullName.Equals(value: "System.TimeSpan", comparisonType: StringComparison.Ordinal))
        {
            return $"{stringLiteralCharacter}00:00:00{stringLiteralCharacter}";
        }

        if (type.FullName.Equals(value: "System.Decimal", comparisonType: StringComparison.Ordinal)
            && !_settings.DecimalTypeGeneration.Equals(value: TypeScriptTypeMapper.DefaultDecimalType, comparisonType: StringComparison.Ordinal))
        {
            return $"new {_settings.DecimalTypeGeneration}(0)";
        }

        if (type.IsDateLike)
        {
            return "new Date()";
        }

        if (type.IsEnum)
        {
            return ResolveEnumDefault(type: type) ?? "0";
        }

        if (type.Name.Equals(value: "Boolean", comparisonType: StringComparison.OrdinalIgnoreCase)
            || type.Name.Equals(value: "bool", comparisonType: StringComparison.OrdinalIgnoreCase)
            || type.FullName.Equals(value: "System.Boolean", comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            return "false";
        }

        if (type.IsPrimitive
            && !type.Name.Equals(value: "String", comparisonType: StringComparison.OrdinalIgnoreCase)
            && !type.FullName.Equals(value: "System.String", comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            return "0";
        }

        return type.Name.Equals(value: "String", comparisonType: StringComparison.OrdinalIgnoreCase)
            || type.FullName.Equals(value: "System.String", comparisonType: StringComparison.OrdinalIgnoreCase)
                ? $"{stringLiteralCharacter}{stringLiteralCharacter}"
                : "null";
    }

    private string? ResolveEnumDefault(TypeMetadataReference type)
    {
        if (type.EnumValues.Count > 0)
        {
            return ResolveEnumDefault(enumName: type.Name, enumValues: type.EnumValues);
        }

        return _typesByFullName.TryGetValue(key: type.FullName, value: out var enumMetadata)
            ? ResolveEnumDefault(enumName: type.Name, enumValues: enumMetadata.EnumValues)
            : null;
    }
}
