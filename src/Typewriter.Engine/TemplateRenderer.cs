using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using Typewriter.Abstractions;
using CodeModelAttribute = Typewriter.CodeModel.Attribute;
using CodeModelAttributeArgument = Typewriter.CodeModel.AttributeArgument;
using CodeModelClass = Typewriter.CodeModel.Class;
using CodeModelConstant = Typewriter.CodeModel.Constant;
using CodeModelDelegate = Typewriter.CodeModel.Delegate;
using CodeModelDocComment = Typewriter.CodeModel.DocComment;
using CodeModelEnum = Typewriter.CodeModel.Enum;
using CodeModelEnumValue = Typewriter.CodeModel.EnumValue;
using CodeModelEvent = Typewriter.CodeModel.Event;
using CodeModelField = Typewriter.CodeModel.Field;
using CodeModelInterface = Typewriter.CodeModel.Interface;
using CodeModelItem = Typewriter.CodeModel.Item;
using CodeModelMethod = Typewriter.CodeModel.Method;
using CodeModelParameter = Typewriter.CodeModel.Parameter;
using CodeModelParameterComment = Typewriter.CodeModel.ParameterComment;
using CodeModelProperty = Typewriter.CodeModel.Property;
using CodeModelRecord = Typewriter.CodeModel.Record;
using CodeModelStaticReadOnlyField = Typewriter.CodeModel.StaticReadOnlyField;
using CodeModelStruct = Typewriter.CodeModel.Struct;
using CodeModelType = Typewriter.CodeModel.Type;
using CodeModelTypeParameter = Typewriter.CodeModel.TypeParameter;
using NameCase = Typewriter.CodeModel.NameCase;

namespace Typewriter.Engine;

public sealed class TemplateRenderer
{
    private readonly TypeScriptTypeMapper _typeMapper;

    public TemplateRenderer(TypeScriptTypeMapper typeMapper)
    {
        _typeMapper = typeMapper;
    }

    public string Render(
        TemplateDocument template,
        ProjectMetadata metadata,
        ICollection<GenerationDiagnostic> diagnostics,
        TemplateRenderDefaults? defaults = null) =>
        RenderTemplate(template: template, metadata: metadata, diagnostics: diagnostics, defaults: defaults).Content;

    internal TemplateRenderResult RenderTemplate(
        TemplateDocument template,
        ProjectMetadata metadata,
        ICollection<GenerationDiagnostic> diagnostics,
        TemplateRenderDefaults? defaults = null,
        CompiledTemplateFactory? compiledTemplateFactory = null,
        ProjectMetadataIndex? metadataIndex = null)
    {
        ArgumentNullException.ThrowIfNull(argument: template);
        ArgumentNullException.ThrowIfNull(argument: metadata);
        ArgumentNullException.ThrowIfNull(argument: diagnostics);

        using var state = new RenderState(
            template: template,
            metadata: metadata,
            diagnostics: diagnostics,
            defaults: defaults ?? TemplateRenderDefaults.Default,
            compiledTemplateFactory: compiledTemplateFactory,
            metadataIndex: metadataIndex);
        var content = NormalizeRenderedWhitespace(value: RenderCore(template: template.Content, context: metadata, state: state));
        var settings = state.TemplateSettings;
        var outputPath = template.OutputPath ?? state.ResolveOutputPath();
        state.NotifyRenderComplete();
        return new TemplateRenderResult(
            Content: content,
            OutputPath: outputPath,
            UsesOutputFilenameFactory: state.UsesOutputFilenameFactory,
            RootItemCount: state.RootItemCount,
            IsSingleFileMode: settings?.IsSingleFileMode == true,
            OutputExtension: settings?.OutputExtension ?? ".ts",
            OutputDirectory: settings?.OutputDirectory,
            Utf8Bom: settings?.Utf8BomGeneration);
    }

#pragma warning disable SA1204
    internal static TemplateRenderInspection InspectTemplate(
        TemplateDocument template,
        ProjectMetadata metadata,
        ICollection<GenerationDiagnostic> diagnostics,
        TemplateRenderDefaults? defaults = null,
        CompiledTemplateFactory? compiledTemplateFactory = null,
        ProjectMetadataIndex? metadataIndex = null)
    {
        ArgumentNullException.ThrowIfNull(argument: template);
        ArgumentNullException.ThrowIfNull(argument: metadata);
        ArgumentNullException.ThrowIfNull(argument: diagnostics);

        using var state = new RenderState(
            template: template,
            metadata: metadata,
            diagnostics: diagnostics,
            defaults: defaults ?? TemplateRenderDefaults.Default,
            compiledTemplateFactory: compiledTemplateFactory,
            metadataIndex: metadataIndex);
        var settings = state.TemplateSettings;
        return new TemplateRenderInspection(
            IsSingleFileMode: settings?.IsSingleFileMode == true,
            UsesOutputFilenameFactory: state.UsesOutputFilenameFactory,
            IncludedProjects: settings?.IncludedProjects ?? []);
    }
#pragma warning restore SA1204

    private static string? FormatCollectionScalar(IEnumerable collection)
    {
        if (collection is Typewriter.CodeModel.IStringConvertible)
        {
            return collection.ToString() ?? string.Empty;
        }

        if (collection is IEnumerable<TypeParameterMetadata> typeParameters)
        {
            var items = typeParameters.ToArray();
            return items.Length == 0
                ? string.Empty
                : string.Concat(str0: "<", str1: string.Join(separator: ", ", values: items.Select(selector: item => item.Name)), str2: ">");
        }

        if (collection is IEnumerable<TypeMetadataReference> typeArguments)
        {
            var items = typeArguments.ToArray();
            return items.Length == 0
                ? string.Empty
                : string.Concat(str0: "<", str1: string.Join(separator: ", ", values: items.Select(selector: item => item.Name)), str2: ">");
        }

        var untypedItems = collection.Cast<object>().ToArray();
        return untypedItems.FirstOrDefault() switch
        {
            TypeParameterMetadata => string.Concat(str0: "<", str1: string.Join(separator: ", ", values: untypedItems.Cast<TypeParameterMetadata>().Select(selector: item => item.Name)), str2: ">"),
            TypeMetadataReference => string.Concat(str0: "<", str1: string.Join(separator: ", ", values: untypedItems.Cast<TypeMetadataReference>().Select(selector: item => item.Name)), str2: ">"),
            _ => null,
        };
    }

    private static bool IsRootItemCollection(string identifier)
    {
        return identifier is "Types"
            or "Classes"
            or "Records"
            or "Structs"
            or "Delegates"
            or "Interfaces"
            or "Enums";
    }

    private static bool MatchesRecipeTypePredicate(
        TypeMetadata type,
        string body)
    {
        var namespacePrefixes = ReadStartsWithLiterals(body: body, memberName: "Namespace").ToArray();
        if (namespacePrefixes.Length > 0
            && !namespacePrefixes.Any(predicate: prefix => type.Namespace.StartsWith(value: prefix, comparisonType: StringComparison.Ordinal)))
        {
            return false;
        }

        if (body.Contains(value: "GenerateFrontendType", comparisonType: StringComparison.Ordinal)
            && !HasAttribute(attributes: type.Attributes, name: "GenerateFrontendType"))
        {
            return false;
        }

        var baseType = type.BaseTypes.FirstOrDefault();
        if (body.Contains(value: "LogFileController", comparisonType: StringComparison.Ordinal)
            && type.FullName.Contains(value: "LogFileController", comparisonType: StringComparison.Ordinal))
        {
            return false;
        }

        if (!body.Contains(value: "ControllerBase", comparisonType: StringComparison.Ordinal)
            && !body.Contains(value: "Controller\"", comparisonType: StringComparison.Ordinal))
        {
            return true;
        }

        var hasControllerBase = baseType is not null
                                && (baseType.Name.EndsWith(value: "Controller", comparisonType: StringComparison.Ordinal)
                                    || baseType.Name.EndsWith(value: "ControllerBase", comparisonType: StringComparison.Ordinal));
        var requiresControllerBase = body.Contains(value: "parent == null", comparisonType: StringComparison.Ordinal)
                                     && body.Contains(value: "return true", comparisonType: StringComparison.Ordinal);

        return requiresControllerBase
            ? hasControllerBase
            : !hasControllerBase;
    }

    private static bool TryEvaluateLiteral(
        string expression,
        out object? value)
    {
        value = null;
        if (TryReadQuotedLiteral(value: expression, literal: out var literal))
        {
            value = literal;
            return true;
        }

        if (expression.Equals(value: "null", comparisonType: StringComparison.Ordinal))
        {
            return true;
        }

        if (expression.Equals(value: "true", comparisonType: StringComparison.Ordinal))
        {
            value = true;
            return true;
        }

        if (expression.Equals(value: "false", comparisonType: StringComparison.Ordinal))
        {
            value = false;
            return true;
        }

        if (expression.Equals(value: "string.Empty", comparisonType: StringComparison.Ordinal))
        {
            value = string.Empty;
            return true;
        }

        if (long.TryParse(s: expression, style: NumberStyles.Integer, provider: CultureInfo.InvariantCulture, result: out var number))
        {
            value = number;
            return true;
        }

        return false;
    }

#pragma warning disable S3776
    private static IEnumerable<string> SplitMemberPath(string path)
#pragma warning restore S3776
    {
        var start = 0;
        var depth = 0;
        var quote = '\0';
        for (var index = 0; index < path.Length; index++)
        {
            var current = path[index: index];
            if (quote != '\0')
            {
                if (current == quote && (index == 0 || path[index: index - 1] != '\\'))
                {
                    quote = '\0';
                }

                continue;
            }

            if (current is '"' or '\'')
            {
                quote = current;
                continue;
            }

            if (current is '(' or '[' or '{')
            {
                depth++;
                continue;
            }

            if (current is ')' or ']' or '}')
            {
                depth--;
                continue;
            }

            if (current == '.' && depth == 0)
            {
                yield return path[start..index];
                start = index + 1;
            }
        }

        if (start < path.Length)
        {
            yield return path[start..];
        }
    }

    private static bool TryParseLambda(
        string value,
        out string parameterName,
        out string expression)
    {
        parameterName = string.Empty;
        expression = string.Empty;
        var index = value.IndexOf(value: "=>", comparisonType: StringComparison.Ordinal);
        if (index <= 0)
        {
            return false;
        }

        parameterName = value[..index].Trim().Trim('(', ')', ' ');
        expression = value[(index + 2)..].Trim();
        return parameterName.Length > 0 && expression.Length > 0;
    }

    private static bool TryParseInstanceMethodCall(
        string expression,
        string methodName,
        out string target,
        out string arguments)
    {
        target = string.Empty;
        arguments = string.Empty;
        var marker = "." + methodName + "(";
        var index = expression.LastIndexOf(value: marker, comparisonType: StringComparison.Ordinal);
        if (index <= 0 || !expression.EndsWith(value: ')'))
        {
            return false;
        }

        target = expression[..index].Trim();
        arguments = expression[(index + marker.Length)..^1].Trim();
        return target.Length > 0;
    }

#pragma warning disable S3776
    private static bool TrySplitTopLevel(
        string expression,
        string operatorText,
        out string left,
        out string right)
#pragma warning restore S3776
    {
        left = string.Empty;
        right = string.Empty;
        var depth = 0;
        var quote = '\0';
        for (var index = 0; index <= expression.Length - operatorText.Length; index++)
        {
            var current = expression[index: index];
            if (quote != '\0')
            {
                if (current == quote && (index == 0 || expression[index: index - 1] != '\\'))
                {
                    quote = '\0';
                }

                continue;
            }

            if (current is '"' or '\'')
            {
                quote = current;
                continue;
            }

            if (current is '(' or '[' or '{')
            {
                depth++;
                continue;
            }

            if (current is ')' or ']' or '}')
            {
                depth--;
                continue;
            }

            if (depth != 0
                || !expression.AsSpan(start: index, length: operatorText.Length).SequenceEqual(other: operatorText.AsSpan()))
            {
                continue;
            }

            left = expression[..index].Trim();
            right = expression[(index + operatorText.Length)..].Trim();
            return left.Length > 0 && right.Length > 0;
        }

        return false;
    }

    private static string StripOuterParentheses(string expression)
    {
        var trimmed = expression.Trim();
        while (trimmed.Length > 1
            && trimmed[index: 0] == '('
            && FindClosingParenthesis(expression: trimmed, openIndex: 0) == trimmed.Length - 1)
        {
            trimmed = trimmed[1..^1].Trim();
        }

        return trimmed;
    }

#pragma warning disable S3776
    private static int FindClosingParenthesis(
        string expression,
        int openIndex)
#pragma warning restore S3776
    {
        var depth = 0;
        var quote = '\0';
        for (var index = openIndex; index < expression.Length; index++)
        {
            var current = expression[index: index];
            if (quote != '\0')
            {
                if (current == quote && (index == 0 || expression[index: index - 1] != '\\'))
                {
                    quote = '\0';
                }

                continue;
            }

            if (current is '"' or '\'')
            {
                quote = current;
                continue;
            }

            if (current == '(')
            {
                depth++;
            }
            else if (current == ')')
            {
                depth--;
                if (depth == 0)
                {
                    return index;
                }
            }
        }

        return -1;
    }

    private static bool TryReadFirstStringLiteral(
        string value,
        out string literal)
    {
        literal = string.Empty;
        foreach (var argument in SplitArguments(value: value))
        {
            if (TryReadQuotedLiteral(value: argument.Trim(), literal: out literal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryReadStringLiteralPair(
        string value,
        out string first,
        out string second)
    {
        first = string.Empty;
        second = string.Empty;
        var literals = SplitArguments(value: value)
            .Select(selector: argument => argument.Trim())
            .Where(predicate: argument => TryReadQuotedLiteral(value: argument, literal: out _))
            .Select(selector: argument =>
            {
                _ = TryReadQuotedLiteral(value: argument, literal: out var literal);
                return literal;
            })
            .ToArray();
        if (literals.Length < 2)
        {
            return false;
        }

        first = literals[0];
        second = literals[1];
        return true;
    }

#pragma warning disable S3776
    private static IEnumerable<string> SplitArguments(string value)
#pragma warning restore S3776
    {
        var start = 0;
        var depth = 0;
        var quote = '\0';
        for (var index = 0; index < value.Length; index++)
        {
            var current = value[index: index];
            if (quote != '\0')
            {
                if (current == quote && (index == 0 || value[index: index - 1] != '\\'))
                {
                    quote = '\0';
                }

                continue;
            }

            if (current is '"' or '\'')
            {
                quote = current;
                continue;
            }

            if (current is '(' or '[' or '{')
            {
                depth++;
                continue;
            }

            if (current is ')' or ']' or '}')
            {
                depth--;
                continue;
            }

            if (current == ',' && depth == 0)
            {
                yield return value[start..index];
                start = index + 1;
            }
        }

        yield return value[start..];
    }

    private static bool TryReadQuotedLiteral(
        string value,
        out string literal)
    {
        literal = string.Empty;
        var trimmed = value.Trim();
        if (trimmed.Length < 2)
        {
            return false;
        }

        if (trimmed[index: 0] == '@')
        {
            trimmed = trimmed[1..];
        }

        var quote = trimmed[index: 0];
        if (quote is not ('"' or '\'') || trimmed[^1] != quote)
        {
            return false;
        }

        literal = trimmed[1..^1]
            .Replace(oldValue: "\\\"", newValue: "\"", comparisonType: StringComparison.Ordinal)
            .Replace(oldValue: "\\'", newValue: "'", comparisonType: StringComparison.Ordinal)
            .Replace(oldValue: "\\\\", newValue: "\\", comparisonType: StringComparison.Ordinal);
        return true;
    }

    private static bool ValuesEqual(
        object? left,
        object? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        if (left is bool leftBool && right is bool rightBool)
        {
            return leftBool == rightBool;
        }

        return left.ToString()?.Equals(value: right.ToString(), comparisonType: StringComparison.Ordinal) == true;
    }

    private static IEnumerable<string> ReadStartsWithLiterals(
        string body,
        string memberName)
    {
        var marker = memberName + ".StartsWith(";
        var index = 0;
        while ((index = body.IndexOf(value: marker, startIndex: index, comparisonType: StringComparison.Ordinal)) >= 0)
        {
            var start = index + marker.Length;
            var end = body.IndexOf(value: ')', startIndex: start);
            if (end < 0)
            {
                yield break;
            }

            if (TryReadFirstStringLiteral(value: body[start..end], literal: out var literal))
            {
                yield return literal;
            }

            index = end + 1;
        }
    }

    private static bool HasAttribute(
        IEnumerable<AttributeMetadata> attributes,
        string name)
    {
        return attributes.Any(
            predicate: attribute => attribute.Name.Equals(value: name, comparisonType: StringComparison.Ordinal)
                                    || attribute.FullName.Equals(value: name, comparisonType: StringComparison.Ordinal)
                                    || attribute.FullName.EndsWith(value: "." + name + "Attribute", comparisonType: StringComparison.Ordinal)
                                    || attribute.Name.Equals(value: name + "Attribute", comparisonType: StringComparison.Ordinal));
    }

    private static string NormalizeLegacyMemberName(string segment)
    {
        return segment switch
        {
            "BaseClass" => "BaseType",
            "BaseRecord" => "BaseType",
            _ => segment,
        };
    }

    private static bool TryGetTypeReference(
        object? value,
        out TypeMetadataReference type)
    {
        switch (value)
        {
            case TypeMetadataReference typeReference:
                type = typeReference;
                return true;
            case TypeTemplateValue typeTemplateValue:
                type = typeTemplateValue.Reference;
                return true;
            case TypeMappingContext typeMappingContext:
                type = typeMappingContext.Reference;
                return true;
            case PropertyMetadata property:
                type = property.Type;
                return true;
            case MethodMetadata method:
                type = method.ReturnType;
                return true;
            case ParameterMetadata parameter:
                type = parameter.Type;
                return true;
            case ConstantMetadata constant:
                type = constant.Type;
                return true;
            case FieldMetadata field:
                type = field.Type;
                return true;
            case StaticReadOnlyFieldMetadata field:
                type = field.Type;
                return true;
            case EventMetadata @event:
                type = @event.Type;
                return true;
            case DelegateMetadata @delegate:
                type = @delegate.ReturnType;
                return true;
            default:
                type = null!;
                return false;
        }
    }

    private static string GetClassNameForDefault(TypeMetadataReference type)
    {
        var className = type.IsCollection && type.ElementType is not null
            ? type.ElementType.Name
            : type.Name;
        return className
            .Replace(oldValue: " | null", newValue: string.Empty, comparisonType: StringComparison.Ordinal)
            .Replace(oldValue: "(", newValue: string.Empty, comparisonType: StringComparison.Ordinal)
            .Replace(oldValue: ")", newValue: string.Empty, comparisonType: StringComparison.Ordinal)
            .TrimEnd('[', ']');
    }

    private static bool IsDynamicType(TypeMetadataReference type)
    {
        return type.FullName.Equals(value: "dynamic", comparisonType: StringComparison.OrdinalIgnoreCase)
            || type.FullName.Equals(value: "System.Object", comparisonType: StringComparison.Ordinal);
    }

    private static bool IsTaskLike(string fullName)
    {
        return fullName.Equals(value: "System.Threading.Tasks.Task", comparisonType: StringComparison.Ordinal)
            || fullName.Equals(value: "System.Threading.Tasks.ValueTask", comparisonType: StringComparison.Ordinal);
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

    private static bool HasAccessibility(
        object item,
        MetadataAccessibility accessibility)
    {
        return item switch
        {
            TypeMetadata type => type.Accessibility == accessibility,
            PropertyMetadata property => property.Accessibility == accessibility,
            MethodMetadata method => method.Accessibility == accessibility,
            ConstantMetadata constant => constant.Accessibility == accessibility,
            FieldMetadata field => field.Accessibility == accessibility,
            StaticReadOnlyFieldMetadata field => field.Accessibility == accessibility,
            EventMetadata @event => @event.Accessibility == accessibility,
            DelegateMetadata @delegate => @delegate.Accessibility == accessibility,
            _ => false,
        };
    }

    private static IEnumerable<string> GetItemSelectors(object item)
    {
        return item switch
        {
            TypeMetadata type => [type.Name, type.FullName, type.Namespace, type.Accessibility.ToString()],
            PropertyMetadata property => [property.Name, property.FullName, property.Accessibility.ToString()],
            MethodMetadata method => [method.Name, method.FullName, method.Accessibility.ToString()],
            ParameterMetadata parameter => [parameter.Name, parameter.FullName],
            ConstantMetadata constant => [constant.Name, constant.FullName, constant.Accessibility.ToString()],
            FieldMetadata field => [field.Name, field.FullName, field.Accessibility.ToString()],
            StaticReadOnlyFieldMetadata field => [field.Name, field.FullName, field.Accessibility.ToString()],
            EventMetadata @event => [@event.Name, @event.FullName, @event.Accessibility.ToString()],
            DelegateMetadata @delegate => [@delegate.Name, @delegate.FullName, @delegate.Accessibility.ToString()],
            EnumValueMetadata enumValue => [enumValue.Name],
            AttributeMetadata attribute => [attribute.Name, attribute.FullName],
            ParameterCommentMetadata parameterComment => [parameterComment.Name],
            TypeParameterMetadata typeParameter => [typeParameter.Name, typeParameter.FullName],
            _ => [item.ToString() ?? string.Empty],
        };
    }

    private static IEnumerable<string> GetAttributeSelectors(object item)
    {
        return item switch
        {
            TypeMetadata type => type.Attributes.SelectMany(selector: attribute => new[] { attribute.Name, attribute.FullName }),
            PropertyMetadata property => property.Attributes.SelectMany(selector: attribute => new[] { attribute.Name, attribute.FullName }),
            MethodMetadata method => method.Attributes.SelectMany(selector: attribute => new[] { attribute.Name, attribute.FullName }),
            ParameterMetadata parameter => parameter.Attributes.SelectMany(selector: attribute => new[] { attribute.Name, attribute.FullName }),
            ConstantMetadata constant => constant.Attributes.SelectMany(selector: attribute => new[] { attribute.Name, attribute.FullName }),
            FieldMetadata field => field.Attributes.SelectMany(selector: attribute => new[] { attribute.Name, attribute.FullName }),
            StaticReadOnlyFieldMetadata field => field.Attributes.SelectMany(selector: attribute => new[] { attribute.Name, attribute.FullName }),
            EventMetadata @event => @event.Attributes.SelectMany(selector: attribute => new[] { attribute.Name, attribute.FullName }),
            DelegateMetadata @delegate => @delegate.Attributes.SelectMany(selector: attribute => new[] { attribute.Name, attribute.FullName }),
            EnumValueMetadata enumValue => enumValue.Attributes.SelectMany(selector: attribute => new[] { attribute.Name, attribute.FullName }),
            _ => [],
        };
    }

    private static IEnumerable<string> GetInheritanceSelectors(object item)
    {
        return item switch
        {
            TypeMetadata type => type.BaseTypes.SelectMany(selector: typeReference => new[] { typeReference.Name, typeReference.FullName }),
            _ => [],
        };
    }

    private static bool MatchesAny(
        IEnumerable<string> values,
        string pattern)
    {
        return values.Any(predicate: value => MatchesFilterPattern(value: value, pattern: pattern));
    }

    private static bool MatchesFilterPattern(
        string value,
        string pattern)
    {
        if (!pattern.Contains(value: '*', comparisonType: StringComparison.Ordinal))
        {
            return value.Equals(value: pattern, comparisonType: StringComparison.OrdinalIgnoreCase);
        }

        var parts = pattern.Split(separator: '*');
        var position = 0;
        foreach (var part in parts)
        {
            if (part.Length == 0)
            {
                continue;
            }

            var index = value.IndexOf(value: part, startIndex: position, comparisonType: StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                return false;
            }

            position = index + part.Length;
        }

        return (pattern.StartsWith(value: '*')
                || value.StartsWith(value: parts[0], comparisonType: StringComparison.OrdinalIgnoreCase))
            && (pattern.EndsWith(value: '*')
                || value.EndsWith(value: parts[^1], comparisonType: StringComparison.OrdinalIgnoreCase));
    }

    private static bool ResolveStrictNull(
        Typewriter.Configuration.Settings? settings,
        RenderState? state) =>
        settings?.StrictNullGeneration ?? state?.StrictNullGeneration ?? true;

    private static string ResolveDateInitializer(
        Typewriter.Configuration.Settings? settings,
        RenderState? state) =>
        settings?.DateInitializerGeneration ?? state?.DateInitializerGeneration ?? TypeScriptTypeMapper.DefaultDateInitializer;

    private static string ResolveDateOnlyInitializer(
        Typewriter.Configuration.Settings? settings,
        RenderState? state) =>
        settings?.DateOnlyInitializerGeneration ?? state?.DateOnlyInitializerGeneration ?? TypeScriptTypeMapper.DefaultDateOnlyInitializer;

    private static string ResolveTimeOnlyInitializer(
        Typewriter.Configuration.Settings? settings,
        RenderState? state) =>
        settings?.TimeOnlyInitializerGeneration ?? state?.TimeOnlyInitializerGeneration ?? TypeScriptTypeMapper.DefaultTimeOnlyInitializer;

    private static TypeScriptDateMapping ResolveDateMapping(
        Typewriter.Configuration.Settings? settings,
        RenderState? state) =>
        settings?.GetDateMapping()
        ?? state?.DateMapping
        ?? TypeScriptDateMapping.Legacy(
            dateType: TypeScriptTypeMapper.DefaultDateType,
            dateOnlyType: TypeScriptTypeMapper.DefaultDateOnlyType,
            timeOnlyType: TypeScriptTypeMapper.DefaultTimeOnlyType);

    private static string ResolveSemanticDateInitializer(
        DateSemanticKind kind,
        Typewriter.Configuration.Settings? settings,
        RenderState? state)
    {
        if (settings is not null)
        {
            return settings.GetDateInitializer(kind: kind);
        }

        return state?.GetDateInitializer(kind: kind)
               ?? kind switch
               {
                   DateSemanticKind.PlainDate => TypeScriptTypeMapper.DefaultDateOnlyInitializer,
                   DateSemanticKind.PlainTime => TypeScriptTypeMapper.DefaultTimeOnlyInitializer,
                   DateSemanticKind.Duration => "\"00:00:00\"",
                   DateSemanticKind.Period => "\"P0D\"",
                   DateSemanticKind.PlainYearMonth or DateSemanticKind.PlainMonthDay => "\"\"",
                   _ => TypeScriptTypeMapper.DefaultDateInitializer,
               };
    }

    private static string? GetRuntimeOverrideDefault(
        FrontendRuntimeTypeKind runtimeType,
        Typewriter.Configuration.Settings? settings,
        RenderState? state,
        char stringLiteralCharacter)
    {
        var guidType = ResolveGuidType(settings: settings, state: state);
        var guidInitializer = ResolveGuidInitializer(settings: settings, state: state);
        var decimalType = ResolveDecimalType(settings: settings, state: state);
        var decimalInitializer = ResolveDecimalInitializer(settings: settings, state: state);
        return runtimeType switch
        {
            FrontendRuntimeTypeKind.Decimal => ScalarInitializer.ResolveDecimal(
                decimalType: decimalType,
                decimalInitializer: decimalInitializer),
            FrontendRuntimeTypeKind.Uuid => ScalarInitializer.ResolveGuid(
                guidType: guidType,
                guidInitializer: guidInitializer,
                stringLiteralCharacter: stringLiteralCharacter),
            FrontendRuntimeTypeKind.String => $"{stringLiteralCharacter}{stringLiteralCharacter}",
            _ => null,
        };
    }

    private static string? GetSemanticDateDefault(
        TypeMetadataReference type,
        FrontendRuntimeTypeKind runtimeType,
        Typewriter.Configuration.Settings? settings,
        RenderState? state)
    {
        var dateMapping = ResolveDateMapping(settings: settings, state: state);
        return (dateMapping.Library != Typewriter.Configuration.DateLibrary.Legacy
                || runtimeType != FrontendRuntimeTypeKind.Auto)
               && DateSemanticTypeResolver.Resolve(type: type, runtimeType: runtimeType) is { } semanticKind
            ? ResolveSemanticDateInitializer(kind: semanticKind, settings: settings, state: state)
            : null;
    }

    private static string ResolveGuidType(
        Typewriter.Configuration.Settings? settings,
        RenderState? state) =>
        settings?.GuidTypeGeneration ?? state?.GuidTypeGeneration ?? TypeScriptTypeMapper.DefaultGuidType;

    private static string ResolveGuidInitializer(
        Typewriter.Configuration.Settings? settings,
        RenderState? state) =>
        settings?.GuidInitializerGeneration ?? state?.GuidInitializerGeneration ?? TypeScriptTypeMapper.DefaultGuidInitializer;

    private static string ResolveDecimalType(
        Typewriter.Configuration.Settings? settings,
        RenderState? state) =>
        settings?.DecimalTypeGeneration ?? state?.DecimalTypeGeneration ?? TypeScriptTypeMapper.DefaultDecimalType;

    private static string ResolveDecimalInitializer(
        Typewriter.Configuration.Settings? settings,
        RenderState? state) =>
        settings?.DecimalInitializerGeneration ?? state?.DecimalInitializerGeneration ?? TypeScriptTypeMapper.DefaultDecimalInitializer;

    private static object ResolveDocComment(
        DocCommentMetadata docComment,
        string identifier)
    {
        return identifier switch
        {
            "Summary" => docComment.Summary,
            "Returns" => docComment.Returns,
            "Parameters" => docComment.Parameters,
            "Text" => docComment.Summary,
            _ => Unresolved.Value,
        };
    }

    private static object ResolveParameterComment(
        ParameterCommentMetadata parameterComment,
        string identifier)
    {
        return identifier switch
        {
            "Name" => parameterComment.Name,
            "Description" => parameterComment.Description,
            _ => Unresolved.Value,
        };
    }

    private static object ResolveTypeParameter(
        TypeParameterMetadata typeParameter,
        string identifier)
    {
        return identifier switch
        {
            "Name" => typeParameter.Name,
            "name" => ToCamelCase(value: typeParameter.Name),
            "FullName" => typeParameter.FullName,
            _ => Unresolved.Value,
        };
    }

    private static object? ResolveCodeModelClass(
        CodeModelClass @class,
        string identifier)
    {
        return identifier switch
        {
            "Name" => @class.Name,
            "name" => @class.name,
            "FullName" => @class.FullName,
            "AssemblyName" => @class.AssemblyName,
            "Namespace" => @class.Namespace,
            "Attributes" => @class.Attributes,
            "BaseClass" => @class.BaseClass,
            "ContainingClass" => @class.ContainingClass,
            "Constants" => @class.Constants,
            "Delegates" => @class.Delegates,
            "DocComment" => @class.DocComment,
            "Events" => @class.Events,
            "Fields" => @class.Fields,
            "Interfaces" => @class.Interfaces,
            "Methods" => @class.Methods,
            "NestedClasses" => @class.NestedClasses,
            "NestedEnums" => @class.NestedEnums,
            "NestedInterfaces" => @class.NestedInterfaces,
            "NestedStructs" => @class.NestedStructs,
            "Parent" => @class.Parent,
            "Properties" => @class.Properties,
            "StaticReadOnlyFields" => @class.StaticReadOnlyFields,
            "Type" => @class.Type,
            "TypeArguments" => @class.TypeArguments,
            "TypeParameters" => @class.TypeParameters,
            "IsAbstract" => @class.IsAbstract,
            "IsGeneric" => @class.IsGeneric,
            "IsStatic" => @class.IsStatic,
            _ => Unresolved.Value,
        };
    }

    private static object? ResolveCodeModelRecord(
        CodeModelRecord record,
        string identifier)
    {
        return identifier switch
        {
            "Name" => record.Name,
            "name" => record.name,
            "FullName" => record.FullName,
            "AssemblyName" => record.AssemblyName,
            "Namespace" => record.Namespace,
            "Attributes" => record.Attributes,
            "BaseRecord" => record.BaseRecord,
            "ContainingRecord" => record.ContainingRecord,
            "Constants" => record.Constants,
            "Delegates" => record.Delegates,
            "DocComment" => record.DocComment,
            "Events" => record.Events,
            "Fields" => record.Fields,
            "Interfaces" => record.Interfaces,
            "Methods" => record.Methods,
            "NestedStructs" => record.NestedStructs,
            "Parent" => record.Parent,
            "Properties" => record.Properties,
            "StaticReadOnlyFields" => record.StaticReadOnlyFields,
            "Type" => record.Type,
            "TypeArguments" => record.TypeArguments,
            "TypeParameters" => record.TypeParameters,
            "IsAbstract" => record.IsAbstract,
            "IsGeneric" => record.IsGeneric,
            _ => Unresolved.Value,
        };
    }

    private static object? ResolveCodeModelStruct(
        CodeModelStruct @struct,
        string identifier)
    {
        return identifier switch
        {
            "Name" => @struct.Name,
            "name" => @struct.name,
            "FullName" => @struct.FullName,
            "AssemblyName" => @struct.AssemblyName,
            "Namespace" => @struct.Namespace,
            "Attributes" => @struct.Attributes,
            "ContainingClass" => @struct.ContainingClass,
            "ContainingStruct" => @struct.ContainingStruct,
            "Constants" => @struct.Constants,
            "Delegates" => @struct.Delegates,
            "DocComment" => @struct.DocComment,
            "Events" => @struct.Events,
            "Fields" => @struct.Fields,
            "Interfaces" => @struct.Interfaces,
            "Methods" => @struct.Methods,
            "NestedClasses" => @struct.NestedClasses,
            "NestedEnums" => @struct.NestedEnums,
            "NestedInterfaces" => @struct.NestedInterfaces,
            "NestedRecords" => @struct.NestedRecords,
            "NestedStructs" => @struct.NestedStructs,
            "Parent" => @struct.Parent,
            "Properties" => @struct.Properties,
            "StaticReadOnlyFields" => @struct.StaticReadOnlyFields,
            "Type" => @struct.Type,
            "TypeArguments" => @struct.TypeArguments,
            "TypeParameters" => @struct.TypeParameters,
            "IsGeneric" => @struct.IsGeneric,
            "IsStatic" => @struct.IsStatic,
            _ => Unresolved.Value,
        };
    }

    private static object? ResolveCodeModelInterface(
        CodeModelInterface @interface,
        string identifier)
    {
        return identifier switch
        {
            "Name" => @interface.Name,
            "name" => @interface.name,
            "FullName" => @interface.FullName,
            "AssemblyName" => @interface.AssemblyName,
            "Namespace" => @interface.Namespace,
            "Attributes" => @interface.Attributes,
            "ContainingClass" => @interface.ContainingClass,
            "DocComment" => @interface.DocComment,
            "Events" => @interface.Events,
            "Interfaces" => @interface.Interfaces,
            "Methods" => @interface.Methods,
            "NestedStructs" => @interface.NestedStructs,
            "Parent" => @interface.Parent,
            "Properties" => @interface.Properties,
            "Type" => @interface.Type,
            "TypeArguments" => @interface.TypeArguments,
            "TypeParameters" => @interface.TypeParameters,
            "IsGeneric" => @interface.IsGeneric,
            _ => Unresolved.Value,
        };
    }

    private static object? ResolveCodeModelEnum(
        CodeModelEnum @enum,
        string identifier)
    {
        return identifier switch
        {
            "Name" => @enum.Name,
            "name" => @enum.name,
            "FullName" => @enum.FullName,
            "AssemblyName" => @enum.AssemblyName,
            "Namespace" => @enum.Namespace,
            "Attributes" => @enum.Attributes,
            "ContainingClass" => @enum.ContainingClass,
            "DocComment" => @enum.DocComment,
            "IsFlags" => @enum.IsFlags,
            "Parent" => @enum.Parent,
            "Type" => @enum.Type,
            "Values" => @enum.Values,
            _ => Unresolved.Value,
        };
    }

    private static object? ResolveCodeModelDelegate(
        CodeModelDelegate @delegate,
        string identifier)
    {
        return identifier switch
        {
            "Name" => @delegate.Name,
            "name" => @delegate.name,
            "FullName" => @delegate.FullName,
            "AssemblyName" => @delegate.AssemblyName,
            "Attributes" => @delegate.Attributes,
            "DocComment" => @delegate.DocComment,
            "IsGeneric" => @delegate.IsGeneric,
            "Parameters" => @delegate.Parameters,
            "Parent" => @delegate.Parent,
            "Type" => @delegate.Type,
            "ReturnType" => @delegate.Type,
            "TypeParameters" => @delegate.TypeParameters,
            _ => Unresolved.Value,
        };
    }

    private static object? ResolveCodeModelField(
        CodeModelField field,
        string identifier)
    {
        return identifier switch
        {
            "Name" => field.Name,
            "name" => field.name,
            "FullName" => field.FullName,
            "AssemblyName" => field.AssemblyName,
            "Attributes" => field.Attributes,
            "DocComment" => field.DocComment,
            "Parent" => field.Parent,
            "Type" => field.Type,
            "Value" => field.Value,
            _ => Unresolved.Value,
        };
    }

    private static object? ResolveCodeModelStaticReadOnlyField(
        CodeModelStaticReadOnlyField field,
        string identifier)
    {
        return identifier switch
        {
            "Name" => field.Name,
            "name" => field.name,
            "FullName" => field.FullName,
            "AssemblyName" => field.AssemblyName,
            "Attributes" => field.Attributes,
            "DocComment" => field.DocComment,
            "Parent" => field.Parent,
            "Type" => field.Type,
            "Value" => field.Value,
            _ => Unresolved.Value,
        };
    }

    private static object? ResolveCodeModelEvent(
        CodeModelEvent @event,
        string identifier)
    {
        return identifier switch
        {
            "Name" => @event.Name,
            "name" => @event.name,
            "FullName" => @event.FullName,
            "AssemblyName" => @event.AssemblyName,
            "Attributes" => @event.Attributes,
            "DocComment" => @event.DocComment,
            "Parent" => @event.Parent,
            "Type" => @event.Type,
            _ => Unresolved.Value,
        };
    }

    private static object? ResolveCodeModelMethod(
        CodeModelMethod method,
        string identifier)
    {
        return identifier switch
        {
            "Name" => method.Name,
            "name" => method.name,
            "FullName" => method.FullName,
            "AssemblyName" => method.AssemblyName,
            "DocComment" => method.DocComment,
            "Parent" => method.Parent,
            "Type" => method.Type,
            "ReturnType" => method.Type,
            "Parameters" => method.Parameters,
            "Attributes" => method.Attributes,
            "IsAbstract" => method.IsAbstract,
            "IsGeneric" => method.IsGeneric,
            "TypeParameters" => method.TypeParameters,
            _ => Unresolved.Value,
        };
    }

    private static object? ResolveCodeModelParameter(
        CodeModelParameter parameter,
        string identifier)
    {
        return identifier switch
        {
            "Name" => parameter.Name,
            "name" => parameter.name,
            "FullName" => parameter.FullName,
            "AssemblyName" => parameter.AssemblyName,
            "Parent" => parameter.Parent,
            "Type" => parameter.Type,
            "HasDefaultValue" => parameter.HasDefaultValue,
            "DefaultValue" => parameter.DefaultValue,
            "Attributes" => parameter.Attributes,
            _ => Unresolved.Value,
        };
    }

    private static object? ResolveCodeModelProperty(
        CodeModelProperty property,
        string identifier)
    {
        return identifier switch
        {
            "Name" => property.Name,
            "name" => property.name,
            "FullName" => property.FullName,
            "AssemblyName" => property.AssemblyName,
            "DocComment" => property.DocComment,
            "Parent" => property.Parent,
            "Type" => property.Type,
            "HasGetter" => property.HasGetter,
            "HasSetter" => property.HasSetter,
            "IsAbstract" => property.IsAbstract,
            "IsIndexer" => property.IsIndexer,
            "IsVirtual" => property.IsVirtual,
            "IsRequired" => property.IsRequired,
            "Parameters" => property.Parameters,
            "Attributes" => property.Attributes,
            "Value" => property.Value,
            _ => Unresolved.Value,
        };
    }

    private static object? ResolveCodeModelConstant(
        CodeModelConstant constant,
        string identifier)
    {
        return identifier switch
        {
            "Name" => constant.Name,
            "name" => constant.name,
            "FullName" => constant.FullName,
            "AssemblyName" => constant.AssemblyName,
            "DocComment" => constant.DocComment,
            "Parent" => constant.Parent,
            "Type" => constant.Type,
            "Value" => constant.Value,
            "Attributes" => constant.Attributes,
            _ => Unresolved.Value,
        };
    }

    private static object? ResolveCodeModelEnumValue(
        CodeModelEnumValue enumValue,
        string identifier)
    {
        return identifier switch
        {
            "Name" => enumValue.Name,
            "name" => enumValue.name,
            "FullName" => enumValue.FullName,
            "AssemblyName" => enumValue.AssemblyName,
            "DocComment" => enumValue.DocComment,
            "Parent" => enumValue.Parent,
            "Value" => enumValue.Value.ToString(provider: CultureInfo.InvariantCulture),
            "Attributes" => enumValue.Attributes,
            _ => Unresolved.Value,
        };
    }

    private static object? ResolveCodeModelType(
        CodeModelType type,
        string identifier)
    {
        return identifier switch
        {
            "Name" => type.Name,
            "name" => type.name,
            "FullName" => type.FullName,
            "AssemblyName" => type.AssemblyName,
            "Namespace" => type.Namespace,
            "OriginalName" => type.OriginalName,
            "Attributes" => type.Attributes,
            "BaseClass" => type.BaseClass,
            "Constants" => type.Constants,
            "ContainingClass" => type.ContainingClass,
            "Delegates" => type.Delegates,
            "DocComment" => type.DocComment,
            "Fields" => type.Fields,
            "FileLocations" => type.FileLocations,
            "Interfaces" => type.Interfaces,
            "Methods" => type.Methods,
            "NestedClasses" => type.NestedClasses,
            "NestedEnums" => type.NestedEnums,
            "NestedInterfaces" => type.NestedInterfaces,
            "NestedStructs" => type.NestedStructs,
            "Parent" => type.Parent,
            "Properties" => type.Properties,
            "StaticReadOnlyFields" => type.StaticReadOnlyFields,
            "Default" => type.DefaultValue,
            "DefaultValue" => type.DefaultValue,
            "ElementType" => type.ElementType,
            "Unwrap" => type.ElementType ?? type.TypeArguments.FirstOrDefault() ?? type,
            "TypeArguments" => type.TypeArguments,
            "TypeParameters" => type.TypeParameters,
            "TupleElements" => type.TupleElements,
            "IsDate" => type.IsDate,
            "IsDefined" => type.IsDefined,
            "IsDictionary" => type.IsDictionary,
            "IsEnum" => type.IsEnum,
            "IsEnumerable" => type.IsEnumerable,
            "IsGeneric" => type.IsGeneric,
            "IsGuid" => type.IsGuid,
            "IsNullable" => type.IsNullable,
            "IsDynamic" => type.IsDynamic,
            "IsPrimitive" => type.IsPrimitive,
            "IsStruct" => type.IsStruct,
            "IsTask" => type.IsTask,
            "IsTimeSpan" => type.IsTimeSpan,
            "IsValueTuple" => type.IsValueTuple,
            _ => Unresolved.Value,
        };
    }

    private static object? ResolveCodeModelAttribute(
        CodeModelAttribute attribute,
        string identifier)
    {
        return identifier switch
        {
            "Name" => attribute.Name,
            "name" => attribute.name,
            "FullName" => attribute.FullName,
            "AssemblyName" => attribute.AssemblyName,
            "Parent" => attribute.Parent,
            "Value" => attribute.Value,
            "Arguments" => attribute.Arguments,
            "Type" => attribute.Type,
            _ => Unresolved.Value,
        };
    }

    private static object? ResolveCodeModelAttributeArgument(
        CodeModelAttributeArgument argument,
        string identifier)
    {
        return identifier switch
        {
            "Name" => argument.Name,
            "name" => argument.name,
            "FullName" => argument.FullName,
            "AssemblyName" => argument.AssemblyName,
            "Value" => argument.Value,
            "Type" => argument.Type,
            "TypeValue" => argument.TypeValue,
            _ => Unresolved.Value,
        };
    }

    private static object? ResolveCodeModelDocComment(
        CodeModelDocComment docComment,
        string identifier)
    {
        return identifier switch
        {
            "Summary" => docComment.Summary,
            "Returns" => docComment.Returns,
            "Parameters" => docComment.Parameters,
            "Parent" => docComment.Parent,
            "Text" => docComment.Text,
            _ => Unresolved.Value,
        };
    }

    private static object? ResolveCodeModelParameterComment(
        CodeModelParameterComment parameterComment,
        string identifier)
    {
        return identifier switch
        {
            "Name" => parameterComment.Name,
            "Description" => parameterComment.Description,
            "Parent" => parameterComment.Parent,
            _ => Unresolved.Value,
        };
    }

    private static object? ResolveCodeModelTypeParameter(
        CodeModelTypeParameter typeParameter,
        string identifier)
    {
        return identifier switch
        {
            "Name" => typeParameter.Name,
            "name" => typeParameter.name,
            "FullName" => typeParameter.FullName,
            "Parent" => typeParameter.Parent,
            _ => Unresolved.Value,
        };
    }

    private static object? ResolveEnumValue(
       EnumValueMetadata enumValue,
       string identifier,
       RenderState? state)
    {
        return identifier switch
        {
            "Name" => enumValue.Name,
            "name" => ToCamelCase(value: enumValue.Name),
            "FullName" => string.IsNullOrWhiteSpace(value: enumValue.ParentTypeFullName)
                ? enumValue.Name
                : string.Concat(str0: enumValue.ParentTypeFullName, str1: ".", str2: enumValue.Name),
            "AssemblyName" => enumValue.AssemblyName,
            "Value" => enumValue.Value?.ToString(provider: CultureInfo.InvariantCulture) ?? string.Empty,
            "Parent" => state?.ResolveParentType(fullName: enumValue.ParentTypeFullName) ?? Unresolved.Value,
            "DocComment" => enumValue.DocComment,
            "Attributes" => enumValue.Attributes,
            _ => Unresolved.Value,
        };
    }

    private static object? ResolveAttribute(
        AttributeMetadata attribute,
        string identifier)
    {
        return identifier switch
        {
            "Name" => attribute.Name,
            "name" => ToCamelCase(value: attribute.Name),
            "FullName" => attribute.FullName,
            "AssemblyName" => attribute.AssemblyName,
            "Value" => FormatAttributeValue(attribute: attribute),
            "Arguments" => attribute.Arguments,
            "Type" => attribute.Type,
            _ => Unresolved.Value,
        };
    }

    private static string FormatAttributeValue(AttributeMetadata attribute) =>
        TemplateAttributeValueFormatter.Format(attribute: attribute);

    private static object? ResolveAttributeArgument(
        AttributeArgumentMetadata argument,
        string identifier)
    {
        return identifier switch
        {
            "Name" => argument.Name ?? string.Empty,
            "AssemblyName" => argument.AssemblyName,
            "Value" => argument.Value ?? string.Empty,
            "Type" => argument.Type,
            "TypeValue" => argument.TypeValue,
            _ => Unresolved.Value,
        };
    }

    private static object ResolveScalar(
        string scalar,
        string identifier)
    {
        if (TryResolveTemplateNameCase(identifier: identifier, nameCase: out var nameCase))
        {
            return Typewriter.CodeModel.NameCaseFormatter.Format(value: scalar, nameCase: nameCase);
        }

        return identifier switch
        {
            "Value" => scalar,
            _ => Unresolved.Value,
        };
    }

    private static bool TryResolveTemplateNameCase(
        string identifier,
        out NameCase nameCase)
    {
        if (Typewriter.CodeModel.NameCaseFormatter.TryParse(value: identifier, nameCase: out nameCase))
        {
            return true;
        }

        if (!identifier.EndsWith(value: "Name", comparisonType: StringComparison.Ordinal)
            || identifier.Equals(value: "OriginalName", comparisonType: StringComparison.Ordinal)
            || identifier.Length <= "Name".Length)
        {
            return false;
        }

        return Typewriter.CodeModel.NameCaseFormatter.TryParse(value: identifier[..^"Name".Length], nameCase: out nameCase);
    }

    private static string ReadIdentifier(
        string template,
        int start)
    {
        var end = start;
        while (end < template.Length
            && (char.IsLetterOrDigit(c: template[index: end]) || template[index: end] == '_'))
        {
            end++;
        }

        return template[start..end];
    }

    private static string? ResolveEnumDefault(
        TypeMetadataReference type,
        RenderState? state)
    {
        if (type.EnumValues.Count > 0)
        {
            return $"{type.Name}.{type.EnumValues[index: 0].Name}";
        }

        return state is not null
            && state.TryResolveType(fullName: type.FullName, type: out var enumMetadata)
            && enumMetadata.EnumValues.Count > 0
                ? $"{type.Name}.{enumMetadata.EnumValues[index: 0].Name}"
                : null;
    }

    private static bool TryReadBlock(
        string template,
        ref int index,
        char open,
        char close,
        out string? block)
    {
        block = null;
        var openIndex = index + 1;
        if (openIndex >= template.Length || template[index: openIndex] != open)
        {
            return false;
        }

        var depth = 0;
        for (var cursor = openIndex; cursor < template.Length; cursor++)
        {
            if (template[index: cursor] == open)
            {
                depth++;
            }
            else if (template[index: cursor] == close)
            {
                depth--;
                if (depth == 0)
                {
                    block = template.Substring(startIndex: openIndex + 1, length: cursor - openIndex - 1);
                    index = cursor;
                    return true;
                }
            }
        }

        index = template.Length - 1;
        return true;
    }

    private static string GetName(object item)
    {
        return item switch
        {
            TypeMetadata type => type.Name,
            PropertyMetadata property => property.Name,
            MethodMetadata method => method.Name,
            ParameterMetadata parameter => parameter.Name,
            ConstantMetadata constant => constant.Name,
            EnumValueMetadata enumValue => enumValue.Name,
            AttributeMetadata attribute => attribute.Name,
            _ => item.ToString() ?? string.Empty,
        };
    }

    private static string ToCamelCase(string value)
    {
        if (string.IsNullOrEmpty(value: value) || char.IsLower(c: value[index: 0]))
        {
            return value;
        }

        return value.Length == 1
            ? value.ToLowerInvariant()
            : char.ToLowerInvariant(c: value[index: 0]) + value[1..];
    }

    private static string NormalizeRenderedWhitespace(string value)
    {
        var normalized = value.Replace(oldValue: "\r\n", newValue: "\n", comparisonType: StringComparison.Ordinal)
            .Replace(oldChar: '\r', newChar: '\n');
        var lines = normalized.Split(separator: '\n');
        var nextNonBlankLineIndexes = new int[lines.Length];
        var nextNonBlankLineIndex = -1;
        for (var index = lines.Length - 1; index >= 0; index--)
        {
            nextNonBlankLineIndexes[index] = nextNonBlankLineIndex;
            if (!string.IsNullOrWhiteSpace(value: lines[index]))
            {
                nextNonBlankLineIndex = index;
            }
        }

        var output = new List<string>(capacity: lines.Length);
        string? previousNonBlankLine = null;
        for (var index = 0; index < lines.Length; index++)
        {
            if (!string.IsNullOrWhiteSpace(value: lines[index]))
            {
                output.Add(item: lines[index]);
                previousNonBlankLine = lines[index];
                continue;
            }

            var nextIndex = nextNonBlankLineIndexes[index];
            var next = nextIndex < 0 ? null : lines[nextIndex];
            if (previousNonBlankLine?.TrimEnd().EndsWith(value: '{') == true
                || next?.TrimStart().StartsWith(value: '}') == true)
            {
                continue;
            }

            output.Add(item: lines[index]);
        }

        return string.Join(separator: '\n', values: output);
    }

    private static object? ResolveContainingType(
        TypeMetadata type,
        TypeMetadataKind kind,
        RenderState? state)
    {
        if (string.IsNullOrWhiteSpace(value: type.ContainingTypeFullName))
        {
            return null;
        }

        if (state is null
            || !state.TryResolveType(fullName: type.ContainingTypeFullName, type: out var containingType))
        {
            return Unresolved.Value;
        }

        return containingType.Kind == kind ? containingType : null;
    }

    private string RenderCore(
        string template,
        object context,
        RenderState state)
    {
        var output = new StringBuilder(capacity: template.Length);
        for (var index = 0; index < template.Length; index++)
        {
            if (template[index: index] != '$')
            {
                output.Append(value: template[index: index]);
                continue;
            }

            if (index + 1 < template.Length
                && template[index: index + 1] == '$')
            {
                output.Append(value: '$');
#pragma warning disable S127 // "for" loop stop conditions should be invariant
                index++;
#pragma warning restore S127 // "for" loop stop conditions should be invariant
                continue;
            }

            var identifier = ReadIdentifier(template: template, start: index + 1);
            if (identifier.Length == 0)
            {
                output.Append(value: template[index: index]);
                continue;
            }

            var identifierStart = index;
#pragma warning disable S127 // "for" loop stop conditions should be invariant
            index += identifier.Length;
#pragma warning restore S127 // "for" loop stop conditions should be invariant
            if (!TryResolveCompatibilityString(context: context, identifier: identifier, state: state, value: out var value)
                && !TryResolve(context: context, identifier: identifier, value: out value, state: state))
            {
                state.AddUnknownIdentifier(identifier: identifier, index: identifierStart);
                output.Append(value: '$').Append(value: identifier);
                continue;
            }

            RenderValue(template: template, context: context, state: state, output: output, index: ref index, identifier: identifier, value: value);
        }

        return output.ToString();
    }

    private void RenderValue(
        string template,
        object context,
        RenderState state,
        StringBuilder output,
        ref int index,
        string identifier,
        object? value)
    {
        if (value is TypeTemplateValue typeValue)
        {
            AppendScalar(template: template, state: state, output: output, index: ref index, value: typeValue.Text, blockContext: typeValue);
            return;
        }

        if (value is CodeModelType codeModelType)
        {
            AppendScalar(template: template, state: state, output: output, index: ref index, value: codeModelType.Name, blockContext: codeModelType);
            return;
        }

        if (value is string stringValue)
        {
            AppendScalar(template: template, state: state, output: output, index: ref index, value: stringValue);
            return;
        }

        if (value is bool boolValue)
        {
            AppendBoolean(template: template, context: context, state: state, output: output, index: ref index, value: boolValue);
            return;
        }

        if (value is IEnumerable collection)
        {
            AppendCollection(template: template, context: context, state: state, output: output, index: ref index, identifier: identifier, collection: collection);
            return;
        }

        if (value is null)
        {
            _ = TryReadBlock(template: template, index: ref index, open: '[', close: ']', block: out _);
            return;
        }

        AppendScalar(template: template, state: state, output: output, index: ref index, value: value.ToString() ?? string.Empty, blockContext: value);
    }

    private void AppendCollection(
        string template,
        object context,
        RenderState state,
        StringBuilder output,
        ref int index,
        string identifier,
        IEnumerable collection)
    {
        var originalIndex = index;
        _ = TryReadBlock(template: template, index: ref index, open: '(', close: ')', block: out var filter);
        if (!TryReadBlock(template: template, index: ref index, open: '[', close: ']', block: out var block)
            || block is null)
        {
            var scalarValue = FormatCollectionScalar(collection: collection);
            if (scalarValue is not null)
            {
                index = originalIndex;
                output.Append(value: scalarValue);
                return;
            }

            state.AddParseError(identifier: identifier, index: index, message: "Collection template block is missing.");
            return;
        }

        _ = TryReadBlock(template: template, index: ref index, open: '[', close: ']', block: out var separator);

        var items = ApplyFilter(items: collection.Cast<object>(), filter: filter, parentContext: context, state: state).ToArray();
        if (context is ProjectMetadata
            && IsRootItemCollection(identifier: identifier))
        {
            state.AddRootItemCount(count: items.Length);
        }

        state.PushParentContext(context: context);
        try
        {
            for (var itemIndex = 0; itemIndex < items.Length; itemIndex++)
            {
                if (itemIndex > 0 && separator is not null)
                {
                    output.Append(value: RenderCore(template: separator, context: context, state: state));
                }

                output.Append(value: RenderCore(template: block, context: items[itemIndex], state: state));
            }
        }
        finally
        {
            state.PopParentContext();
        }
    }

    private void AppendBoolean(
        string template,
        object context,
        RenderState state,
        StringBuilder output,
        ref int index,
        bool value)
    {
        if (!TryReadBlock(template: template, index: ref index, open: '[', close: ']', block: out var trueBlock))
        {
            output.Append(value: value ? "true" : "false");
            return;
        }

        _ = TryReadBlock(template: template, index: ref index, open: '[', close: ']', block: out var falseBlock);
        output.Append(value: RenderCore(template: value ? trueBlock ?? string.Empty : falseBlock ?? string.Empty, context: context, state: state));
    }

    private void AppendScalar(
        string template,
        RenderState state,
        StringBuilder output,
        ref int index,
        string value,
        object? blockContext = null)
    {
        if (TryReadBlock(template: template, index: ref index, open: '[', close: ']', block: out var block))
        {
            output.Append(value: RenderCore(template: block ?? string.Empty, context: blockContext ?? value, state: state));
            return;
        }

        if (index + 1 < template.Length
            && template[index: index + 1] == '$'
            && (index + 2 >= template.Length
                || (template[index: index + 2] != '{' && !char.IsLetter(c: template[index: index + 2]) && template[index: index + 2] != '_')))
        {
            index++;
        }

        output.Append(value: value);
    }

    private IEnumerable<object> ApplyFilter(
        IEnumerable<object> items,
        string? filter,
        object parentContext,
        RenderState state)
    {
        if (string.IsNullOrWhiteSpace(value: filter))
        {
            return items;
        }

        var trimmed = filter.Trim();
        if (TryApplyPredicateFilter(items: items, filter: trimmed, parentContext: parentContext, state: state, filtered: out var filtered))
        {
            return filtered;
        }

        return trimmed switch
        {
            "Class" => items.OfType<TypeMetadata>().Where(predicate: type => type.Kind == TypeMetadataKind.Class),
            "Record" => items.OfType<TypeMetadata>().Where(predicate: type => type.Kind == TypeMetadataKind.Record),
            "Struct" => items.OfType<TypeMetadata>().Where(predicate: type => type.Kind == TypeMetadataKind.Struct),
            "Interface" => items.OfType<TypeMetadata>().Where(predicate: type => type.Kind == TypeMetadataKind.Interface),
            "Enum" => items.OfType<TypeMetadata>().Where(predicate: type => type.Kind == TypeMetadataKind.Enum),
            "HasProperties" => items.OfType<TypeMetadata>().Where(predicate: type => type.Properties.Count > 0),
            "HasMethods" => items.OfType<TypeMetadata>().Where(predicate: type => type.Methods.Count > 0),
            "HasConstants" => items.OfType<TypeMetadata>().Where(predicate: type => type.Constants.Count > 0),
            "Static" => items.OfType<TypeMetadata>().Where(predicate: type => type.IsStatic),
            "NonStatic" => items.OfType<TypeMetadata>().Where(predicate: type => !type.IsStatic),
            "Public" => items.Where(predicate: item => HasAccessibility(item: item, accessibility: MetadataAccessibility.Public)),
            "Internal" => items.Where(predicate: item => HasAccessibility(item: item, accessibility: MetadataAccessibility.Internal)),
            _ when trimmed.StartsWith(value: "Namespace=", comparisonType: StringComparison.OrdinalIgnoreCase) => items
                .OfType<TypeMetadata>()
                .Where(predicate: type => type.Namespace.Equals(value: trimmed["Namespace=".Length..], comparisonType: StringComparison.Ordinal)),
            _ when trimmed.StartsWith(value: "Name=", comparisonType: StringComparison.OrdinalIgnoreCase) => items
                .Where(predicate: item => GetName(item: item).Equals(value: trimmed["Name=".Length..], comparisonType: StringComparison.Ordinal)),
            _ when trimmed.StartsWith(value: '[') && trimmed.EndsWith(value: ']') => items
                .Where(predicate: item => MatchesAny(values: GetAttributeSelectors(item: item), pattern: trimmed.Trim('[', ']', ' '))),
            _ when trimmed.StartsWith(value: ':') => items
                .Where(predicate: item => MatchesAny(values: GetInheritanceSelectors(item: item), pattern: trimmed[1..].Trim())),
            _ => items.Where(predicate: item => MatchesAny(values: GetItemSelectors(item: item), pattern: trimmed)),
        };
    }

    private bool TryApplyPredicateFilter(
        IEnumerable<object> items,
        string filter,
        object parentContext,
        RenderState state,
        out IEnumerable<object> filtered)
    {
        filtered = [];
        if (TryParseLambda(value: filter, parameterName: out var parameterName, expression: out var expression))
        {
            filtered = items.Where(predicate: item => EvaluatePredicate(expression: expression, parameterName: parameterName, context: item));
            return true;
        }

        var methodName = filter.StartsWith(value: '$')
            ? filter[1..]
            : filter;
        if (state.HasCompiledMethod(methodName: methodName))
        {
            filtered = items.Where(predicate: item => state.InvokeCompiledPredicate(methodName: methodName, parentContext: parentContext, context: item));
            return true;
        }

        if (!state.TryGetCompatibilityMethod(
                name: methodName,
                kind: TemplateCompatibilityMethodKind.Predicate,
                method: out var method))
        {
            return false;
        }

        filtered = items.Where(predicate: item => EvaluateCompatibilityPredicate(method: method, context: item));
        return true;
    }

    private bool EvaluateCompatibilityPredicate(
        TemplateCompatibilityMethod method,
        object context)
    {
        return method.Expression is not null
            ? EvaluatePredicate(expression: method.Expression, parameterName: method.ParameterName, context: context)
            : EvaluateRecipePredicate(methodName: method.Name, body: method.Body, context: context);
    }

    private bool TryResolveCompatibilityString(
        object context,
        string identifier,
        RenderState state,
        out object? value)
    {
        value = null;
        if (state.TryInvokeCompiledValue(methodName: identifier, context: context, value: out value))
        {
            return true;
        }

        if (!state.TryGetCompatibilityMethod(
                name: identifier,
                kind: TemplateCompatibilityMethodKind.String,
                method: out var method))
        {
            return false;
        }

        if (method.Expression is not null
            && TryEvaluateValue(expression: method.Expression, parameterName: method.ParameterName, context: context, value: out value))
        {
            value = value?.ToString() ?? string.Empty;
            return true;
        }

        if (TryEvaluateRecipeString(methodName: method.Name, context: context, state: state, value: out var recipeValue))
        {
            value = recipeValue;
            return true;
        }

        value = string.Empty;
        return true;
    }

    private bool TryEvaluateRecipeString(
        string methodName,
        object context,
        RenderState state,
        out string value)
    {
        value = string.Empty;
        switch (methodName)
        {
            case "NullableMark" when context is PropertyMetadata property:
                value = property.Type.IsNullable ? "?" : string.Empty;
                return true;
            case "ReturnTypeDefault" when TryGetTypeReference(value: context, type: out var type):
                value = GetDefaultValue(type: type, state: state).Replace(oldChar: '\"', newChar: '\'');
                return true;
            case "GetAttributeValueOrReturnEnumNameIfNoAttribute" when context is EnumValueMetadata enumValue:
                value = enumValue.Name;
                return true;
            case "GetEnumAsStringIfItsStringable" when context is EnumValueMetadata enumValue:
                value = enumValue.Value?.ToString(provider: CultureInfo.InvariantCulture) ?? enumValue.Name;
                return true;
            case "InheritClass" or "InheritRecord" when context is TypeMetadata type:
                value = type.BaseTypes.FirstOrDefault() is { } baseType
                    ? " extends " + baseType.Name
                    : string.Empty;
                return true;
            case "InheritInterfaceForClass" or "InheritInterfaceForRecord" when context is TypeMetadata type:
                value = type.BaseTypes.FirstOrDefault() is { } interfaceBaseType
                    ? " extends I" + interfaceBaseType.Name
                    : string.Empty;
                return true;
            case "ImplementsInterfaceForClass" or "ImplementsInterfaceForRecord" when context is TypeMetadata type:
                value = " implements I" + type.Name;
                return true;
            case "SuperClass" or "SuperRecord" when context is TypeMetadata type:
                value = type.BaseTypes.Count > 0
                    ? Environment.NewLine + "    super(initObj);"
                    : string.Empty;
                return true;
            default:
                return false;
        }
    }

    private bool EvaluatePredicate(
        string expression,
        string parameterName,
        object context)
    {
        var trimmed = StripOuterParentheses(expression: expression.Trim());
        if (TrySplitTopLevel(expression: trimmed, operatorText: "||", left: out var left, right: out var right))
        {
            return EvaluatePredicate(expression: left, parameterName: parameterName, context: context)
                || EvaluatePredicate(expression: right, parameterName: parameterName, context: context);
        }

        if (TrySplitTopLevel(expression: trimmed, operatorText: "&&", left: out left, right: out right))
        {
            return EvaluatePredicate(expression: left, parameterName: parameterName, context: context)
                && EvaluatePredicate(expression: right, parameterName: parameterName, context: context);
        }

        if (trimmed.StartsWith(value: '!'))
        {
            return !EvaluatePredicate(expression: trimmed[1..], parameterName: parameterName, context: context);
        }

        if (TryEvaluateAnyPredicate(expression: trimmed, parameterName: parameterName, context: context, result: out var anyResult))
        {
            return anyResult;
        }

        if (TryEvaluateStringPredicate(expression: trimmed, parameterName: parameterName, context: context, result: out var stringResult))
        {
            return stringResult;
        }

        if (TryEvaluateComparison(expression: trimmed, parameterName: parameterName, context: context, result: out var comparisonResult))
        {
            return comparisonResult;
        }

        return TryEvaluateValue(expression: trimmed, parameterName: parameterName, context: context, value: out var value)
            && value is bool boolValue
            && boolValue;
    }

#pragma warning disable CC0091,S2325
    private bool EvaluateRecipePredicate(
        string methodName,
        string body,
        object context)
#pragma warning restore CC0091,S2325
    {
        return methodName switch
        {
            "IncludeProperty" => context is PropertyMetadata property
                && !HasAttribute(attributes: property.Attributes, name: "JsonIgnore"),
            "IncludeEnums" => context is TypeMetadata { Kind: TypeMetadataKind.Enum } type
                && MatchesRecipeTypePredicate(type: type, body: body),
            "IncludeClass" => context is TypeMetadata { Kind: TypeMetadataKind.Class } type
                && MatchesRecipeTypePredicate(type: type, body: body),
            "IncludeClassWithoutStatic" => context is TypeMetadata { Kind: TypeMetadataKind.Class } type
                && MatchesRecipeTypePredicate(type: type, body: body),
            "IncludeClassStaticOnly" => false,
            "IncludeInterface" => context is TypeMetadata { Kind: TypeMetadataKind.Interface } type
                && MatchesRecipeTypePredicate(type: type, body: body),
            "IncludeRecord" => context is TypeMetadata { Kind: TypeMetadataKind.Record } type
                && MatchesRecipeTypePredicate(type: type, body: body),
            "IncludeStruct" => context is TypeMetadata { Kind: TypeMetadataKind.Struct } type
                && MatchesRecipeTypePredicate(type: type, body: body),
            "IsEnumAsNumber" => context is TypeMetadata type
                && !HasAttribute(attributes: type.Attributes, name: "AsString"),
            _ => false,
        };
    }

    private bool TryEvaluateAnyPredicate(
        string expression,
        string parameterName,
        object context,
        out bool result)
    {
        result = false;
        if (!TryParseInstanceMethodCall(expression: expression, methodName: "Any", target: out var target, arguments: out var arguments)
            || !TryEvaluateValue(expression: target, parameterName: parameterName, context: context, value: out var collection)
            || collection is not IEnumerable enumerable)
        {
            return false;
        }

        var items = enumerable.Cast<object>().ToArray();
        if (string.IsNullOrWhiteSpace(value: arguments))
        {
            result = items.Length > 0;
            return true;
        }

        if (!TryParseLambda(value: arguments, parameterName: out var itemName, expression: out var itemExpression))
        {
            return false;
        }

        result = items.Any(predicate: item => EvaluatePredicate(expression: itemExpression, parameterName: itemName, context: item));
        return true;
    }

    private bool TryEvaluateStringPredicate(
        string expression,
        string parameterName,
        object context,
        out bool result)
    {
        foreach (var methodName in new[] { "StartsWith", "EndsWith", "Contains", "Equals" })
        {
            if (!TryParseInstanceMethodCall(expression: expression, methodName: methodName, target: out var target, arguments: out var arguments)
                || !TryReadFirstStringLiteral(value: arguments, literal: out var expected)
                || !TryEvaluateValue(expression: target, parameterName: parameterName, context: context, value: out var value))
            {
                continue;
            }

            var text = value?.ToString() ?? string.Empty;
            result = methodName switch
            {
                "StartsWith" => text.StartsWith(value: expected, comparisonType: StringComparison.Ordinal),
                "EndsWith" => text.EndsWith(value: expected, comparisonType: StringComparison.Ordinal),
                "Contains" => text.Contains(value: expected, comparisonType: StringComparison.Ordinal),
                "Equals" => text.Equals(value: expected, comparisonType: StringComparison.Ordinal),
                _ => false,
            };
            return true;
        }

        result = false;
        return false;
    }

    private bool TryEvaluateComparison(
        string expression,
        string parameterName,
        object context,
        out bool result)
    {
        result = false;
        var isNegated = false;
        if (!TrySplitTopLevel(expression: expression, operatorText: "==", left: out var left, right: out var right))
        {
            if (!TrySplitTopLevel(expression: expression, operatorText: "!=", left: out left, right: out right))
            {
                return false;
            }

            isNegated = true;
        }

        if (!TryEvaluateValue(expression: left, parameterName: parameterName, context: context, value: out var leftValue)
            || !TryEvaluateValue(expression: right, parameterName: parameterName, context: context, value: out var rightValue))
        {
            return false;
        }

        result = ValuesEqual(left: leftValue, right: rightValue);
        if (isNegated)
        {
            result = !result;
        }

        return true;
    }

    private bool TryEvaluateValue(
        string expression,
        string parameterName,
        object context,
        out object? value)
    {
        var trimmed = StripOuterParentheses(expression: expression.Trim());
        if (TryEvaluateLiteral(expression: trimmed, value: out value))
        {
            return true;
        }

        if (TrySplitTopLevel(expression: trimmed, operatorText: "+", left: out var left, right: out var right)
            && TryEvaluateValue(expression: left, parameterName: parameterName, context: context, value: out var leftValue)
            && TryEvaluateValue(expression: right, parameterName: parameterName, context: context, value: out var rightValue))
        {
            value = string.Concat(str0: leftValue?.ToString(), str1: rightValue?.ToString());
            return true;
        }

        if (TryEvaluateStringTransform(expression: trimmed, parameterName: parameterName, context: context, value: out value))
        {
            return true;
        }

        if (trimmed.Equals(value: parameterName, comparisonType: StringComparison.Ordinal))
        {
            value = context;
            return true;
        }

        var path = trimmed.StartsWith(value: parameterName + ".", comparisonType: StringComparison.Ordinal)
            ? trimmed[(parameterName.Length + 1)..]
            : trimmed;
        value = context;
        foreach (var segment in SplitMemberPath(path: path))
        {
            if (value is null)
            {
                return true;
            }

            if (!TryResolve(context: value, identifier: NormalizeLegacyMemberName(segment: segment), value: out value))
            {
                return false;
            }
        }

        return true;
    }

    private bool TryEvaluateStringTransform(
        string expression,
        string parameterName,
        object context,
        out object? value)
    {
        value = null;
        if (TryParseInstanceMethodCall(expression: expression, methodName: "ToUpperInvariant", target: out var target, arguments: out var arguments)
            && string.IsNullOrWhiteSpace(value: arguments)
            && TryEvaluateValue(expression: target, parameterName: parameterName, context: context, value: out var targetValue))
        {
            value = (targetValue?.ToString() ?? string.Empty).ToUpperInvariant();
            return true;
        }

        if (TryParseInstanceMethodCall(expression: expression, methodName: "ToLowerInvariant", target: out target, arguments: out arguments)
            && string.IsNullOrWhiteSpace(value: arguments)
            && TryEvaluateValue(expression: target, parameterName: parameterName, context: context, value: out targetValue))
        {
            value = (targetValue?.ToString() ?? string.Empty).ToLowerInvariant();
            return true;
        }

        if (TryParseInstanceMethodCall(expression: expression, methodName: "Replace", target: out target, arguments: out arguments)
            && TryReadStringLiteralPair(value: arguments, first: out var oldValue, second: out var newValue)
            && TryEvaluateValue(expression: target, parameterName: parameterName, context: context, value: out targetValue))
        {
            value = (targetValue?.ToString() ?? string.Empty).Replace(oldValue: oldValue, newValue: newValue, comparisonType: StringComparison.Ordinal);
            return true;
        }

        if (TryParseInstanceMethodCall(expression: expression, methodName: "ClassName", target: out target, arguments: out arguments)
            && string.IsNullOrWhiteSpace(value: arguments)
            && TryEvaluateValue(expression: target, parameterName: parameterName, context: context, value: out targetValue)
            && TryGetTypeReference(value: targetValue, type: out var type))
        {
            value = GetClassName(type: type);
            return true;
        }

        if (TryParseInstanceMethodCall(expression: expression, methodName: "Default", target: out target, arguments: out arguments)
            && string.IsNullOrWhiteSpace(value: arguments)
            && TryEvaluateValue(expression: target, parameterName: parameterName, context: context, value: out targetValue)
            && TryGetTypeReference(value: targetValue, type: out type))
        {
            value = GetDefaultValue(type: type);
            return true;
        }

        if (TryParseInstanceMethodCall(expression: expression, methodName: "ToString", target: out target, arguments: out arguments)
            && string.IsNullOrWhiteSpace(value: arguments)
            && TryEvaluateValue(expression: target, parameterName: parameterName, context: context, value: out targetValue))
        {
            value = targetValue?.ToString() ?? string.Empty;
            return true;
        }

        return false;
    }

    private string MapType(
        TypeMetadataReference type,
        RenderState? state,
        FrontendRuntimeTypeKind runtimeType = FrontendRuntimeTypeKind.Auto)
    {
        var dateMapping = state?.DateMapping
                          ?? TypeScriptDateMapping.Legacy(
                              dateType: TypeScriptTypeMapper.DefaultDateType,
                              dateOnlyType: TypeScriptTypeMapper.DefaultDateOnlyType,
                              timeOnlyType: TypeScriptTypeMapper.DefaultTimeOnlyType);
        return _typeMapper.Map(
            type: type,
            strictNull: state?.StrictNullGeneration ?? true,
            dateMapping: dateMapping,
            decimalType: state?.DecimalTypeGeneration,
            guidType: state?.GuidTypeGeneration,
            runtimeType: runtimeType);
    }

    private string GetLegacyTypeName(
        TypeMetadataReference type,
        bool strictNull,
        TypeScriptDateMapping dateMapping,
        string? guidType,
        string? decimalType,
        FrontendRuntimeTypeKind runtimeType)
    {
        return _typeMapper.Map(
            type: type,
            strictNull: strictNull,
            dateMapping: dateMapping,
            decimalType: decimalType,
            guidType: guidType,
            runtimeType: runtimeType);
    }

    private string GetLegacyTypeName(
        TypeMetadataReference type,
        bool strictNull = true,
        string? dateType = null,
        string? guidType = null,
        string? dateOnlyType = null,
        string? timeOnlyType = null,
        string? decimalType = null)
    {
        return _typeMapper.Map(type: type, strictNull: strictNull, dateType: dateType, decimalType: decimalType, guidType: guidType, dateOnlyType: dateOnlyType, timeOnlyType: timeOnlyType);
    }

    private string GetClassName(
        TypeMetadataReference type,
        bool strictNull = true,
        string? dateType = null,
        string? guidType = null,
        string? dateOnlyType = null,
        string? timeOnlyType = null,
        string? decimalType = null)
    {
        var className = type.IsCollection && type.ElementType is not null
            ? _typeMapper.Map(type: type.ElementType, strictNull: strictNull, dateType: dateType, decimalType: decimalType, guidType: guidType, dateOnlyType: dateOnlyType, timeOnlyType: timeOnlyType)
            : _typeMapper.Map(type: type, strictNull: strictNull, dateType: dateType, decimalType: decimalType, guidType: guidType, dateOnlyType: dateOnlyType, timeOnlyType: timeOnlyType);
        return className
            .Replace(oldValue: " | null", newValue: string.Empty, comparisonType: StringComparison.Ordinal)
            .Replace(oldValue: "(", newValue: string.Empty, comparisonType: StringComparison.Ordinal)
            .Replace(oldValue: ")", newValue: string.Empty, comparisonType: StringComparison.Ordinal)
            .TrimEnd('[', ']');
    }

    private string GetClassName(
        TypeMetadataReference type,
        bool strictNull,
        TypeScriptDateMapping dateMapping,
        string? guidType,
        string? decimalType,
        FrontendRuntimeTypeKind runtimeType)
    {
        var effectiveType = type.IsCollection && type.ElementType is not null
            ? type.ElementType
            : type;
        var className = _typeMapper.Map(
            type: effectiveType,
            strictNull: strictNull,
            dateMapping: dateMapping,
            decimalType: decimalType,
            guidType: guidType,
            runtimeType: runtimeType);
        return className
            .Replace(oldValue: " | null", newValue: string.Empty, comparisonType: StringComparison.Ordinal)
            .Replace(oldValue: "(", newValue: string.Empty, comparisonType: StringComparison.Ordinal)
            .Replace(oldValue: ")", newValue: string.Empty, comparisonType: StringComparison.Ordinal)
            .TrimEnd('[', ']');
    }

    private string GetDefaultValue(
        TypeMetadataReference type,
        RenderState? state = null)
    {
        return GetDefaultValue(type: type, state: state, settings: null);
    }

#pragma warning disable CC0091,S2325
    private string GetDefaultValue(
        TypeMetadataReference type,
        RenderState? state,
        Typewriter.Configuration.Settings? settings,
        FrontendRuntimeTypeKind runtimeType = FrontendRuntimeTypeKind.Auto)
#pragma warning restore CC0091,S2325
    {
        var containerDefault = GetContainerDefault(type: type);
        if (containerDefault is not null)
        {
            return containerDefault;
        }

        var stringLiteralCharacter = settings?.StringLiteralCharacter ?? state?.StringLiteralCharacter ?? '"';
        var overriddenDefault = GetRuntimeOverrideDefault(
            runtimeType: runtimeType,
            settings: settings,
            state: state,
            stringLiteralCharacter: stringLiteralCharacter);
        if (overriddenDefault is not null)
        {
            return overriddenDefault;
        }

        var semanticDefault = GetSemanticDateDefault(type: type, runtimeType: runtimeType, settings: settings, state: state);
        if (semanticDefault is not null)
        {
            return semanticDefault;
        }

        if (type.FullName.Equals(value: "System.Guid", comparisonType: StringComparison.Ordinal))
        {
            return GetGuidDefault(settings: settings, state: state, stringLiteralCharacter: stringLiteralCharacter);
        }

        if (TryGetSpecialDefault(type: type, state: state, settings: settings, stringLiteralCharacter: stringLiteralCharacter, value: out var specialDefault))
        {
            return specialDefault;
        }

        if (type.IsEnum)
        {
            return ResolveEnumDefault(type: type, state: state) ?? "0";
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

        if (type.Name.Equals(value: "String", comparisonType: StringComparison.OrdinalIgnoreCase)
            || type.FullName.Equals(value: "System.String", comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            return $"{stringLiteralCharacter}{stringLiteralCharacter}";
        }

        return $"new {GetClassNameForDefault(type: type)}()";
    }

#pragma warning disable SA1204
    private static string GetGuidDefault(
        Typewriter.Configuration.Settings? settings,
        RenderState? state,
        char stringLiteralCharacter) =>
        ScalarInitializer.ResolveGuid(
            guidType: ResolveGuidType(settings: settings, state: state),
            guidInitializer: ResolveGuidInitializer(settings: settings, state: state),
            stringLiteralCharacter: stringLiteralCharacter);
#pragma warning restore SA1204

#pragma warning disable CC0091,S2325
    private string? GetContainerDefault(TypeMetadataReference type)
    {
        if (type.IsNullable)
        {
            return "null";
        }

        if (type.IsDictionary)
        {
            return "{}";
        }

        return type.IsCollection ? "[]" : null;
    }

    private bool TryGetSpecialDefault(
        TypeMetadataReference type,
        RenderState? state,
        Typewriter.Configuration.Settings? settings,
        char stringLiteralCharacter,
        [NotNullWhen(returnValue: true)] out string? value)
    {
        if (type.FullName.Equals(value: "System.TimeSpan", comparisonType: StringComparison.Ordinal))
        {
            value = $"{stringLiteralCharacter}00:00:00{stringLiteralCharacter}";
            return true;
        }

        if (TypeScriptTemporalTypes.IsDateOnly(fullName: type.FullName))
        {
            value = ResolveDateOnlyInitializer(settings: settings, state: state);
            return true;
        }

        if (TypeScriptTemporalTypes.IsTimeOnly(fullName: type.FullName))
        {
            value = TypeScriptTemporalTypes.FormatTimeOnlyInitializer(
                initializer: ResolveTimeOnlyInitializer(settings: settings, state: state),
                stringLiteralCharacter: stringLiteralCharacter);
            return true;
        }

        value = GetConfiguredTemporalOrDecimalDefault(type: type, state: state, settings: settings);
        return value is not null;
    }

    private string? GetConfiguredTemporalOrDecimalDefault(
        TypeMetadataReference type,
        RenderState? state,
        Typewriter.Configuration.Settings? settings)
    {
        var decimalDefault = GetConfiguredDecimalDefault(type: type, state: state, settings: settings);
        if (decimalDefault is not null)
        {
            return decimalDefault;
        }

        return type.IsDateLike || TypeScriptTemporalTypes.IsDateTime(fullName: type.FullName)
            ? ResolveDateInitializer(settings: settings, state: state)
            : null;
    }

    private string? GetConfiguredDecimalDefault(
        TypeMetadataReference type,
        RenderState? state,
        Typewriter.Configuration.Settings? settings)
    {
        if (!type.FullName.Equals(value: "System.Decimal", comparisonType: StringComparison.Ordinal))
        {
            return null;
        }

        return ScalarInitializer.ResolveDecimal(
            decimalType: ResolveDecimalType(settings: settings, state: state),
            decimalInitializer: ResolveDecimalInitializer(settings: settings, state: state));
    }

#pragma warning restore CC0091,S2325

    private bool TryResolve(
        object context,
        string identifier,
        out object? value,
        RenderState? state = null)
    {
        if (TryResolveNameCase(context: context, identifier: identifier, value: out value))
        {
            return true;
        }

        value = context switch
        {
            ProjectMetadata metadata => ResolveProject(metadata: metadata, identifier: identifier),
            TypeMetadata type => ResolveType(type: type, identifier: identifier, state: state),
            PropertyMetadata property => ResolveProperty(property: property, identifier: identifier, state: state),
            MethodMetadata method => ResolveMethod(method: method, identifier: identifier, state: state),
            ParameterMetadata parameter => ResolveParameter(parameter: parameter, identifier: identifier, state: state),
            ConstantMetadata constant => ResolveConstant(constant: constant, identifier: identifier, state: state),
            FieldMetadata field => ResolveField(field: field, identifier: identifier, state: state),
            StaticReadOnlyFieldMetadata field => ResolveStaticReadOnlyField(field: field, identifier: identifier, state: state),
            EventMetadata @event => ResolveEvent(@event: @event, identifier: identifier, state: state),
            DelegateMetadata @delegate => ResolveDelegate(@delegate: @delegate, identifier: identifier, state: state),
            DocCommentMetadata docComment => ResolveDocComment(docComment: docComment, identifier: identifier),
            ParameterCommentMetadata parameterComment => ResolveParameterComment(parameterComment: parameterComment, identifier: identifier),
            TypeParameterMetadata typeParameter => ResolveTypeParameter(typeParameter: typeParameter, identifier: identifier),
            CodeModelClass codeClass => ResolveCodeModelClass(@class: codeClass, identifier: identifier),
            CodeModelRecord codeRecord => ResolveCodeModelRecord(record: codeRecord, identifier: identifier),
            CodeModelStruct codeStruct => ResolveCodeModelStruct(@struct: codeStruct, identifier: identifier),
            CodeModelInterface codeInterface => ResolveCodeModelInterface(@interface: codeInterface, identifier: identifier),
            CodeModelEnum codeEnum => ResolveCodeModelEnum(@enum: codeEnum, identifier: identifier),
            CodeModelDelegate codeDelegate => ResolveCodeModelDelegate(@delegate: codeDelegate, identifier: identifier),
            CodeModelStaticReadOnlyField codeField => ResolveCodeModelStaticReadOnlyField(field: codeField, identifier: identifier),
            CodeModelField codeField => ResolveCodeModelField(field: codeField, identifier: identifier),
            CodeModelEvent codeEvent => ResolveCodeModelEvent(@event: codeEvent, identifier: identifier),
            CodeModelMethod codeMethod => ResolveCodeModelMethod(method: codeMethod, identifier: identifier),
            CodeModelParameter codeParameter => ResolveCodeModelParameter(parameter: codeParameter, identifier: identifier),
            CodeModelProperty codeProperty => ResolveCodeModelProperty(property: codeProperty, identifier: identifier),
            CodeModelConstant codeConstant => ResolveCodeModelConstant(constant: codeConstant, identifier: identifier),
            CodeModelEnumValue codeEnumValue => ResolveCodeModelEnumValue(enumValue: codeEnumValue, identifier: identifier),
            CodeModelDocComment codeDocComment => ResolveCodeModelDocComment(docComment: codeDocComment, identifier: identifier),
            CodeModelParameterComment codeParameterComment => ResolveCodeModelParameterComment(parameterComment: codeParameterComment, identifier: identifier),
            CodeModelTypeParameter codeTypeParameter => ResolveCodeModelTypeParameter(typeParameter: codeTypeParameter, identifier: identifier),
            CodeModelType codeType => ResolveCodeModelType(type: codeType, identifier: identifier),
            CodeModelAttribute codeAttribute => ResolveCodeModelAttribute(attribute: codeAttribute, identifier: identifier),
            CodeModelAttributeArgument codeArgument => ResolveCodeModelAttributeArgument(argument: codeArgument, identifier: identifier),
            TypeTemplateValue typeValue => ResolveTypeTemplateValue(typeValue: typeValue, identifier: identifier, state: state),
            TypeMetadataReference typeReference => ResolveTypeMetadataReference(typeReference: typeReference, identifier: identifier, state: state),
            EnumValueMetadata enumValue => ResolveEnumValue(enumValue: enumValue, identifier: identifier, state: state),
            AttributeMetadata attribute => ResolveAttribute(attribute: attribute, identifier: identifier),
            AttributeArgumentMetadata argument => ResolveAttributeArgument(argument: argument, identifier: identifier),
            string scalar => ResolveScalar(scalar: scalar, identifier: identifier),
            _ => Unresolved.Value,
        };

        return value is not Unresolved;
    }

    private object? ResolveTypeTemplateValue(
        TypeTemplateValue typeValue,
        string identifier,
        RenderState? state) =>
        ResolveTypeReference(
            type: typeValue.Reference,
            identifier: identifier,
            settings: typeValue.Settings,
            state: state,
            runtimeType: typeValue.RuntimeType);

    private object? ResolveTypeMetadataReference(
        TypeMetadataReference typeReference,
        string identifier,
        RenderState? state) =>
        ResolveTypeReference(
            type: typeReference,
            identifier: identifier,
            settings: null,
            state: state,
            runtimeType: FrontendRuntimeTypeKind.Auto);

#pragma warning disable CC0091,S2325
    private object ResolveProject(
        ProjectMetadata metadata,
        string identifier)
    {
        return identifier switch
        {
            "ProjectPath" => metadata.ProjectPath,
            "RootNamespace" => Path.GetFileNameWithoutExtension(path: metadata.ProjectPath),
            "rootnamespace" => Path.GetFileNameWithoutExtension(path: metadata.ProjectPath),
            "Types" => metadata.Types,
            "Classes" => metadata.Types.Where(predicate: type => type.Kind is TypeMetadataKind.Class or TypeMetadataKind.Record),
            "Records" => metadata.Types.Where(predicate: type => type.Kind == TypeMetadataKind.Record),
            "Structs" => metadata.Types.Where(predicate: type => type.Kind == TypeMetadataKind.Struct),
            "Delegates" => metadata.Delegates,
            "Interfaces" => metadata.Types.Where(predicate: type => type.Kind == TypeMetadataKind.Interface),
            "Enums" => metadata.Types.Where(predicate: type => type.Kind == TypeMetadataKind.Enum),
            _ => Unresolved.Value,
        };
    }
#pragma warning restore CC0091,S2325

#pragma warning disable CC0091,S2325
    private object? ResolveType(
        TypeMetadata type,
        string identifier,
        RenderState? state)
    {
        return identifier switch
        {
            "Name" => type.Name,
            "name" => ToCamelCase(value: type.Name),
            "FullName" => FormatGenericFullName(type: type),
            "Namespace" => type.Namespace,
            "Kind" => type.Kind.ToString(),
            "AssemblyName" => type.AssemblyName,
            "DocComment" => type.DocComment,
            "TypeParameters" => type.TypeParameters,
            "TypeArguments" => type.TypeArguments,
            "IsGeneric" => type.TypeParameters.Count > 0 || type.TypeArguments.Count > 0,
            "Properties" => type.Properties,
            "Methods" => type.Methods,
            "Constants" => type.Constants,
            "Fields" => type.Fields,
            "StaticReadOnlyFields" => type.StaticReadOnlyFields,
            "Events" => type.Events,
            "Delegates" => type.Delegates,
            "Attributes" => type.Attributes,
            "BaseType" => type.BaseTypes.FirstOrDefault(),
            "BaseClass" => type.BaseTypes.FirstOrDefault(),
            "BaseRecord" => type.BaseTypes.FirstOrDefault(),
            "BaseTypes" => type.BaseTypes,
            "ContainingClass" => ResolveContainingType(type: type, kind: TypeMetadataKind.Class, state: state),
            "ContainingRecord" => ResolveContainingType(type: type, kind: TypeMetadataKind.Record, state: state),
            "ContainingStruct" => ResolveContainingType(type: type, kind: TypeMetadataKind.Struct, state: state),
            "NestedClasses" => type.NestedClasses,
            "NestedRecords" => type.NestedRecords,
            "NestedStructs" => type.NestedStructs,
            "NestedEnums" => type.NestedEnums,
            "NestedInterfaces" => type.NestedInterfaces,
            "FileLocations" => type.FileLocations,
            "Values" => type.EnumValues,
            "EnumValues" => type.EnumValues,
            "IsClass" => type.Kind == TypeMetadataKind.Class,
            "IsRecord" => type.Kind == TypeMetadataKind.Record,
            "IsStruct" => type.Kind == TypeMetadataKind.Struct,
            "IsInterface" => type.Kind == TypeMetadataKind.Interface,
            "IsEnum" => type.Kind == TypeMetadataKind.Enum,
            "IsNullableAware" => type.IsNullableAware,
            "IsStatic" => type.IsStatic,
            "IsAbstract" => type.IsAbstract,
            _ => Unresolved.Value,
        };
    }
#pragma warning restore CC0091,S2325

    private object? ResolveProperty(
        PropertyMetadata property,
        string identifier,
        RenderState? state)
    {
        var runtimeType = FrontendRuntimeTypeResolver.Resolve(attributes: property.Attributes);
        return identifier switch
        {
            "Name" => property.Name,
            "name" => ToCamelCase(value: property.Name),
            "FullName" => property.FullName,
            "Parent" => state?.ResolveParentType(fullName: property.ParentTypeFullName) ?? Unresolved.Value,
            "Type" => CreateTypeTemplateValue(type: property.Type, state: state, runtimeType: runtimeType),
            "CSharpType" => property.Type.Name,
            "TypeScriptType" => MapType(type: property.Type, state: state, runtimeType: runtimeType),
            "IsNullable" => property.Type.IsNullable,
            "IsEnumerable" => property.Type.IsCollection,
            "IsPrimitive" => IsPrimitiveType(type: property.Type),
            "IsDate" => property.Type.IsDateLike,
            "HasGetter" => property.HasGetter,
            "HasSetter" => property.HasSetter,
            "IsRequired" => property.IsRequired,
            "IsIndexer" => property.IsIndexer,
            "IsAbstract" => property.IsAbstract,
            "IsVirtual" => property.IsVirtual,
            "Parameters" => property.Parameters,
            "DocComment" => property.DocComment,
            "Attributes" => property.Attributes,
            "Value" => property.Value,
            _ => Unresolved.Value,
        };
    }

    private object? ResolveMethod(
        MethodMetadata method,
        string identifier,
        RenderState? state)
    {
        return identifier switch
        {
            "Name" => method.Name,
            "name" => ToCamelCase(value: method.Name),
            "FullName" => method.FullName,
            "Parent" => state?.ResolveParentType(fullName: method.ParentTypeFullName) ?? Unresolved.Value,
            "Type" => CreateTypeTemplateValue(type: method.ReturnType, state: state),
            "ReturnType" => CreateTypeTemplateValue(type: method.ReturnType, state: state),
            "CSharpType" => method.ReturnType.Name,
            "TypeScriptType" => MapType(type: method.ReturnType, state: state),
            "Parameters" => method.Parameters,
            "Attributes" => method.Attributes,
            "IsStatic" => method.IsStatic,
            "IsAbstract" => method.IsAbstract,
            "IsGeneric" => method.IsGeneric,
            "DocComment" => method.DocComment,
            "TypeParameters" => method.TypeParameters,
            "ParentTypeFullName" => method.ParentTypeFullName,
            _ => Unresolved.Value,
        };
    }

    private object? ResolveParameter(
        ParameterMetadata parameter,
        string identifier,
        RenderState? state)
    {
        var runtimeType = FrontendRuntimeTypeResolver.Resolve(attributes: parameter.Attributes);
        return identifier switch
        {
            "Name" => parameter.Name,
            "name" => ToCamelCase(value: parameter.Name),
            "FullName" => parameter.FullName,
            "Parent" => !string.IsNullOrWhiteSpace(value: parameter.ParentMethodFullName)
                ? state?.ResolveParentMethod(fullName: parameter.ParentMethodFullName) ?? Unresolved.Value
                : state?.ResolveParentProperty(fullName: parameter.ParentPropertyFullName) ?? state?.CurrentParentContext ?? Unresolved.Value,
            "Type" => CreateTypeTemplateValue(type: parameter.Type, state: state, runtimeType: runtimeType),
            "CSharpType" => parameter.Type.Name,
            "TypeScriptType" => MapType(type: parameter.Type, state: state, runtimeType: runtimeType),
            "HasDefaultValue" => parameter.HasDefaultValue,
            "DefaultValue" => parameter.DefaultValue ?? string.Empty,
            "DocComment" => parameter.DocComment,
            "Attributes" => parameter.Attributes,
            "ParentMethodFullName" => parameter.ParentMethodFullName,
            "ParentPropertyFullName" => parameter.ParentPropertyFullName,
            _ => Unresolved.Value,
        };
    }

    private object? ResolveConstant(
        ConstantMetadata constant,
        string identifier,
        RenderState? state)
    {
        return identifier switch
        {
            "Name" => constant.Name,
            "name" => ToCamelCase(value: constant.Name),
            "FullName" => constant.FullName,
            "Parent" => state?.ResolveParentType(fullName: constant.ParentTypeFullName) ?? Unresolved.Value,
            "Type" => CreateTypeTemplateValue(type: constant.Type, state: state),
            "CSharpType" => constant.Type.Name,
            "TypeScriptType" => MapType(type: constant.Type, state: state),
            "Value" => constant.Value ?? string.Empty,
            "DocComment" => constant.DocComment,
            "Attributes" => constant.Attributes,
            "ParentTypeFullName" => constant.ParentTypeFullName,
            _ => Unresolved.Value,
        };
    }

    private object? ResolveField(
        FieldMetadata field,
        string identifier,
        RenderState? state)
    {
        var runtimeType = FrontendRuntimeTypeResolver.Resolve(attributes: field.Attributes);
        return identifier switch
        {
            "Name" => field.Name,
            "name" => ToCamelCase(value: field.Name),
            "FullName" => field.FullName,
            "AssemblyName" => field.AssemblyName,
            "Parent" => state?.ResolveParentType(fullName: field.ParentTypeFullName) ?? Unresolved.Value,
            "Type" => CreateTypeTemplateValue(type: field.Type, state: state, runtimeType: runtimeType),
            "CSharpType" => field.Type.Name,
            "TypeScriptType" => MapType(type: field.Type, state: state, runtimeType: runtimeType),
            "DocComment" => field.DocComment,
            "Attributes" => field.Attributes,
            "ParentTypeFullName" => field.ParentTypeFullName,
            "Value" => field.Value,
            _ => Unresolved.Value,
        };
    }

    private object? ResolveStaticReadOnlyField(
        StaticReadOnlyFieldMetadata field,
        string identifier,
        RenderState? state)
    {
        return identifier switch
        {
            "Name" => field.Name,
            "name" => ToCamelCase(value: field.Name),
            "FullName" => field.FullName,
            "AssemblyName" => field.AssemblyName,
            "Parent" => state?.ResolveParentType(fullName: field.ParentTypeFullName) ?? Unresolved.Value,
            "Type" => CreateTypeTemplateValue(type: field.Type, state: state),
            "CSharpType" => field.Type.Name,
            "TypeScriptType" => MapType(type: field.Type, state: state),
            "Value" => field.Value ?? string.Empty,
            "DocComment" => field.DocComment,
            "Attributes" => field.Attributes,
            "ParentTypeFullName" => field.ParentTypeFullName,
            _ => Unresolved.Value,
        };
    }

    private object? ResolveEvent(
        EventMetadata @event,
        string identifier,
        RenderState? state)
    {
        return identifier switch
        {
            "Name" => @event.Name,
            "name" => ToCamelCase(value: @event.Name),
            "FullName" => @event.FullName,
            "AssemblyName" => @event.AssemblyName,
            "Parent" => state?.ResolveParentType(fullName: @event.ParentTypeFullName) ?? Unresolved.Value,
            "Type" => CreateTypeTemplateValue(type: @event.Type, state: state),
            "CSharpType" => @event.Type.Name,
            "TypeScriptType" => MapType(type: @event.Type, state: state),
            "DocComment" => @event.DocComment,
            "Attributes" => @event.Attributes,
            "ParentTypeFullName" => @event.ParentTypeFullName,
            _ => Unresolved.Value,
        };
    }

    private object? ResolveDelegate(
        DelegateMetadata @delegate,
        string identifier,
        RenderState? state)
    {
        return identifier switch
        {
            "Name" => @delegate.Name,
            "name" => ToCamelCase(value: @delegate.Name),
            "FullName" => @delegate.FullName,
            "AssemblyName" => @delegate.AssemblyName,
            "Parent" => state?.ResolveParentType(fullName: @delegate.ParentTypeFullName) ?? Unresolved.Value,
            "Type" => CreateTypeTemplateValue(type: @delegate.ReturnType, state: state),
            "ReturnType" => CreateTypeTemplateValue(type: @delegate.ReturnType, state: state),
            "CSharpType" => @delegate.ReturnType.Name,
            "TypeScriptType" => MapType(type: @delegate.ReturnType, state: state),
            "Parameters" => @delegate.Parameters,
            "Attributes" => @delegate.Attributes,
            "DocComment" => @delegate.DocComment,
            "IsGeneric" => @delegate.IsGeneric,
            "TypeParameters" => @delegate.TypeParameters,
            "ParentTypeFullName" => @delegate.ParentTypeFullName,
            _ => Unresolved.Value,
        };
    }

    private TypeTemplateValue CreateTypeTemplateValue(
        TypeMetadataReference type,
        RenderState? state,
        FrontendRuntimeTypeKind runtimeType = FrontendRuntimeTypeKind.Auto) =>
        new(
            Reference: type,
            Text: MapType(type: type, state: state, runtimeType: runtimeType),
            Settings: state?.TemplateSettings,
            RuntimeType: runtimeType);

#pragma warning disable MA0051
    private object? ResolveTypeReference(
        TypeMetadataReference type,
        string identifier,
        Typewriter.Configuration.Settings? settings,
        RenderState? state,
        FrontendRuntimeTypeKind runtimeType)
    {
        var strictNull = ResolveStrictNull(settings: settings, state: state);
        var guidType = ResolveGuidType(settings: settings, state: state);
        var decimalType = ResolveDecimalType(settings: settings, state: state);
        return identifier switch
        {
            "Name" => GetLegacyTypeName(
                type: type,
                strictNull: strictNull,
                dateMapping: ResolveDateMapping(settings: settings, state: state),
                guidType: guidType,
                decimalType: decimalType,
                runtimeType: runtimeType),
            "name" => GetLegacyTypeName(
                type: type,
                strictNull: strictNull,
                dateMapping: ResolveDateMapping(settings: settings, state: state),
                guidType: guidType,
                decimalType: decimalType,
                runtimeType: runtimeType),
            "FullName" => type.FullName,
            "AssemblyName" => type.AssemblyName,
            "Namespace" => type.Namespace,
            "OriginalName" => type.Name,
            "Type" => new TypeTemplateValue(
                Reference: type,
                Text: _typeMapper.Map(
                    type: type,
                    strictNull: strictNull,
                    dateMapping: ResolveDateMapping(settings: settings, state: state),
                    decimalType: decimalType,
                    guidType: guidType,
                    runtimeType: runtimeType),
                Settings: settings,
                RuntimeType: runtimeType),
            "TypeScriptType" => _typeMapper.Map(
                type: type,
                strictNull: strictNull,
                dateMapping: ResolveDateMapping(settings: settings, state: state),
                decimalType: decimalType,
                guidType: guidType,
                runtimeType: runtimeType),
            "Class" => GetClassName(
                type: type,
                strictNull: strictNull,
                dateMapping: ResolveDateMapping(settings: settings, state: state),
                guidType: guidType,
                decimalType: decimalType,
                runtimeType: runtimeType),
            "ClassName" => GetClassName(
                type: type,
                strictNull: strictNull,
                dateMapping: ResolveDateMapping(settings: settings, state: state),
                guidType: guidType,
                decimalType: decimalType,
                runtimeType: runtimeType),
            "Default" => GetDefaultValue(type: type, state: state, settings: settings, runtimeType: runtimeType),
            "DefaultValue" => GetDefaultValue(type: type, state: state, settings: settings, runtimeType: runtimeType),
            "ElementType" => type.ElementType is null
                ? null
                : CreateTypeTemplateValue(type: type.ElementType, state: state, runtimeType: runtimeType),
            "Unwrap" => CreateTypeTemplateValue(
                type: type.ElementType ?? type.TypeArguments.FirstOrDefault() ?? type,
                state: state,
                runtimeType: runtimeType),
            "TypeArguments" => type.TypeArguments.Select(
                selector: argument => CreateTypeTemplateValue(type: argument, state: state, runtimeType: runtimeType)).ToArray(),
            "TupleElements" => type.TupleElements,
            "IsGeneric" => type.TypeArguments.Count > 0,
            "IsNullable" => type.IsNullable,
            "IsEnumerable" => type.IsCollection,
            "IsDictionary" => type.IsDictionary,
            "IsDynamic" => IsDynamicType(type: type),
            "IsEnum" => type.IsEnum,
            "IsPrimitive" => IsPrimitiveType(type: type),
            "IsStruct" => state is not null
                && state.TryResolveType(fullName: type.FullName, type: out var metadata)
                && metadata.Kind == TypeMetadataKind.Struct,
            "IsDate" => type.IsDateLike,
            "IsGuid" => type.FullName.Equals(value: "System.Guid", comparisonType: StringComparison.Ordinal),
            "IsTimeSpan" => type.FullName.Equals(value: "System.TimeSpan", comparisonType: StringComparison.Ordinal),
            "IsTask" => IsTaskLike(fullName: type.FullName),
            "IsValueTuple" => type.IsValueTuple,
            _ => Unresolved.Value,
        };
    }
#pragma warning restore MA0051

    private bool TryResolveNameCase(
        object context,
        string identifier,
        out object? value)
    {
        value = null;
        if (!TryResolveTemplateNameCase(identifier: identifier, nameCase: out var nameCase)
            || !TryGetContextName(context: context, name: out var name))
        {
            return false;
        }

        value = Typewriter.CodeModel.NameCaseFormatter.Format(value: name, nameCase: nameCase);
        return true;
    }

    private bool TryGetContextName(
        object context,
        out string name)
    {
        name = context switch
        {
            TypeMetadata type => type.Name,
            PropertyMetadata property => property.Name,
            MethodMetadata method => method.Name,
            ParameterMetadata parameter => parameter.Name,
            ConstantMetadata constant => constant.Name,
            FieldMetadata field => field.Name,
            StaticReadOnlyFieldMetadata field => field.Name,
            EventMetadata @event => @event.Name,
            DelegateMetadata @delegate => @delegate.Name,
            TypeParameterMetadata typeParameter => typeParameter.Name,
            TypeMetadataReference typeReference => GetLegacyTypeName(type: typeReference),
            TypeTemplateValue typeValue => GetLegacyTypeName(type: typeValue.Reference),
            EnumValueMetadata enumValue => enumValue.Name,
            AttributeMetadata attribute => attribute.Name,
            AttributeArgumentMetadata argument => argument.Name ?? string.Empty,
            CodeModelItem item => item.Name,
            string scalar => scalar,
            _ => string.Empty,
        };

        return context is TypeMetadata
            or PropertyMetadata
            or MethodMetadata
            or ParameterMetadata
            or ConstantMetadata
            or FieldMetadata
            or StaticReadOnlyFieldMetadata
            or EventMetadata
            or DelegateMetadata
            or TypeParameterMetadata
            or TypeMetadataReference
            or TypeTemplateValue
            or EnumValueMetadata
            or AttributeMetadata
            or AttributeArgumentMetadata
            or CodeModelItem
            or string;
    }

    private sealed class RenderState : IDisposable
    {
        private readonly ICollection<GenerationDiagnostic> _diagnostics;
        private readonly CompiledTemplateHelper? _compiledTemplateHelper;
        private readonly ProjectMetadata _metadata;
        private readonly IReadOnlyDictionary<string, MethodMetadata> _methodsByFullName;
        private readonly IReadOnlyDictionary<string, PropertyMetadata> _propertiesByFullName;
        private readonly IReadOnlyDictionary<string, TypeMetadata> _typesByFullName;
        private readonly Stack<object> _parentContexts = new();
        private readonly HashSet<string> _reportedCompiledInvocationErrors = [];
        private readonly HashSet<string> _reportedUnknownIdentifiers = [];
        private readonly IReadOnlyDictionary<string, TemplateCompatibilityMethod> _compatibilityMethods;
        private readonly string _templateContent;
        private readonly string _templatePath;
        private readonly TemplateRenderDefaults _defaults;

        public RenderState(
            TemplateDocument template,
            ProjectMetadata metadata,
            ICollection<GenerationDiagnostic> diagnostics,
            TemplateRenderDefaults defaults,
            CompiledTemplateFactory? compiledTemplateFactory,
            ProjectMetadataIndex? metadataIndex)
        {
            _metadata = metadata;
            _templatePath = template.Path;
            _templateContent = template.Content;
            _compatibilityMethods = template.CompatibilityMethods;
            _diagnostics = diagnostics;
            _defaults = defaults;
            var index = metadataIndex ?? ProjectMetadataIndex.Create(metadata: metadata);
            _typesByFullName = index.TypesByFullName;
            _methodsByFullName = index.MethodsByFullName;
            _propertiesByFullName = index.PropertiesByFullName;
            _compiledTemplateHelper = template.CodeBlocks.Count == 0
                ? null
                : compiledTemplateFactory?.CreateHelper(metadata: metadata, diagnostics: diagnostics, defaults: defaults, metadataIndex: index)
                  ?? TemplateRuntimeCompiler.Compile(template: template, metadata: metadata, diagnostics: diagnostics, defaults: defaults, metadataIndex: index);
        }

        public Typewriter.Configuration.Settings? TemplateSettings => _compiledTemplateHelper?.Settings;

        public bool StrictNullGeneration => TemplateSettings?.StrictNullGeneration ?? _defaults.StrictNullGeneration;

        public string DateInitializerGeneration => TemplateSettings?.DateInitializerGeneration ?? _defaults.DateInitializerGeneration;

        public string DateOnlyInitializerGeneration => TemplateSettings?.DateOnlyInitializerGeneration ?? _defaults.DateOnlyInitializerGeneration;

        public string TimeOnlyInitializerGeneration => TemplateSettings?.TimeOnlyInitializerGeneration ?? _defaults.TimeOnlyInitializerGeneration;

        public TypeScriptDateMapping DateMapping =>
            TemplateSettings?.GetDateMapping()
            ?? DateLibraryProfiles.GetMapping(
                library: _defaults.DateLibraryGeneration,
                dateType: _defaults.DateTypeGeneration,
                dateOnlyType: _defaults.DateOnlyTypeGeneration,
                timeOnlyType: _defaults.TimeOnlyTypeGeneration);

        public string GuidTypeGeneration => TemplateSettings?.GuidTypeGeneration ?? _defaults.GuidTypeGeneration;

        public string GuidInitializerGeneration => TemplateSettings?.GuidInitializerGeneration ?? _defaults.GuidInitializerGeneration;

        public string DecimalTypeGeneration => TemplateSettings?.DecimalTypeGeneration ?? _defaults.DecimalTypeGeneration;

        public string DecimalInitializerGeneration => TemplateSettings?.DecimalInitializerGeneration ?? _defaults.DecimalInitializerGeneration;

        public char StringLiteralCharacter => TemplateSettings?.StringLiteralCharacter ?? _defaults.StringLiteralCharacter;

        public bool UsesOutputFilenameFactory => _compiledTemplateHelper?.UsesOutputFilenameFactory == true;

        public int RootItemCount { get; private set; }

        public object? CurrentParentContext => _parentContexts.Count > 0 ? _parentContexts.Peek() : null;

        public string GetDateInitializer(DateSemanticKind kind)
        {
            if (TemplateSettings is not null)
            {
                return TemplateSettings.GetDateInitializer(kind: kind);
            }

            if (_defaults.DateLibraryGeneration != Typewriter.Configuration.DateLibrary.Legacy)
            {
                var profile = DateLibraryProfiles.Get(library: _defaults.DateLibraryGeneration);
                return kind switch
                {
                    DateSemanticKind.Instant => profile.InstantInitializer,
                    DateSemanticKind.PlainDate => profile.PlainDateInitializer,
                    DateSemanticKind.PlainTime => profile.PlainTimeInitializer,
                    DateSemanticKind.PlainDateTime => profile.PlainDateTimeInitializer,
                    DateSemanticKind.ZonedDateTime => profile.ZonedDateTimeInitializer,
                    DateSemanticKind.Duration => profile.DurationInitializer,
                    DateSemanticKind.Period => profile.PeriodInitializer,
                    DateSemanticKind.PlainYearMonth => profile.PlainYearMonthInitializer,
                    DateSemanticKind.PlainMonthDay => profile.PlainMonthDayInitializer,
                    _ => throw new ArgumentOutOfRangeException(paramName: nameof(kind), actualValue: kind, message: null),
                };
            }

            return kind switch
            {
                DateSemanticKind.PlainDate => _defaults.DateOnlyInitializerGeneration,
                DateSemanticKind.PlainTime => _defaults.TimeOnlyInitializerGeneration,
                DateSemanticKind.Duration => "\"00:00:00\"",
                DateSemanticKind.Period => "\"P0D\"",
                DateSemanticKind.PlainYearMonth or DateSemanticKind.PlainMonthDay => "\"\"",
                _ => _defaults.DateInitializerGeneration,
            };
        }

        public void AddRootItemCount(int count)
        {
            RootItemCount += count;
        }

        public void NotifyRenderComplete()
        {
            if (_compiledTemplateHelper is null
                || !_compiledTemplateHelper.TryInvokeRenderComplete(metadata: _metadata, error: out var error))
            {
                return;
            }

            if (error is not null)
            {
                AddCompiledInvocationError(methodName: "OnRenderComplete", message: error);
            }
        }

        public string? ResolveOutputPath()
        {
            if (_compiledTemplateHelper is null
                || RootItemCount == 0)
            {
                return null;
            }

            var outputPath = _compiledTemplateHelper.ResolveConfiguredOutputPath(metadata: _metadata, error: out var outputPathError);
            if (outputPathError is not null)
            {
                AddCompiledInvocationError(methodName: "OutputFilenameFactory", message: outputPathError);
            }

            return outputPath;
        }

        public object ResolveParentType(string fullName)
        {
            return !string.IsNullOrWhiteSpace(value: fullName)
                && _typesByFullName.TryGetValue(key: fullName, value: out var parent)
                    ? parent
                    : Unresolved.Value;
        }

        public object ResolveParentMethod(string fullName)
        {
            return !string.IsNullOrWhiteSpace(value: fullName)
                && _methodsByFullName.TryGetValue(key: fullName, value: out var parent)
                    ? parent
                    : Unresolved.Value;
        }

        public object ResolveParentProperty(string fullName)
        {
            return !string.IsNullOrWhiteSpace(value: fullName)
                && _propertiesByFullName.TryGetValue(key: fullName, value: out var parent)
                    ? parent
                    : Unresolved.Value;
        }

        public void PushParentContext(object context)
        {
            _parentContexts.Push(item: context);
        }

        public void PopParentContext()
        {
            _ = _parentContexts.Pop();
        }

        public bool HasCompiledMethod(string methodName)
        {
            return _compiledTemplateHelper?.HasMethod(methodName: methodName) == true;
        }

        public bool InvokeCompiledPredicate(
            string methodName,
            object context)
        {
            var invocationContext = NormalizeCompiledContext(context: context);
            if (_compiledTemplateHelper is null
                || !_compiledTemplateHelper.TryInvoke(methodName: methodName, context: invocationContext, value: out var value, error: out var error))
            {
                return false;
            }

            if (error is not null)
            {
                AddCompiledInvocationError(methodName: methodName, message: error);
                return false;
            }

            return value is bool boolValue && boolValue;
        }

        public bool InvokeCompiledPredicate(
            string methodName,
            object parentContext,
            object context)
        {
            var invocationParentContext = NormalizeCompiledContext(context: parentContext);
            var invocationContext = NormalizeCompiledContext(context: context);
            if (_compiledTemplateHelper is not null
                && _compiledTemplateHelper.TryInvoke(
                    methodName: methodName,
                    firstContext: invocationParentContext,
                    secondContext: invocationContext,
                    value: out var value,
                    error: out var error))
            {
                if (error is not null)
                {
                    AddCompiledInvocationError(methodName: methodName, message: error);
                    return false;
                }

                return value is bool boolValue && boolValue;
            }

            return InvokeCompiledPredicate(methodName: methodName, context: context);
        }

        public bool TryInvokeCompiledValue(
            string methodName,
            object context,
            out object? value)
        {
            value = null;
            if (_compiledTemplateHelper is null)
            {
                return false;
            }

            var invocationContext = NormalizeCompiledContext(context: context);
            var parentContext = CurrentParentContext;
            if (parentContext is not null
                && !ReferenceEquals(objA: parentContext, objB: context)
                && _compiledTemplateHelper.TryInvoke(
                    methodName: methodName,
                    firstContext: NormalizeCompiledContext(context: parentContext),
                    secondContext: invocationContext,
                    value: out value,
                    error: out var parentAwareError))
            {
                if (parentAwareError is not null)
                {
                    AddCompiledInvocationError(methodName: methodName, message: parentAwareError);
                    value = string.Empty;
                }

                return true;
            }

            if (!_compiledTemplateHelper.TryInvoke(methodName: methodName, context: invocationContext, value: out value, error: out var error))
            {
                return false;
            }

            if (error is not null)
            {
                AddCompiledInvocationError(methodName: methodName, message: error);
                value = string.Empty;
            }

            return true;
        }

        public bool TryGetCompatibilityMethod(
            string name,
            TemplateCompatibilityMethodKind kind,
            out TemplateCompatibilityMethod method)
        {
            return _compatibilityMethods.TryGetValue(key: name, value: out method!)
                && method.Kind == kind;
        }

        public bool TryResolveType(
            string fullName,
            out TypeMetadata type)
        {
            return _typesByFullName.TryGetValue(key: fullName, value: out type!);
        }

        public void AddUnknownIdentifier(
            string identifier,
            int index)
        {
            if (!_reportedUnknownIdentifiers.Add(item: identifier))
            {
                return;
            }

            var (line, column) = GetLineColumn(index: index);
            _diagnostics.Add(
                item: new GenerationDiagnostic(
                    File: _templatePath,
                    Line: line,
                    Column: column,
                    Severity: DiagnosticSeverity.Error,
                    Message: $"Unknown template member: {identifier}.",
                    Code: DiagnosticCodes.UnknownTemplateMember));
        }

        public void AddParseError(
            string identifier,
            int index,
            string message)
        {
            var (line, column) = GetLineColumn(index: index);
            _diagnostics.Add(
                item: new GenerationDiagnostic(
                    File: _templatePath,
                    Line: line,
                    Column: column,
                    Severity: DiagnosticSeverity.Error,
                    Message: $"{message} Identifier: {identifier}.",
                    Code: DiagnosticCodes.TemplateParseError));
        }

        public void Dispose()
        {
            _compiledTemplateHelper?.Dispose();
        }

        private static object NormalizeCompiledContext(object context) =>
            context is TypeTemplateValue typeTemplateValue
                ? new TypeMappingContext(
                    Reference: typeTemplateValue.Reference,
                    RuntimeType: typeTemplateValue.RuntimeType)
                : context;

        private static int? ReadTemplateLine(string message)
        {
            const string Marker = ":line ";
            var marker = message.IndexOf(value: Marker, comparisonType: StringComparison.Ordinal);
            if (marker < 0)
            {
                return null;
            }

            var start = marker + Marker.Length;
            var end = start;
            while (end < message.Length && char.IsAsciiDigit(c: message[index: end]))
            {
                end++;
            }

            return end > start
                   && int.TryParse(s: message[start..end], style: NumberStyles.Integer, provider: CultureInfo.InvariantCulture, result: out var line)
                ? line
                : null;
        }

        private void AddCompiledInvocationError(
            string methodName,
            string message)
        {
            if (!_reportedCompiledInvocationErrors.Add(item: methodName))
            {
                return;
            }

            _diagnostics.Add(
                item: new GenerationDiagnostic(
                    File: _templatePath,
                    Line: ReadTemplateLine(message: message),
                    Column: null,
                    Severity: DiagnosticSeverity.Error,
                    Message: $"Template C# helper '{methodName}' failed: {message}",
                    Code: DiagnosticCodes.TemplateParseError));
        }

        private (int Line, int Column) GetLineColumn(int index)
        {
            var line = 1;
            var column = 1;
            for (var cursor = 0; cursor < index && cursor < _templateContent.Length; cursor++)
            {
                if (_templateContent[index: cursor] == '\n')
                {
                    line++;
                    column = 1;
                }
                else
                {
                    column++;
                }
            }

            return (line, column);
        }
    }

    private sealed class Unresolved
    {
        public static Unresolved Value { get; } = new();
    }

    private sealed record TypeTemplateValue(
        TypeMetadataReference Reference,
        string Text,
        Typewriter.Configuration.Settings? Settings,
        FrontendRuntimeTypeKind RuntimeType)
    {
        public override string ToString() => Text;
    }
}
