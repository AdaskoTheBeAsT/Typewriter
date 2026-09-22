${
    using System.Globalization;
    using System.Text.RegularExpressions;
    using Typewriter.Extensions.Types;

    Template(Settings settings)
    {
        settings
            .IncludeCurrentProject()
            .IncludeReferencedProjects()
            .UseStringLiteralCharacter('\'')
            .OutputFilenameFactory = file =>
                (file.Classes.FirstOrDefault()?.Name ?? file.Records.FirstOrDefault()?.Name) + ".schema.ts"
            ;
    }

    bool IncludeClass(Class c)
    {
        return IsRecipeNamespace(c.Namespace)
            && c.Attributes.Any(p => p.Name == "GenerateFrontendType")
            && !c.IsGeneric
            && (c.BaseClass == null
                || (!c.BaseClass.Name.EndsWith("Controller")
                    && !c.BaseClass.Name.EndsWith("ControllerBase")));
    }

    bool IncludeRecord(Record r)
    {
        return IsRecipeNamespace(r.Namespace)
            && r.Attributes.Any(p => p.Name == "GenerateFrontendType")
            && !r.IsGeneric
            && (r.BaseRecord == null
                || (!r.BaseRecord.Name.EndsWith("Controller")
                    && !r.BaseRecord.Name.EndsWith("ControllerBase")));
    }

    bool IncludeProperty(Property property)
    {
        return !property.IsIndexer
            && property.HasGetter
            && !property.Attributes.Any(p => p.Name == "JsonIgnore");
    }

    bool IsRecipeNamespace(string value)
    {
        return value.StartsWith("AngularWebApiSample")
            || value.StartsWith("ReactWebApiSample");
    }

    string SchemaName(Class c) => c.Name + "Schema";
    string SchemaName(Record r) => r.Name + "Schema";

    string Quote(string value)
    {
        return "'" + (value ?? string.Empty)
            .Replace("\\", "\\\\")
            .Replace("'", "\\'")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n") + "'";
    }

    string TypeScriptType(Type type)
    {
        return string.IsNullOrWhiteSpace(type.Name) ? "unknown" : type.Name;
    }

    string CastSchema(string expression, Type type)
    {
        return $"({expression} as RuntimeSchema<{TypeScriptType(type)}>)";
    }

    string GetSerializedName(Property property)
    {
        var attribute = property.Attributes.FirstOrDefault(p => p.Name == "JsonPropertyName");
        var argument = attribute?.Arguments.FirstOrDefault(p => string.IsNullOrEmpty(p.Name));
        if (argument?.Value != null)
        {
            return argument.Value.ToString();
        }

        if (attribute != null && !string.IsNullOrWhiteSpace(attribute.Value))
        {
            var nameofMatch = Regex.Match(attribute.Value, @"nameof\s*[(]\s*([^)]+)\s*[)]");
            if (nameofMatch.Success)
            {
                return nameofMatch.Groups[1].Value.Trim();
            }

            var stringMatch = Regex.Match(attribute.Value, @"""([^""]*)""");
            if (stringMatch.Success)
            {
                return stringMatch.Groups[1].Value;
            }
        }

        return property.name;
    }

    bool IsPropertyRequired(Property property)
    {
        return property.IsRequired
            || property.Attributes.Any(p => p.Name == "JsonRequired");
    }

    string PropertyEntry(Property property)
    {
        var serializedName = GetSerializedName(property);
        var propertySchema = GetSchemaForProperty(property);
        if (!IsPropertyRequired(property))
        {
            propertySchema = $"schema.optional({propertySchema})";
        }

        var serializedArgument = serializedName == property.name
            ? string.Empty
            : $", {Quote(serializedName)}";
        return $"  {Quote(property.name)}: schema.property({propertySchema}{serializedArgument})";
    }

    string GetSchemaForProperty(Property property)
    {
        if (property.Attributes.Any(a => a.Name == "FrontendNoTransform"))
        {
            return CastSchema("schema.unknown()", property.Type);
        }

        var runtimeAttribute = property.Attributes.FirstOrDefault(a => a.Name == "FrontendRuntimeType");
        var runtimeType = GetRuntimeType(runtimeAttribute);
        var overridden = RuntimeTypeToSchema(runtimeType, runtimeAttribute, property.Type);
        if (overridden != null)
        {
            return property.Type.IsNullable
                ? $"schema.nullable({overridden})"
                : overridden;
        }

        return GetSchemaForType(property.Type);
    }

    string GetRuntimeType(Attribute attribute)
    {
        if (attribute == null)
        {
            return "Auto";
        }

        var argument = attribute.Arguments.FirstOrDefault(p => string.IsNullOrEmpty(p.Name));
        var value = argument?.Value?.ToString();
        if (string.IsNullOrWhiteSpace(value))
        {
            value = attribute.Value;
        }

        value = (value ?? string.Empty).Trim();
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numeric))
        {
            return numeric switch
            {
                1 => "Decimal",
                2 => "Uuid",
                3 => "Instant",
                4 => "PlainDate",
                5 => "PlainTime",
                6 => "PlainDateTime",
                7 => "ZonedDateTime",
                8 => "Duration",
                9 => "String",
                10 => "Period",
                11 => "PlainYearMonth",
                12 => "PlainMonthDay",
                _ => "Auto",
            };
        }

        var lastDot = value.LastIndexOf('.');
        if (lastDot >= 0)
        {
            value = value.Substring(lastDot + 1);
        }

        return value.StartsWith("Temporal", StringComparison.Ordinal)
            ? value.Substring("Temporal".Length)
            : value;
    }

    string GetNamedArgument(Attribute attribute, string name)
    {
        return attribute?.Arguments.FirstOrDefault(p => p.Name == name)?.Value?.ToString();
    }

    string RuntimeTypeToSchema(string runtimeType, Attribute attribute, Type type)
    {
        switch (runtimeType)
        {
            case "Decimal":
                var wireFormat = GetNamedArgument(attribute, "WireFormat");
                var wireType = string.Equals(wireFormat, "number", StringComparison.OrdinalIgnoreCase)
                    ? "number"
                    : "string";
                return CastSchema($"schema.decimal({Quote(wireType)})", type);
            case "Uuid":
                return CastSchema("schema.uuid()", type);
            case "Instant":
                return CastSchema("schema.instant()", type);
            case "PlainDate":
                return CastSchema("schema.plainDate()", type);
            case "PlainTime":
                return CastSchema("schema.plainTime()", type);
            case "PlainDateTime":
                return CastSchema("schema.plainDateTime()", type);
            case "ZonedDateTime":
                return CastSchema("schema.zonedDateTime()", type);
            case "Duration":
                return CastSchema("schema.duration()", type);
            case "Period":
                return CastSchema("schema.period()", type);
            case "PlainYearMonth":
                return CastSchema("schema.plainYearMonth()", type);
            case "PlainMonthDay":
                return CastSchema("schema.plainMonthDay()", type);
            case "String":
                return CastSchema("schema.string()", type);
            default:
                return null;
        }
    }

    string GetSchemaForType(Type type)
    {
        var core = GetSchemaForTypeCore(type);
        return type.IsNullable ? $"schema.nullable({core})" : core;
    }

    string GetSchemaForTypeCore(Type type)
    {
        if (type.IsGuid)
        {
            return CastSchema("schema.uuid()", type);
        }

        if (IsDecimal(type))
        {
            return CastSchema("schema.decimal('string')", type);
        }

        if (type.IsTimeSpan)
        {
            return CastSchema("schema.duration()", type);
        }

        if (type.IsDate)
        {
            return CastSchema(DateTypeToSchema(type), type);
        }

        if (type.IsEnum)
        {
            var expression = IsStringEnum(type) ? "schema.string()" : "schema.number()";
            return CastSchema(expression, type);
        }

        if (type.IsDictionary)
        {
            var valueType = type.TypeArguments.Count > 1 ? type.TypeArguments[1] : null;
            return valueType == null
                ? CastSchema("schema.record(schema.unknown())", type)
                : $"schema.record({GetSchemaForType(valueType)})";
        }

        if (type.IsEnumerable)
        {
            return type.ElementType == null
                ? CastSchema("schema.array(schema.unknown())", type)
                : $"schema.array({GetSchemaForType(type.ElementType)})";
        }

        if (type.IsPrimitive)
        {
            var original = type.OriginalName ?? string.Empty;
            if (original.Equals("String", StringComparison.OrdinalIgnoreCase))
            {
                return CastSchema("schema.string()", type);
            }

            if (original.Equals("Boolean", StringComparison.OrdinalIgnoreCase)
                || original.Equals("bool", StringComparison.OrdinalIgnoreCase))
            {
                return CastSchema("schema.boolean()", type);
            }

            return CastSchema("schema.number()", type);
        }

        if (!string.IsNullOrEmpty(type.FullName))
        {
            return $"schema.reference<{TypeScriptType(type)}>({Quote(type.FullName)})";
        }

        return CastSchema("schema.unknown()", type);
    }

    bool IsDecimal(Type type)
    {
        var original = type.OriginalName ?? string.Empty;
        return original.Equals("decimal", StringComparison.OrdinalIgnoreCase)
            || original.Equals("System.Decimal", StringComparison.Ordinal);
    }

    bool IsStringEnum(Type type)
    {
        return type.Attributes.Any(a => a.Name == "AsString")
            || type.Attributes.Any(a =>
                a.Name == "JsonConverter"
                && (a.Value.Contains("JsonStringEnumConverter")
                    || a.Value.Contains("StringEnumConverter")));
    }

    string DateTypeToSchema(Type type)
    {
        var original = type.OriginalName ?? string.Empty;
        return original switch
        {
            "DateTimeOffset" => "schema.instant()",
            "DateOnly" => "schema.plainDate()",
            "TimeOnly" => "schema.plainTime()",
            "Instant" => "schema.instant()",
            "LocalDate" => "schema.plainDate()",
            "LocalTime" => "schema.plainTime()",
            "LocalDateTime" => "schema.plainDateTime()",
            "ZonedDateTime" => "schema.zonedDateTime()",
            "Duration" => "schema.duration()",
            "Period" => "schema.period()",
            "YearMonth" => "schema.plainYearMonth()",
            "AnnualDate" => "schema.plainMonthDay()",
            _ => "schema.plainDateTime()",
        };
    }

    IEnumerable<Property> GetAllProperties(Class c)
    {
        var properties = new List<Property>();
        AddProperties(c, properties);
        return properties
            .Where(IncludeProperty)
            .GroupBy(p => p.Name)
            .Select(p => p.Last());
    }

    void AddProperties(Class c, IList<Property> properties)
    {
        if (c.BaseClass != null)
        {
            AddProperties(c.BaseClass, properties);
        }

        foreach (var property in c.Properties)
        {
            properties.Add(property);
        }
    }

    IEnumerable<Property> GetAllProperties(Record r)
    {
        var properties = new List<Property>();
        AddProperties(r, properties);
        return properties
            .Where(IncludeProperty)
            .GroupBy(p => p.Name)
            .Select(p => p.Last());
    }

    void AddProperties(Record r, IList<Property> properties)
    {
        if (r.BaseRecord != null)
        {
            AddProperties(r.BaseRecord, properties);
        }

        foreach (var property in r.Properties)
        {
            properties.Add(property);
        }
    }

    string GetObjectSchemaDefinition(Class c)
    {
        var entries = string.Join(",\n", GetAllProperties(c).Select(PropertyEntry));
        return $"schema.object<I{c.Name}>({{\n{entries}\n}})";
    }

    string GetObjectSchemaDefinition(Record r)
    {
        var entries = string.Join(",\n", GetAllProperties(r).Select(PropertyEntry));
        return $"schema.object<I{r.Name}>({{\n{entries}\n}})";
    }

    string GetDiscriminator(IAttributeCollection attributes)
    {
        var attribute = attributes.FirstOrDefault(p => p.Name == "JsonPolymorphic");
        var value = GetNamedArgument(attribute, "TypeDiscriminatorPropertyName");
        return string.IsNullOrWhiteSpace(value) ? "$type" : value;
    }

    bool HasPolymorphism(Class c)
    {
        return c.BaseClass == null && c.Attributes.Any(a => a.Name == "JsonDerivedType");
    }

    bool HasPolymorphism(Record r)
    {
        return r.BaseRecord == null && r.Attributes.Any(a => a.Name == "JsonDerivedType");
    }

    string GetDerivedTypeName(Attribute attribute)
    {
        var argument = attribute.Arguments.FirstOrDefault(p => string.IsNullOrEmpty(p.Name));
        if (argument?.TypeValue != null && !string.IsNullOrWhiteSpace(argument.TypeValue.FullName))
        {
            return argument.TypeValue.FullName;
        }

        var match = Regex.Match(attribute.Value ?? string.Empty, @"typeof\s*[(]\s*([^)\s]+)\s*[)]");
        return match.Success ? match.Groups[1].Value : string.Empty;
    }

    string GetDiscriminatorValue(Attribute attribute)
    {
        var arguments = attribute.Arguments.Where(p => string.IsNullOrEmpty(p.Name)).ToList();
        if (arguments.Count > 1 && arguments[1].Value != null)
        {
            return arguments[1].Value.ToString();
        }

        var stringMatch = Regex.Match(attribute.Value ?? string.Empty, @",\s*""([^""]*)""");
        if (stringMatch.Success)
        {
            return stringMatch.Groups[1].Value;
        }

        var numberMatch = Regex.Match(attribute.Value ?? string.Empty, @",\s*(-?\d+)\s*$");
        return numberMatch.Success ? numberMatch.Groups[1].Value : string.Empty;
    }

    string GetPolymorphismSchema(Class c)
    {
        var variants = new List<string>();
        foreach (var attribute in c.Attributes.Where(p => p.Name == "JsonDerivedType"))
        {
            var typeName = GetDerivedTypeName(attribute);
            var discriminatorValue = GetDiscriminatorValue(attribute);
            if (string.IsNullOrWhiteSpace(typeName) || string.IsNullOrWhiteSpace(discriminatorValue))
            {
                continue;
            }

            var variantSchema = typeName == c.FullName
                ? GetObjectSchemaDefinition(c)
                : $"schema.reference<I{c.Name}>({Quote(typeName)})";
            variants.Add($"  {Quote(discriminatorValue)}: {variantSchema}");
        }

        return $"schema.discriminatedUnion<I{c.Name}>({Quote(GetDiscriminator(c.Attributes))}, {{\n{string.Join(",\n", variants)}\n}})";
    }

    string GetPolymorphismSchema(Record r)
    {
        var variants = new List<string>();
        foreach (var attribute in r.Attributes.Where(p => p.Name == "JsonDerivedType"))
        {
            var typeName = GetDerivedTypeName(attribute);
            var discriminatorValue = GetDiscriminatorValue(attribute);
            if (string.IsNullOrWhiteSpace(typeName) || string.IsNullOrWhiteSpace(discriminatorValue))
            {
                continue;
            }

            var variantSchema = typeName == r.FullName
                ? GetObjectSchemaDefinition(r)
                : $"schema.reference<I{r.Name}>({Quote(typeName)})";
            variants.Add($"  {Quote(discriminatorValue)}: {variantSchema}");
        }

        return $"schema.discriminatedUnion<I{r.Name}>({Quote(GetDiscriminator(r.Attributes))}, {{\n{string.Join(",\n", variants)}\n}})";
    }

    string GetSchemaDefinition(Class c)
    {
        return HasPolymorphism(c) ? GetPolymorphismSchema(c) : GetObjectSchemaDefinition(c);
    }

    string GetSchemaDefinition(Record r)
    {
        return HasPolymorphism(r) ? GetPolymorphismSchema(r) : GetObjectSchemaDefinition(r);
    }

    string ReferenceImports(Class c)
    {
        return ReferenceImports(GetAllProperties(c), c.FullName);
    }

    string ReferenceImports(Record r)
    {
        return ReferenceImports(GetAllProperties(r), r.FullName);
    }

    string ReferenceImports(IEnumerable<Property> properties, string currentTypeName)
    {
        var imports = new SortedDictionary<string, string>();
        foreach (var property in properties)
        {
            AddReferenceImport(property.Type, currentTypeName, imports);
        }

        return string.Join("\n", imports.Values);
    }

    void AddReferenceImport(Type type, string currentTypeName, IDictionary<string, string> imports)
    {
        if (type.IsDictionary)
        {
            if (type.TypeArguments.Count > 1)
            {
                AddReferenceImport(type.TypeArguments[1], currentTypeName, imports);
            }
            return;
        }

        if (type.IsEnumerable)
        {
            if (type.ElementType != null)
            {
                AddReferenceImport(type.ElementType, currentTypeName, imports);
            }
            return;
        }

        if (type.IsPrimitive || type.IsDate || type.IsGuid || type.IsTimeSpan || IsDecimal(type))
        {
            return;
        }

        if (type.FullName == currentTypeName || !IsRecipeNamespace(type.Namespace))
        {
            return;
        }

        var typeName = TypeScriptType(type);
        imports[typeName] = $"import type {{ {typeName} }} from './{typeName}';";
    }
}
$Classes($IncludeClass)[
// This file has been AUTOGENERATED by TypeWriter (https://github.com/adaskothebeast/Typewriter).
// Do not modify it.
import { schema } from '@adaskothebeast/typewriter-schema';
import type { RuntimeSchema } from '@adaskothebeast/typewriter-schema';
import type { I$Name } from './$Name';
$ReferenceImports

export const $SchemaName = $GetSchemaDefinition;
]$Records($IncludeRecord)[
// This file has been AUTOGENERATED by TypeWriter (https://github.com/adaskothebeast/Typewriter).
// Do not modify it.
import { schema } from '@adaskothebeast/typewriter-schema';
import type { RuntimeSchema } from '@adaskothebeast/typewriter-schema';
import type { I$Name } from './$Name';
$ReferenceImports

export const $SchemaName = $GetSchemaDefinition;
]
