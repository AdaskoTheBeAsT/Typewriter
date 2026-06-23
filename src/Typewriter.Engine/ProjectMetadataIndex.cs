using Typewriter.Abstractions;

namespace Typewriter.Engine;

internal sealed class ProjectMetadataIndex
{
    private ProjectMetadataIndex(
        IReadOnlyDictionary<string, TypeMetadata> typesByFullName,
        IReadOnlyDictionary<string, MethodMetadata> methodsByFullName,
        IReadOnlyDictionary<string, PropertyMetadata> propertiesByFullName)
    {
        TypesByFullName = typesByFullName;
        MethodsByFullName = methodsByFullName;
        PropertiesByFullName = propertiesByFullName;
    }

    public IReadOnlyDictionary<string, TypeMetadata> TypesByFullName { get; }

    public IReadOnlyDictionary<string, MethodMetadata> MethodsByFullName { get; }

    public IReadOnlyDictionary<string, PropertyMetadata> PropertiesByFullName { get; }

    public static ProjectMetadataIndex Create(ProjectMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(argument: metadata);

        return new ProjectMetadataIndex(
            typesByFullName: metadata.Types
                .GroupBy(keySelector: type => type.FullName, comparer: StringComparer.Ordinal)
                .ToDictionary(keySelector: group => group.Key, elementSelector: group => group.First(), comparer: StringComparer.Ordinal),
            methodsByFullName: metadata.Types
                .SelectMany(selector: type => type.Methods)
                .GroupBy(keySelector: method => method.FullName, comparer: StringComparer.Ordinal)
                .ToDictionary(keySelector: group => group.Key, elementSelector: group => group.First(), comparer: StringComparer.Ordinal),
            propertiesByFullName: metadata.Types
                .SelectMany(selector: type => type.Properties)
                .GroupBy(keySelector: property => property.FullName, comparer: StringComparer.Ordinal)
                .ToDictionary(keySelector: group => group.Key, elementSelector: group => group.First(), comparer: StringComparer.Ordinal));
    }
}
