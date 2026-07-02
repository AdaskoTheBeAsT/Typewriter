using System.Text.Json;
using System.Text.Json.Serialization;
using Typewriter.Abstractions;
using Xunit;

namespace Typewriter.Cli.Tests;

public sealed class TypewriterConfigurationSchemaTests
{
    [Fact]
    public void SchemaFileDefinesExactlyTheConfigurationProperties()
    {
        var schemaPath = Path.Combine(path1: FindRepositoryRoot(), path2: "typewriter.schema.json");
        File.Exists(path: schemaPath).Should().BeTrue(because: $"the schema file should exist at {schemaPath}");

#pragma warning disable SEC0116
        var schemaText = File.ReadAllText(path: schemaPath);
#pragma warning restore SEC0116
        using var schemaDocument = JsonDocument.Parse(json: schemaText);
        var schemaRoot = schemaDocument.RootElement;
        schemaRoot.GetProperty(propertyName: "type").GetString().Should().Be("object");
        schemaRoot.GetProperty(propertyName: "additionalProperties").GetBoolean().Should().BeFalse();

        using var defaultDocument = JsonDocument.Parse(json: JsonSerializer.Serialize(value: TypewriterConfiguration.Default, options: CreateOptions()));

        AssertPropertiesMatch(
            schemaProperties: schemaRoot.GetProperty(propertyName: "properties"),
            configurationObject: defaultDocument.RootElement,
            ignoredSchemaProperties: ["$schema"]);
    }

    private static void AssertPropertiesMatch(
        JsonElement schemaProperties,
        JsonElement configurationObject,
        IReadOnlyCollection<string> ignoredSchemaProperties)
    {
        var schemaKeys = GetPropertyNames(element: schemaProperties)
            .Where(predicate: name => !ignoredSchemaProperties.Contains(value: name, comparer: StringComparer.Ordinal))
            .Order(comparer: StringComparer.Ordinal)
            .ToArray();
        var configurationKeys = GetPropertyNames(element: configurationObject)
            .Order(comparer: StringComparer.Ordinal)
            .ToArray();

        schemaKeys.Should().Equal(configurationKeys);

        using var configurationEnumerator = configurationObject.EnumerateObject();
        while (configurationEnumerator.MoveNext())
        {
            var property = configurationEnumerator.Current;
            var nestedSchema = schemaProperties.GetProperty(propertyName: property.Name);
            if (property.Value.ValueKind == JsonValueKind.Object)
            {
                nestedSchema.GetProperty(propertyName: "additionalProperties").GetBoolean().Should().BeFalse();
                AssertPropertiesMatch(
                    schemaProperties: nestedSchema.GetProperty(propertyName: "properties"),
                    configurationObject: property.Value,
                    ignoredSchemaProperties: []);
                continue;
            }

            nestedSchema.TryGetProperty(propertyName: "default", value: out var defaultValue)
                .Should().BeTrue(because: $"{property.Name} should document its runtime default");
            JsonElement.DeepEquals(element1: defaultValue, element2: property.Value)
                .Should().BeTrue(because: $"{property.Name} should have the same schema and runtime default");

            GetSchemaTypes(schema: nestedSchema)
                .Should().Contain(
                    expected: GetJsonType(value: property.Value),
                    because: $"{property.Name} should declare the runtime default's JSON type");

            if (nestedSchema.TryGetProperty(propertyName: "enum", value: out var enumValues))
            {
                var containsDefault = false;
                using var enumEnumerator = enumValues.EnumerateArray();
                while (enumEnumerator.MoveNext())
                {
                    if (JsonElement.DeepEquals(element1: enumEnumerator.Current, element2: property.Value))
                    {
                        containsDefault = true;
                        break;
                    }
                }

                containsDefault.Should().BeTrue(because: $"{property.Name}'s runtime default should be allowed by its schema enum");
            }
        }
    }

    private static IReadOnlyList<string> GetSchemaTypes(JsonElement schema)
    {
        var type = schema.GetProperty(propertyName: "type");
        if (type.ValueKind != JsonValueKind.Array)
        {
            return [type.GetString() ?? string.Empty];
        }

        var types = new List<string>();
        using var typeEnumerator = type.EnumerateArray();
        while (typeEnumerator.MoveNext())
        {
            types.Add(item: typeEnumerator.Current.GetString() ?? string.Empty);
        }

        return types;
    }

    private static string GetJsonType(JsonElement value) =>
        value.ValueKind switch
        {
            JsonValueKind.Array => "array",
            JsonValueKind.False or JsonValueKind.True => "boolean",
            JsonValueKind.Null => "null",
            JsonValueKind.Number when value.TryGetInt64(out _) => "integer",
            JsonValueKind.Number => "number",
            JsonValueKind.String => "string",
            _ => value.ValueKind.ToString().ToLowerInvariant(),
        };

    private static List<string> GetPropertyNames(JsonElement element)
    {
        var names = new List<string>();
        using var enumerator = element.EnumerateObject();
        while (enumerator.MoveNext())
        {
            names.Add(item: enumerator.Current.Name);
        }

        return names;
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };
        options.Converters.Add(item: new JsonStringEnumConverter(namingPolicy: JsonNamingPolicy.CamelCase));
        return options;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(path: AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(path: Path.Combine(path1: directory.FullName, path2: "AdaskoTheBeAsT.Typewriter.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return AppContext.BaseDirectory;
    }
}
