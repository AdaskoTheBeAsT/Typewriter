using System.Diagnostics.CodeAnalysis;
using System.Globalization;
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
    private readonly ProjectMetadata _metadata;
    private readonly Typewriter.Configuration.Settings _settings;
    private readonly string _assemblyName;
    private readonly ProjectMetadataIndex _metadataIndex;
    private readonly IReadOnlyDictionary<string, MethodMetadata> _methodsByFullName;
    private readonly IReadOnlyDictionary<string, PropertyMetadata> _propertiesByFullName;
    private readonly IReadOnlyDictionary<string, SourceFileMetadata> _sourceFilesByPath;
    private readonly Dictionary<string, CodeFile> _filesBySourcePath = new(comparer: StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<Typewriter.CodeModel.Item, object> _metadataByItem = new(comparer: ReferenceEqualityComparer.Instance);
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
        _metadata = metadata;
        _settings = settings;
        _assemblyName = Path.GetFileNameWithoutExtension(path: metadata.ProjectPath);
        var index = metadataIndex ?? ProjectMetadataIndex.Create(metadata: metadata);
        _metadataIndex = index;
        _methodsByFullName = index.MethodsByFullName;
        _propertiesByFullName = index.PropertiesByFullName;
        _sourceFilesByPath = index.Metadata.SourceFiles
            .Concat(second: metadata.SourceFiles)
            .GroupBy(keySelector: sourceFile => sourceFile.Path, comparer: StringComparer.OrdinalIgnoreCase)
            .ToDictionary(keySelector: group => group.Key, elementSelector: group => group.Last(), comparer: StringComparer.OrdinalIgnoreCase);
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
            TypeMappingContext typeContext => CreateType(type: typeContext.Reference, runtimeType: typeContext.RuntimeType),
            CodeClass codeClass when targetType.IsAssignableFrom(c: typeof(CodeType)) => codeClass.Type,
            CodeRecord codeRecord when targetType.IsAssignableFrom(c: typeof(CodeType)) => codeRecord.Type,
            CodeStruct codeStruct when targetType.IsAssignableFrom(c: typeof(CodeType)) => codeStruct.Type,
            CodeInterface codeInterface when targetType.IsAssignableFrom(c: typeof(CodeType)) => codeInterface.Type,
            CodeEnum codeEnum when targetType.IsAssignableFrom(c: typeof(CodeType)) => codeEnum.Type,
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

        var sourcePaths = GetProjectSourcePaths(project: project);
        var filePath = sourcePaths.Length == 1 ? sourcePaths[0] : project.ProjectPath;
        var isSourceFile = sourcePaths.Length <= 1;
        if (!isSourceFile || !_filesBySourcePath.TryGetValue(key: filePath, value: out var file))
        {
            file = CreateFileModel(project: project, filePath: filePath, attachParents: isSourceFile);
        }

        _cachedFile = file;
        _cachedFileProject = project;
        if (isSourceFile)
        {
            _filesBySourcePath[filePath] = file;
        }

        return file;
    }

    public CodeFile ResolveParentFile(TypeMetadata type)
    {
        var filePath = GetPrimaryFilePath(type: type);
        var sourceFile = FindSourceFile(
            filePath: filePath,
            containsDeclaration: source => source.Types.Any(
                predicate: candidate => candidate.FullName.Equals(value: type.FullName, comparisonType: StringComparison.Ordinal)
                    && candidate.Kind == type.Kind
                    && candidate.TypeParameters.Count == type.TypeParameters.Count
                    && candidate.AssemblyName.Equals(value: type.AssemblyName, comparisonType: StringComparison.Ordinal)));
        return ResolveParentFile(sourceFile: sourceFile, filePath: filePath);
    }

    public CodeFile ResolveParentFile(DelegateMetadata @delegate)
    {
        var filePath = @delegate.Location?.Path;
        var sourceFile = FindSourceFile(
            filePath: filePath,
            containsDeclaration: source => source.Delegates.Any(
                predicate: candidate => candidate.FullName.Equals(value: @delegate.FullName, comparisonType: StringComparison.Ordinal)
                    && candidate.AssemblyName.Equals(value: @delegate.AssemblyName, comparisonType: StringComparison.Ordinal)));
        return ResolveParentFile(sourceFile: sourceFile, filePath: filePath);
    }

    public object GetFilterContext(object item) =>
        item is Typewriter.CodeModel.Item codeItem && _metadataByItem.TryGetValue(key: codeItem, value: out var metadata)
            ? metadata
            : item;

    public Typewriter.CodeModel.InterfaceCollection CreateInterfaces(
        TypeMetadata type,
        TypeMetadataReference? reference = null)
    {
        return new Typewriter.CodeModel.InterfaceCollection(
            items: type.BaseTypes
                .Where(predicate: IsInterfaceReference)
                .Select(selector: baseInterface => CreateInterfaceReference(
                    reference: SubstituteTypeArguments(type: type, reference: reference, target: baseInterface))));
    }

    public Typewriter.CodeModel.InterfaceCollection CreateInterfaces(TypeMetadataReference reference)
    {
        _ = TryResolveType(reference: reference, metadata: out var metadata);
        return CreateInterfaces(reference: reference, metadata: metadata);
    }

    private static IReadOnlyList<string> ResolveFileLocations(
        TypeMetadataReference type,
        TypeMetadata? metadata)
    {
        return type.FileLocations.Count > 0
            ? type.FileLocations
            : metadata?.FileLocations ?? [];
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
            || TypeScriptTemporalTypes.IsDateTime(fullName: effectiveType.FullName)
            || TypeScriptTemporalTypes.IsDateOnly(fullName: effectiveType.FullName)
            || TypeScriptTemporalTypes.IsTimeOnly(fullName: effectiveType.FullName)
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

    private static TypeMetadataReference SubstituteTypeArguments(
        TypeMetadata type,
        TypeMetadataReference? reference,
        TypeMetadataReference target)
    {
        if (reference is null
            || type.TypeParameters.Count == 0
            || type.TypeParameters.Count != reference.TypeArguments.Count)
        {
            return target;
        }

        var substitutions = new Dictionary<string, TypeMetadataReference>(comparer: StringComparer.Ordinal);
        for (var index = 0; index < type.TypeParameters.Count; index++)
        {
            substitutions[type.TypeParameters[index: index].Name] = reference.TypeArguments[index: index];
        }

        return SubstituteTypeArguments(target: target, substitutions: substitutions);
    }

    private static TypeMetadataReference SubstituteTypeArguments(
        TypeMetadataReference target,
        IReadOnlyDictionary<string, TypeMetadataReference> substitutions)
    {
        if (target.Arity == 0
            && target.TypeArguments.Count == 0
            && (substitutions.TryGetValue(key: target.Name, value: out var replacement)
                || substitutions.TryGetValue(key: target.FullName, value: out replacement)))
        {
            return target.IsNullable && !replacement.IsNullable
                ? replacement with
                {
                    IsNullable = true,
                }
                : replacement;
        }

        var hasChanges = false;
        var typeArguments = target.TypeArguments;
        if (target.TypeArguments.Count > 0)
        {
            var substitutedArguments = new TypeMetadataReference[target.TypeArguments.Count];
            for (var index = 0; index < target.TypeArguments.Count; index++)
            {
                var originalArgument = target.TypeArguments[index: index];
                var substitutedArgument = SubstituteTypeArguments(target: originalArgument, substitutions: substitutions);
                substitutedArguments[index] = substitutedArgument;
                if (!ReferenceEquals(objA: originalArgument, objB: substitutedArgument))
                {
                    hasChanges = true;
                }
            }

            if (hasChanges)
            {
                typeArguments = substitutedArguments;
            }
        }

        var elementType = target.ElementType;
        if (elementType is not null)
        {
            var substitutedElement = SubstituteTypeArguments(target: elementType, substitutions: substitutions);
            if (!ReferenceEquals(objA: elementType, objB: substitutedElement))
            {
                elementType = substitutedElement;
                hasChanges = true;
            }
        }

        if (!hasChanges)
        {
            return target;
        }

        return target with
        {
            TypeArguments = typeArguments,
            ElementType = elementType,
            Interfaces = new LazySubstitutedTypeReferenceList(source: target.Interfaces, substitutions: substitutions),
        };
    }

    private static string GetPrimaryFilePath(TypeMetadata type) =>
        !string.IsNullOrWhiteSpace(value: type.Location?.Path)
            ? type.Location.Path
            : type.FileLocations.FirstOrDefault(predicate: path => !string.IsNullOrWhiteSpace(value: path)) ?? string.Empty;

    private static string[] GetProjectSourcePaths(ProjectMetadata project) =>
        (project.SourceFiles.Count > 0
            ? project.SourceFiles.Select(selector: sourceFile => sourceFile.Path)
            : project.Types.Select(selector: GetPrimaryFilePath)
                .Concat(second: project.Delegates.Select(selector: @delegate => @delegate.Location?.Path ?? string.Empty)))
        .Where(predicate: path => !string.IsNullOrWhiteSpace(value: path))
        .Distinct(comparer: StringComparer.OrdinalIgnoreCase)
        .ToArray();

    private static void SetChildParents(
        Typewriter.CodeModel.Item parent,
        IEnumerable<Typewriter.CodeModel.Item> children)
    {
        foreach (var child in children)
        {
            child.SetParent(parent: parent);
        }
    }

    private static T AttachChildren<T>(
        T item,
        Typewriter.CodeModel.IAttributeCollection attributes,
        Typewriter.CodeModel.DocComment? docComment,
        IReadOnlyList<IEnumerable<Typewriter.CodeModel.Item>>? childCollections = null)
        where T : Typewriter.CodeModel.Item
    {
        SetChildParents(parent: item, children: attributes);
        foreach (var attribute in attributes)
        {
            SetChildParents(parent: attribute, children: attribute.Arguments);
        }

        if (docComment is not null)
        {
            docComment.SetParent(parent: item);
            SetChildParents(parent: docComment, children: docComment.Parameters);
        }

        foreach (var children in childCollections ?? [])
        {
            SetChildParents(parent: item, children: children);
        }

        return item;
    }

    private SourceFileMetadata? FindSourceFile(
        string? filePath,
        Func<SourceFileMetadata, bool> containsDeclaration)
    {
        ArgumentNullException.ThrowIfNull(argument: containsDeclaration);

        // A partial declaration belongs to the file currently being rendered,
        // even when Roslyn reports another part as its primary location.
        if (_metadata.SourceFiles.Count == 1 && containsDeclaration.Invoke(arg: _metadata.SourceFiles[index: 0]))
        {
            return _metadata.SourceFiles[index: 0];
        }

        return filePath is not null && _sourceFilesByPath.TryGetValue(key: filePath, value: out var sourceFile)
            ? sourceFile
            : _sourceFilesByPath.Values.FirstOrDefault(predicate: containsDeclaration);
    }

    private CodeFile ResolveParentFile(
        SourceFileMetadata? sourceFile,
        string? filePath)
    {
        if (sourceFile is not null)
        {
            filePath = sourceFile.Path;
            if (_metadata.SourceFiles.Count == 1
                && _metadata.SourceFiles[index: 0].Path.Equals(value: filePath, comparisonType: StringComparison.OrdinalIgnoreCase))
            {
                return CreateFile(project: _metadata);
            }
        }

        if (string.IsNullOrWhiteSpace(value: filePath))
        {
            return CreateFile(project: _metadata);
        }

        if (_filesBySourcePath.TryGetValue(key: filePath, value: out var cachedFile))
        {
            return cachedFile;
        }

        var sourcePaths = GetProjectSourcePaths(project: _metadata);
        if (sourcePaths.Length == 1 && sourcePaths[0].Equals(value: filePath, comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            return CreateFile(project: _metadata);
        }

        sourceFile ??= new SourceFileMetadata(
            Path: filePath,
            Types: _metadataIndex.Metadata.Types
                .Where(predicate: type => GetPrimaryFilePath(type: type).Equals(value: filePath, comparisonType: StringComparison.OrdinalIgnoreCase)
                    || type.FileLocations.Contains(value: filePath, comparer: StringComparer.OrdinalIgnoreCase))
                .ToArray())
        {
            Delegates = _metadataIndex.Metadata.Delegates
                .Where(predicate: @delegate => string.Equals(a: @delegate.Location?.Path, b: filePath, comparisonType: StringComparison.OrdinalIgnoreCase))
                .ToArray(),
        };
        var project = _metadata with
        {
            SourceFiles = [sourceFile],
            Types = sourceFile.Types,
            Delegates = sourceFile.Delegates,
        };
        var file = CreateFileModel(project: project, filePath: filePath, attachParents: true);
        _filesBySourcePath[filePath] = file;
        return file;
    }

    private Typewriter.CodeModel.Item? ResolveTypeParent(TypeMetadata type) =>
        string.IsNullOrWhiteSpace(value: type.ContainingTypeFullName)
            ? ResolveParentFile(type: type)
            : CreateParentTypeItem(fullName: type.ContainingTypeFullName, item: type);

    private void SetTypeParent(
        Typewriter.CodeModel.Item item,
        TypeMetadata type,
        CodeType? declarationType = null)
    {
        _metadataByItem[item] = type;

        // Parent navigation is lazy so adapting a declaration does not recursively
        // construct the file that contains that same declaration.
        item.SetParentFactory(parentFactory: () => ResolveTypeParent(type: type));
        declarationType?.SetParentFactory(parentFactory: () => item.Parent);
    }

    private CodeFile CreateFileModel(
        ProjectMetadata project,
        string filePath,
        bool attachParents)
    {
        var rootTypes = project.Types
            .Where(predicate: static type => string.IsNullOrWhiteSpace(value: type.ContainingTypeFullName))
            .ToArray();

        var file = new CodeFile
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
            Delegates = new Typewriter.CodeModel.DelegateCollection(
                items: project.Delegates
                    .Where(predicate: @delegate => string.IsNullOrWhiteSpace(value: @delegate.ParentTypeFullName))
                    .Select(selector: @delegate => CreateDelegate(@delegate: @delegate, parent: null))),
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
        if (attachParents)
        {
            SetChildParents(parent: file, children: file.Classes);
            SetChildParents(parent: file, children: file.Records);
            SetChildParents(parent: file, children: file.Structs);
            SetChildParents(parent: file, children: file.Delegates);
            SetChildParents(parent: file, children: file.Interfaces);
            SetChildParents(parent: file, children: file.Enums);
            SetChildParents(parent: file, children: file.Types);
        }

        return file;
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
        var item = new CodeClass
        {
            AssemblyName = ResolveAssemblyName(assemblyName: type.AssemblyName),
            Name = type.Name,
            FullName = FormatGenericFullName(type: type),
            Namespace = type.Namespace,
            Attributes = CreateAttributes(attributes: type.Attributes, parent: null),
            BaseClass = CreateBaseClass(type: type, reference: reference),
            ContainingClass = CreateContainingClass(type: type),
            DocComment = CreateDocComment(docComment: type.DocComment, parent: null),
            Interfaces = CreateInterfaces(type: type, reference: reference),
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
        SetTypeParent(item: item, type: type, declarationType: classType);
        return AttachChildren(
            item: item,
            attributes: item.Attributes,
            docComment: item.DocComment,
            childCollections: [
                item.Constants, item.Delegates, item.Events, item.Fields, item.Methods,
                item.NestedClasses, item.NestedEnums, item.NestedInterfaces, item.NestedRecords, item.NestedStructs,
                item.Properties, item.StaticReadOnlyFields, item.TypeParameters,
            ]);
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
        var item = new CodeRecord
        {
            AssemblyName = ResolveAssemblyName(assemblyName: type.AssemblyName),
            Name = type.Name,
            FullName = FormatGenericFullName(type: type),
            Namespace = type.Namespace,
            Attributes = CreateAttributes(attributes: type.Attributes, parent: null),
            BaseRecord = CreateBaseRecord(type: type, reference: reference),
            ContainingRecord = CreateContainingRecord(type: type),
            DocComment = CreateDocComment(docComment: type.DocComment, parent: null),
            Interfaces = CreateInterfaces(type: type, reference: reference),
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
        SetTypeParent(item: item, type: type, declarationType: recordType);
        return AttachChildren(
            item: item,
            attributes: item.Attributes,
            docComment: item.DocComment,
            childCollections: [
                item.Constants, item.Delegates, item.Events, item.Fields, item.Methods,
                item.NestedClasses, item.NestedEnums, item.NestedInterfaces, item.NestedRecords, item.NestedStructs,
                item.Properties, item.StaticReadOnlyFields, item.TypeParameters,
            ]);
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
        var item = new CodeInterface
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
            Interfaces = CreateInterfaces(type: type, reference: reference),
            Methods = new Typewriter.CodeModel.MethodCollection(items: type.Methods.Select(selector: method => CreateMethod(method: method, parent: null))),
            NestedStructs = new Typewriter.CodeModel.StructCollection(items: type.NestedStructs.Select(selector: CreateStruct)),
            Properties = new Typewriter.CodeModel.PropertyCollection(items: type.Properties.Select(selector: property => CreateProperty(property: property, parent: null))),
            Type = interfaceType,
            TypeArguments = interfaceType.TypeArguments,
            TypeParameters = CreateTypeParameters(typeParameters: type.TypeParameters, parent: null),
        };
        SetTypeParent(item: item, type: type, declarationType: interfaceType);
        return AttachChildren(
            item: item,
            attributes: item.Attributes,
            docComment: item.DocComment,
            childCollections: [item.Events, item.Methods, item.NestedStructs, item.Properties, item.TypeParameters]);
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
        var item = new CodeStruct
        {
            AssemblyName = ResolveAssemblyName(assemblyName: type.AssemblyName),
            Name = type.Name,
            FullName = FormatGenericFullName(type: type),
            Namespace = type.Namespace,
            Attributes = CreateAttributes(attributes: type.Attributes, parent: null),
            ContainingClass = CreateContainingClass(type: type),
            ContainingStruct = CreateContainingStruct(type: type),
            DocComment = CreateDocComment(docComment: type.DocComment, parent: null),
            Interfaces = CreateInterfaces(type: type, reference: reference),
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
        SetTypeParent(item: item, type: type, declarationType: structType);
        return AttachChildren(
            item: item,
            attributes: item.Attributes,
            docComment: item.DocComment,
            childCollections: [
                item.Constants, item.Delegates, item.Events, item.Fields, item.Methods,
                item.NestedClasses, item.NestedEnums, item.NestedInterfaces, item.NestedRecords, item.NestedStructs,
                item.Properties, item.StaticReadOnlyFields, item.TypeParameters,
            ]);
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
        SetTypeParent(item: enumModel, type: type, declarationType: enumModel.Type);
        return AttachChildren(
            item: enumModel,
            attributes: enumModel.Attributes,
            docComment: enumModel.DocComment,
            childCollections: [enumModel.Values]);
    }

    private CodeType CreateType(TypeMetadata type)
    {
        var item = new CodeType
        {
            AssemblyName = ResolveAssemblyName(assemblyName: type.AssemblyName),
            Name = type.Name,
            OriginalName = type.Name,
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
            TypeArguments = new Typewriter.CodeModel.TypeCollection(items: type.TypeArguments.Select(selector: argument => CreateType(type: argument))),
            TypeParameters = CreateTypeParameters(typeParameters: type.TypeParameters, parent: null),
            DefaultValue = GetDefaultValue(type: type),
            Settings = _settings,
        };
        SetTypeParent(item: item, type: type);
        return AttachChildren(
            item: item,
            attributes: item.Attributes,
            docComment: item.DocComment,
            childCollections: [
                item.Constants, item.Delegates, item.Fields, item.Methods,
                item.NestedClasses, item.NestedEnums, item.NestedInterfaces, item.NestedRecords, item.NestedStructs,
                item.Properties, item.StaticReadOnlyFields, item.TypeParameters,
            ]);
    }

    private CodeType CreateType(
        TypeMetadataReference type,
        FrontendRuntimeTypeKind runtimeType = FrontendRuntimeTypeKind.Auto)
    {
        var typeArguments = new Typewriter.CodeModel.TypeCollection(
            items: type.TypeArguments.Select(selector: argument => CreateType(type: argument, runtimeType: runtimeType)));
        _ = TryResolveType(reference: type, metadata: out var typeMetadata);
        var fileLocations = ResolveFileLocations(type: type, metadata: typeMetadata);
        var mappedName = _typeMapper.Map(
            type: type,
            strictNull: _settings.StrictNullGeneration,
            dateMapping: _settings.GetDateMapping(),
            decimalType: _settings.DecimalTypeGeneration,
            guidType: _settings.GuidTypeGeneration,
            runtimeType: runtimeType);
        var isStruct = typeMetadata?.Kind == TypeMetadataKind.Struct;
        var item = new MappedCodeType(
            interfacesFactory: () => CreateInterfaces(reference: type, metadata: typeMetadata))
        {
            AssemblyName = ResolveAssemblyName(assemblyName: type.AssemblyName),
            Name = GetLegacyTypeName(mappedName: mappedName),
            FullName = type.FullName,
            Namespace = type.Namespace,
            Attributes = typeMetadata is null
                ? new Typewriter.CodeModel.AttributeCollection()
                : CreateTypeReferenceAttributes(attributes: typeMetadata.Attributes),
            ElementType = type.ElementType is null ? null : CreateType(type: type.ElementType, runtimeType: runtimeType),
            FileLocations = fileLocations,
            IsDate = type.IsDateLike,
            IsDefined = typeMetadata is not null || fileLocations.Count > 0,
            IsDictionary = type.IsDictionary,
            IsDynamic = IsDynamic(type: type),
            IsEnum = type.IsEnum,
            IsEnumerable = type.IsCollection,
            IsGuid = type.FullName.Equals(value: "System.Guid", comparisonType: StringComparison.Ordinal),
            IsGeneric = type.Arity > 0,
            IsNullable = type.IsNullable,
            IsPrimitive = IsPrimitiveType(type: type),
            IsStruct = isStruct,
            IsTask = IsTaskLike(fullName: type.FullName),
            IsTimeSpan = type.FullName.Equals(value: "System.TimeSpan", comparisonType: StringComparison.Ordinal),
            IsValueTuple = type.IsValueTuple,
            OriginalName = type.Name,
            TupleElements = new Typewriter.CodeModel.FieldCollection(items: type.TupleElements.Select(selector: field => CreateField(field: field, parent: null))),
            TypeArguments = typeArguments,
            DefaultValue = GetDefaultValue(type: type, runtimeType: runtimeType),
            Settings = _settings,
            UseResolvedDefault = _settings.DateLibraryGeneration != Typewriter.Configuration.DateLibrary.Legacy
                                 || runtimeType != FrontendRuntimeTypeKind.Auto,
        };
        if (typeMetadata is not null)
        {
            SetTypeParent(item: item, type: typeMetadata);
        }

        return AttachChildren(
            item: item,
            attributes: item.Attributes,
            docComment: item.DocComment,
            childCollections: [item.TupleElements]);
    }

    private bool TryResolveType(
        TypeMetadataReference reference,
        [NotNullWhen(returnValue: true)] out TypeMetadata? metadata)
    {
        return _metadataIndex.TryResolveType(reference: reference, metadata: out metadata);
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
        var item = new Typewriter.CodeModel.Property
        {
            AssemblyName = ResolveAssemblyName(assemblyName: property.AssemblyName),
            Name = property.Name,
            FullName = property.FullName,
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
            Type = CreateType(type: property.Type, runtimeType: FrontendRuntimeTypeResolver.Resolve(attributes: property.Attributes)),
            Value = property.Value ?? string.Empty,
        };
        _metadataByItem[item] = property;
        item.SetParentFactory(parentFactory: () => parent ?? CreateParentTypeItem(fullName: property.ParentTypeFullName, item: property));
        return AttachChildren(
            item: item,
            attributes: item.Attributes,
            docComment: item.DocComment,
            childCollections: [item.Parameters]);
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
        var item = new CodeMethod
        {
            AssemblyName = ResolveAssemblyName(assemblyName: method.AssemblyName),
            Name = method.Name,
            FullName = method.FullName,
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
        _metadataByItem[item] = method;
        item.SetParentFactory(parentFactory: () => parent ?? CreateParentTypeItem(fullName: method.ParentTypeFullName, item: method));
        return AttachChildren(
            item: item,
            attributes: item.Attributes,
            docComment: item.DocComment,
            childCollections: [item.Parameters, item.TypeParameters]);
    }

    private CodeParameter CreateParameter(
        ParameterMetadata parameter,
        Typewriter.CodeModel.Item? parent)
    {
        var item = new CodeParameter
        {
            AssemblyName = ResolveAssemblyName(assemblyName: parameter.AssemblyName),
            Name = parameter.Name,
            FullName = parameter.FullName,
            Attributes = CreateAttributes(attributes: parameter.Attributes, parent: null),
            DefaultValue = parameter.DefaultValue ?? string.Empty,
            HasDefaultValue = parameter.HasDefaultValue,
            Type = CreateType(type: parameter.Type, runtimeType: FrontendRuntimeTypeResolver.Resolve(attributes: parameter.Attributes)),
        };
        _metadataByItem[item] = parameter;
        item.SetParentFactory(
            parentFactory: () => parent
                ?? (Typewriter.CodeModel.Item?)CreateParentMethod(fullName: parameter.ParentMethodFullName)
                ?? CreateParentProperty(fullName: parameter.ParentPropertyFullName));
        return AttachChildren(item: item, attributes: item.Attributes, docComment: null);
    }

    private CodeConstant CreateConstant(
        ConstantMetadata constant,
        Typewriter.CodeModel.Item? parent)
    {
        var item = new CodeConstant
        {
            AssemblyName = ResolveAssemblyName(assemblyName: constant.AssemblyName),
            Name = constant.Name,
            FullName = constant.FullName,
            Attributes = CreateAttributes(attributes: constant.Attributes, parent: null),
            DocComment = CreateDocComment(docComment: constant.DocComment, parent: null),
            Type = CreateType(type: constant.Type),
            Value = constant.Value ?? string.Empty,
        };
        _metadataByItem[item] = constant;
        item.SetParentFactory(parentFactory: () => parent ?? CreateParentTypeItem(fullName: constant.ParentTypeFullName, item: constant));
        return AttachChildren(item: item, attributes: item.Attributes, docComment: item.DocComment);
    }

    private CodeField CreateField(
        FieldMetadata field,
        Typewriter.CodeModel.Item? parent)
    {
        var item = new CodeField
        {
            AssemblyName = ResolveAssemblyName(assemblyName: field.AssemblyName),
            Name = field.Name,
            FullName = field.FullName,
            Attributes = CreateAttributes(attributes: field.Attributes, parent: null),
            DocComment = CreateDocComment(docComment: field.DocComment, parent: null),
            Type = CreateType(type: field.Type, runtimeType: FrontendRuntimeTypeResolver.Resolve(attributes: field.Attributes)),
            Value = field.Value ?? string.Empty,
        };
        _metadataByItem[item] = field;
        item.SetParentFactory(parentFactory: () => parent ?? CreateParentTypeItem(fullName: field.ParentTypeFullName, item: field));
        return AttachChildren(item: item, attributes: item.Attributes, docComment: item.DocComment);
    }

    private CodeStaticReadOnlyField CreateStaticReadOnlyField(
        StaticReadOnlyFieldMetadata field,
        Typewriter.CodeModel.Item? parent)
    {
        var item = new CodeStaticReadOnlyField
        {
            AssemblyName = ResolveAssemblyName(assemblyName: field.AssemblyName),
            Name = field.Name,
            FullName = field.FullName,
            Attributes = CreateAttributes(attributes: field.Attributes, parent: null),
            DocComment = CreateDocComment(docComment: field.DocComment, parent: null),
            Type = CreateType(type: field.Type),
            Value = field.Value ?? string.Empty,
        };
        _metadataByItem[item] = field;
        item.SetParentFactory(parentFactory: () => parent ?? CreateParentTypeItem(fullName: field.ParentTypeFullName, item: field));
        return AttachChildren(item: item, attributes: item.Attributes, docComment: item.DocComment);
    }

    private CodeEvent CreateEvent(
        EventMetadata @event,
        Typewriter.CodeModel.Item? parent)
    {
        var item = new CodeEvent
        {
            AssemblyName = ResolveAssemblyName(assemblyName: @event.AssemblyName),
            Name = @event.Name,
            FullName = @event.FullName,
            Attributes = CreateAttributes(attributes: @event.Attributes, parent: null),
            DocComment = CreateDocComment(docComment: @event.DocComment, parent: null),
            Type = CreateType(type: @event.Type),
        };
        _metadataByItem[item] = @event;
        item.SetParentFactory(parentFactory: () => parent ?? CreateParentTypeItem(fullName: @event.ParentTypeFullName, item: @event));
        return AttachChildren(item: item, attributes: item.Attributes, docComment: item.DocComment);
    }

    private CodeDelegate CreateDelegate(
        DelegateMetadata @delegate,
        Typewriter.CodeModel.Item? parent)
    {
        var item = new CodeDelegate
        {
            AssemblyName = ResolveAssemblyName(assemblyName: @delegate.AssemblyName),
            Name = @delegate.Name,
            FullName = @delegate.FullName,
            Attributes = CreateAttributes(attributes: @delegate.Attributes, parent: null),
            DocComment = CreateDocComment(docComment: @delegate.DocComment, parent: null),
            IsGeneric = @delegate.IsGeneric,
            Parameters = new Typewriter.CodeModel.ParameterCollection(items: @delegate.Parameters.Select(selector: parameter => CreateParameter(parameter: parameter, parent: null))),
            Type = CreateType(type: @delegate.ReturnType),
            TypeParameters = CreateTypeParameters(typeParameters: @delegate.TypeParameters, parent: null),
        };
        _metadataByItem[item] = @delegate;
        item.SetParentFactory(
            parentFactory: () => parent
                ?? (string.IsNullOrWhiteSpace(value: @delegate.ParentTypeFullName)
                    ? ResolveParentFile(@delegate: @delegate)
                    : CreateParentTypeItem(fullName: @delegate.ParentTypeFullName, item: @delegate)));
        return AttachChildren(
            item: item,
            attributes: item.Attributes,
            docComment: item.DocComment,
            childCollections: [item.Parameters, item.TypeParameters]);
    }

    private CodeAttribute CreateAttribute(
        AttributeMetadata attribute,
        Typewriter.CodeModel.Item? parent)
    {
        var item = new CodeAttribute
        {
            AssemblyName = ResolveAssemblyName(assemblyName: attribute.AssemblyName),
            Name = attribute.Name,
            FullName = attribute.FullName,
            Parent = parent,
            Arguments = new Typewriter.CodeModel.AttributeArgumentCollection(items: attribute.Arguments.Select(selector: CreateAttributeArgument)),
            Type = attribute.Type is null ? null : CreateType(type: attribute.Type),
            Value = TemplateAttributeValueFormatter.Format(attribute: attribute),
        };
        _metadataByItem[item] = attribute;
        SetChildParents(parent: item, children: item.Arguments);
        return item;
    }

    private Typewriter.CodeModel.AttributeArgument CreateAttributeArgument(AttributeArgumentMetadata argument)
    {
        return new Typewriter.CodeModel.AttributeArgument
        {
            AssemblyName = ResolveAssemblyName(assemblyName: argument.AssemblyName),
            Name = argument.Name ?? string.Empty,
            Type = argument.Type is null ? null : CreateType(type: argument.Type),
            TypeValue = argument.TypeValue is null ? null : CreateType(type: argument.TypeValue),
            Value = TemplateAttributeArgumentValueResolver.Resolve(argument: argument),
        };
    }

    private Typewriter.CodeModel.AttributeCollection CreateTypeReferenceAttributes(
        IEnumerable<AttributeMetadata> attributes)
    {
        return new Typewriter.CodeModel.AttributeCollection(
            items: attributes.Select(
                selector: attribute => new CodeAttribute
                {
                    AssemblyName = ResolveAssemblyName(assemblyName: attribute.AssemblyName),
                    Name = attribute.Name,
                    FullName = attribute.FullName,
                    Arguments = new Typewriter.CodeModel.AttributeArgumentCollection(
                        items: attribute.Arguments.Select(
                            selector: argument => new Typewriter.CodeModel.AttributeArgument
                            {
                                AssemblyName = ResolveAssemblyName(assemblyName: argument.AssemblyName),
                                Name = argument.Name ?? string.Empty,
                                Value = TemplateAttributeArgumentValueResolver.Resolve(argument: argument),
                            })),
                    Value = TemplateAttributeValueFormatter.Format(attribute: attribute),
                }));
    }

    private Typewriter.CodeModel.EnumValue CreateEnumValue(
        EnumValueMetadata enumValue,
        CodeEnum? parent)
    {
        var item = new Typewriter.CodeModel.EnumValue
        {
            AssemblyName = ResolveAssemblyName(assemblyName: enumValue.AssemblyName),
            Name = enumValue.Name,
            FullName = string.IsNullOrWhiteSpace(value: enumValue.ParentTypeFullName)
                ? enumValue.Name
                : string.Concat(str0: enumValue.ParentTypeFullName, str1: ".", str2: enumValue.Name),
            Attributes = CreateAttributes(attributes: enumValue.Attributes, parent: null),
            DocComment = CreateDocComment(docComment: enumValue.DocComment, parent: null),
            Value = enumValue.Value ?? 0,
        };
        _metadataByItem[item] = enumValue;
        item.SetParentFactory(parentFactory: () => parent ?? CreateParentEnum(fullName: enumValue.ParentTypeFullName, item: enumValue));
        return AttachChildren(item: item, attributes: item.Attributes, docComment: item.DocComment);
    }

    private Typewriter.CodeModel.InterfaceCollection CreateInterfaces(
        TypeMetadataReference reference,
        TypeMetadata? metadata)
    {
        if (reference.Interfaces.Count > 0)
        {
            return new Typewriter.CodeModel.InterfaceCollection(items: reference.Interfaces.Select(selector: CreateInterfaceReference));
        }

        return metadata is null
            ? new Typewriter.CodeModel.InterfaceCollection()
            : CreateInterfaces(type: metadata, reference: reference);
    }

    private bool IsInterfaceReference(TypeMetadataReference reference)
    {
        if (TryResolveType(reference: reference, metadata: out var metadata))
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
        if (TryResolveType(reference: reference, metadata: out var metadata)
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

    private Typewriter.CodeModel.Item? CreateParentTypeItem(
        string fullName,
        object item)
    {
        if (!_metadataIndex.TryResolveParentType(fullName: fullName, item: item, metadata: out var type))
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
                ? CreateMethod(method: method, parent: CreateParentTypeItem(fullName: method.ParentTypeFullName, item: method), includeParameters: false)
                : null;
    }

    private Typewriter.CodeModel.Property? CreateParentProperty(string fullName)
    {
        return !string.IsNullOrWhiteSpace(value: fullName)
            && _propertiesByFullName.TryGetValue(key: fullName, value: out var property)
                ? CreateProperty(property: property, parent: CreateParentTypeItem(fullName: property.ParentTypeFullName, item: property), includeParameters: false)
                : null;
    }

    private CodeEnum? CreateParentEnum(
        string fullName,
        object item)
    {
        return _metadataIndex.TryResolveParentType(fullName: fullName, item: item, metadata: out var type)
            && type.Kind == TypeMetadataKind.Enum
                ? CreateShallowEnum(type: type)
                : null;
    }

    private CodeClass? CreateContainingClass(TypeMetadata type)
    {
        return _metadataIndex.TryResolveParentType(fullName: type.ContainingTypeFullName, item: type, metadata: out var containingType)
            && containingType.Kind == TypeMetadataKind.Class
                ? CreateShallowClass(type: containingType)
                : null;
    }

    private CodeRecord? CreateContainingRecord(TypeMetadata type)
    {
        return _metadataIndex.TryResolveParentType(fullName: type.ContainingTypeFullName, item: type, metadata: out var containingType)
            && containingType.Kind == TypeMetadataKind.Record
                ? CreateShallowRecord(type: containingType)
                : null;
    }

    private CodeStruct? CreateContainingStruct(TypeMetadata type)
    {
        return _metadataIndex.TryResolveParentType(fullName: type.ContainingTypeFullName, item: type, metadata: out var containingType)
            && containingType.Kind == TypeMetadataKind.Struct
                ? CreateShallowStruct(type: containingType)
                : null;
    }

    private CodeClass CreateShallowClass(TypeMetadata type)
    {
        var item = new CodeClass
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
            TypeArguments = new Typewriter.CodeModel.TypeCollection(items: type.TypeArguments.Select(selector: argument => CreateType(type: argument))),
            TypeParameters = CreateTypeParameters(typeParameters: type.TypeParameters, parent: null),
        };
        SetTypeParent(item: item, type: type, declarationType: item.Type);
        return AttachChildren(item: item, attributes: item.Attributes, docComment: item.DocComment, childCollections: [item.TypeParameters]);
    }

    private CodeRecord CreateShallowRecord(TypeMetadata type)
    {
        var item = new CodeRecord
        {
            AssemblyName = ResolveAssemblyName(assemblyName: type.AssemblyName),
            Name = type.Name,
            FullName = FormatGenericFullName(type: type),
            Namespace = type.Namespace,
            Attributes = CreateAttributes(attributes: type.Attributes, parent: null),
            IsAbstract = type.IsAbstract,
            IsGeneric = type.TypeParameters.Count > 0 || type.TypeArguments.Count > 0,
            Type = CreateTypeShell(type: type),
            TypeArguments = new Typewriter.CodeModel.TypeCollection(items: type.TypeArguments.Select(selector: argument => CreateType(type: argument))),
            TypeParameters = CreateTypeParameters(typeParameters: type.TypeParameters, parent: null),
        };
        SetTypeParent(item: item, type: type, declarationType: item.Type);
        return AttachChildren(item: item, attributes: item.Attributes, docComment: item.DocComment, childCollections: [item.TypeParameters]);
    }

    private CodeStruct CreateShallowStruct(TypeMetadata type)
    {
        var item = new CodeStruct
        {
            AssemblyName = ResolveAssemblyName(assemblyName: type.AssemblyName),
            Name = type.Name,
            FullName = FormatGenericFullName(type: type),
            Namespace = type.Namespace,
            Attributes = CreateAttributes(attributes: type.Attributes, parent: null),
            IsGeneric = type.TypeParameters.Count > 0 || type.TypeArguments.Count > 0,
            IsStatic = type.IsStatic,
            Type = CreateTypeShell(type: type),
            TypeArguments = new Typewriter.CodeModel.TypeCollection(items: type.TypeArguments.Select(selector: argument => CreateType(type: argument))),
            TypeParameters = CreateTypeParameters(typeParameters: type.TypeParameters, parent: null),
        };
        SetTypeParent(item: item, type: type, declarationType: item.Type);
        return AttachChildren(item: item, attributes: item.Attributes, docComment: item.DocComment, childCollections: [item.TypeParameters]);
    }

    private CodeInterface CreateShallowInterface(TypeMetadata type)
    {
        var item = new CodeInterface
        {
            AssemblyName = ResolveAssemblyName(assemblyName: type.AssemblyName),
            Name = type.Name,
            FullName = FormatGenericFullName(type: type),
            Namespace = type.Namespace,
            Attributes = CreateAttributes(attributes: type.Attributes, parent: null),
            IsGeneric = type.TypeParameters.Count > 0 || type.TypeArguments.Count > 0,
            Type = CreateTypeShell(type: type),
            TypeArguments = new Typewriter.CodeModel.TypeCollection(items: type.TypeArguments.Select(selector: argument => CreateType(type: argument))),
            TypeParameters = CreateTypeParameters(typeParameters: type.TypeParameters, parent: null),
        };
        SetTypeParent(item: item, type: type, declarationType: item.Type);
        return AttachChildren(item: item, attributes: item.Attributes, docComment: item.DocComment, childCollections: [item.TypeParameters]);
    }

    private CodeEnum CreateShallowEnum(TypeMetadata type)
    {
        var item = new CodeEnum
        {
            AssemblyName = ResolveAssemblyName(assemblyName: type.AssemblyName),
            Name = type.Name,
            FullName = FormatGenericFullName(type: type),
            Namespace = type.Namespace,
            Attributes = CreateAttributes(attributes: type.Attributes, parent: null),
            IsFlags = HasAttribute(attributes: type.Attributes, name: "Flags"),
            Type = CreateTypeShell(type: type),
        };
        SetTypeParent(item: item, type: type, declarationType: item.Type);
        return AttachChildren(item: item, attributes: item.Attributes, docComment: item.DocComment);
    }

    private CodeType CreateTypeShell(TypeMetadata type)
    {
        var item = new CodeType
        {
            AssemblyName = ResolveAssemblyName(assemblyName: type.AssemblyName),
            Name = type.Name,
            OriginalName = type.Name,
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
            TypeArguments = new Typewriter.CodeModel.TypeCollection(items: type.TypeArguments.Select(selector: argument => CreateType(type: argument))),
            TypeParameters = CreateTypeParameters(typeParameters: type.TypeParameters, parent: null),
        };
        SetTypeParent(item: item, type: type);
        return AttachChildren(item: item, attributes: item.Attributes, docComment: item.DocComment, childCollections: [item.TypeParameters]);
    }

    private CodeClass? CreateBaseClass(
        TypeMetadata type,
        TypeMetadataReference? reference = null)
    {
        var baseType = type.BaseTypes.FirstOrDefault(predicate: candidate => !IsInterfaceReference(reference: candidate));
        if (baseType is null)
        {
            return null;
        }

        baseType = SubstituteTypeArguments(type: type, reference: reference, target: baseType);

        return TryResolveType(reference: baseType, metadata: out var baseMetadata)
            && baseMetadata.Kind == TypeMetadataKind.Class
                ? CreateClass(type: baseMetadata, reference: baseType)
                : new CodeClass
                {
                    AssemblyName = ResolveAssemblyName(assemblyName: baseType.AssemblyName),
                    Name = baseType.Name,
                    FullName = baseType.FullName,
                    Namespace = baseType.Namespace,
                    IsGeneric = baseType.Arity > 0,
                    Type = CreateType(type: baseType),
                    TypeArguments = new Typewriter.CodeModel.TypeCollection(items: baseType.TypeArguments.Select(selector: argument => CreateType(type: argument))),
                };
    }

    private CodeRecord? CreateBaseRecord(
        TypeMetadata type,
        TypeMetadataReference? reference = null)
    {
        var baseType = type.BaseTypes.FirstOrDefault(predicate: IsRecordBaseReference);
        if (baseType is null)
        {
            return null;
        }

        baseType = SubstituteTypeArguments(type: type, reference: reference, target: baseType);

        return TryResolveType(reference: baseType, metadata: out var baseMetadata)
            && baseMetadata.Kind == TypeMetadataKind.Record
                ? CreateRecord(type: baseMetadata, reference: baseType)
                : new CodeRecord
                {
                    AssemblyName = ResolveAssemblyName(assemblyName: baseType.AssemblyName),
                    Name = baseType.Name,
                    FullName = baseType.FullName,
                    Namespace = baseType.Namespace,
                    IsGeneric = baseType.Arity > 0,
                    Type = CreateType(type: baseType),
                    TypeArguments = new Typewriter.CodeModel.TypeCollection(items: baseType.TypeArguments.Select(selector: argument => CreateType(type: argument))),
                };
    }

    private bool IsRecordBaseReference(TypeMetadataReference reference)
    {
        if (TryResolveType(reference: reference, metadata: out var metadata))
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

    private string GetDefaultValue(
        TypeMetadataReference type,
        FrontendRuntimeTypeKind runtimeType = FrontendRuntimeTypeKind.Auto)
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
        var overriddenDefault = GetRuntimeOverrideDefault(runtimeType: runtimeType, stringLiteralCharacter: stringLiteralCharacter);
        if (overriddenDefault is not null)
        {
            return overriddenDefault;
        }

        if ((_settings.DateLibraryGeneration != Typewriter.Configuration.DateLibrary.Legacy
             || runtimeType != FrontendRuntimeTypeKind.Auto)
            && DateSemanticTypeResolver.Resolve(type: type, runtimeType: runtimeType) is { } semanticKind)
        {
            return _settings.GetDateInitializer(kind: semanticKind);
        }

        if (TryGetSpecialDefault(type: type, stringLiteralCharacter: stringLiteralCharacter, value: out var specialDefault))
        {
            return specialDefault;
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

    private string? GetRuntimeOverrideDefault(
        FrontendRuntimeTypeKind runtimeType,
        char stringLiteralCharacter) =>
        runtimeType switch
        {
            FrontendRuntimeTypeKind.Decimal => ScalarInitializer.ResolveDecimal(
                decimalType: _settings.DecimalTypeGeneration,
                decimalInitializer: _settings.DecimalInitializerGeneration),
            FrontendRuntimeTypeKind.Uuid => ScalarInitializer.ResolveGuid(
                guidType: _settings.GuidTypeGeneration,
                guidInitializer: _settings.GuidInitializerGeneration,
                stringLiteralCharacter: stringLiteralCharacter),
            FrontendRuntimeTypeKind.String => $"{stringLiteralCharacter}{stringLiteralCharacter}",
            _ => null,
        };

    private bool TryGetSpecialDefault(
        TypeMetadataReference type,
        char stringLiteralCharacter,
        [NotNullWhen(returnValue: true)] out string? value)
    {
        if (type.FullName.Equals(value: "System.Guid", comparisonType: StringComparison.Ordinal))
        {
            value = ScalarInitializer.ResolveGuid(
                guidType: _settings.GuidTypeGeneration,
                guidInitializer: _settings.GuidInitializerGeneration,
                stringLiteralCharacter: stringLiteralCharacter);
            return true;
        }

        if (type.FullName.Equals(value: "System.TimeSpan", comparisonType: StringComparison.Ordinal))
        {
            value = $"{stringLiteralCharacter}00:00:00{stringLiteralCharacter}";
            return true;
        }

        if (TypeScriptTemporalTypes.IsDateOnly(fullName: type.FullName))
        {
            value = _settings.DateOnlyInitializerGeneration;
            return true;
        }

        if (TypeScriptTemporalTypes.IsTimeOnly(fullName: type.FullName))
        {
            value = TypeScriptTemporalTypes.FormatTimeOnlyInitializer(
                initializer: _settings.TimeOnlyInitializerGeneration,
                stringLiteralCharacter: stringLiteralCharacter);
            return true;
        }

        value = GetConfiguredNonStringDefault(type: type);
        return value is not null;
    }

    private string? GetConfiguredNonStringDefault(TypeMetadataReference type)
    {
        if (type.FullName.Equals(value: "System.Decimal", comparisonType: StringComparison.Ordinal))
        {
            return ScalarInitializer.ResolveDecimal(
                decimalType: _settings.DecimalTypeGeneration,
                decimalInitializer: _settings.DecimalInitializerGeneration);
        }

        return type.IsDateLike || TypeScriptTemporalTypes.IsDateTime(fullName: type.FullName)
            ? _settings.DateInitializerGeneration
            : null;
    }

    private string? ResolveEnumDefault(TypeMetadataReference type)
    {
        if (type.EnumValues.Count > 0)
        {
            return ResolveEnumDefault(enumName: type.Name, enumValues: type.EnumValues);
        }

        return TryResolveType(reference: type, metadata: out var enumMetadata)
            ? ResolveEnumDefault(enumName: type.Name, enumValues: enumMetadata.EnumValues)
            : null;
    }

    private sealed class LazySubstitutedTypeReferenceList(
        IReadOnlyList<TypeMetadataReference> source,
        IReadOnlyDictionary<string, TypeMetadataReference> substitutions)
        : IReadOnlyList<TypeMetadataReference>
    {
        private readonly Lazy<IReadOnlyList<TypeMetadataReference>> _items = new(
            valueFactory: () => source.Count == 0
                ? []
                : source
                    .Select(selector: item => SubstituteTypeArguments(target: item, substitutions: substitutions))
                    .ToArray());

        public int Count => _items.Value.Count;

        public TypeMetadataReference this[int index] => _items.Value[index];

        public IEnumerator<TypeMetadataReference> GetEnumerator() => _items.Value.GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
