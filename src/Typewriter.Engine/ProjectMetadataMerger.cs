using Typewriter.Abstractions;

namespace Typewriter.Engine;

internal static class ProjectMetadataMerger
{
    public static ProjectMetadata Merge(
        ProjectMetadata project,
        IReadOnlyList<ProjectMetadata> includedProjects)
    {
        if (includedProjects.Count == 0)
        {
            return project;
        }

        var sourceFiles = project.SourceFiles
            .Concat(second: includedProjects.SelectMany(selector: included => included.SourceFiles))
            .GroupBy(keySelector: sourceFile => Path.GetFullPath(path: sourceFile.Path), comparer: StringComparer.OrdinalIgnoreCase)
            .Select(selector: group => group.First())
            .OrderBy(keySelector: sourceFile => sourceFile.Path, comparer: StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var types = project.Types
            .Concat(second: includedProjects.SelectMany(selector: included => included.Types))
            .GroupBy(keySelector: GetMetadataIdentity, comparer: StringComparer.Ordinal)
            .Select(selector: group => group.First())
            .OrderBy(keySelector: type => type.FullName, comparer: StringComparer.Ordinal)
            .ThenBy(keySelector: type => type.AssemblyName, comparer: StringComparer.Ordinal)
            .ToArray();
        var delegates = project.Delegates
            .Concat(second: includedProjects.SelectMany(selector: included => included.Delegates))
            .GroupBy(keySelector: GetMetadataIdentity, comparer: StringComparer.Ordinal)
            .Select(selector: group => group.First())
            .OrderBy(keySelector: type => type.FullName, comparer: StringComparer.Ordinal)
            .ThenBy(keySelector: type => type.AssemblyName, comparer: StringComparer.Ordinal)
            .ToArray();

        var referencedTypes = project.ReferencedTypes
            .Concat(second: includedProjects.SelectMany(selector: included => included.ReferencedTypes))
            .GroupBy(keySelector: GetReferencedTypeIdentity, comparer: StringComparer.Ordinal)
            .Select(selector: group => group.First())
            .OrderBy(keySelector: type => type.FullName, comparer: StringComparer.Ordinal)
            .ThenBy(keySelector: type => type.AssemblyName, comparer: StringComparer.Ordinal)
            .ToArray();

        return new ProjectMetadata(
            ProjectPath: project.ProjectPath,
            SourceFiles: sourceFiles,
            Types: types,
            Diagnostics: project.Diagnostics)
        {
            Delegates = delegates,
            ReferencedTypes = referencedTypes,
        };
    }

    private static string GetMetadataIdentity(TypeMetadata type) =>
        string.Concat(str0: type.FullName, str1: "\u001F", str2: type.AssemblyName);

    private static string GetMetadataIdentity(DelegateMetadata type) =>
        string.Concat(str0: type.FullName, str1: "\u001F", str2: type.AssemblyName);

    private static string GetReferencedTypeIdentity(TypeMetadata type) =>
        $"{type.FullName}\u001F{type.AssemblyName}\u001F{type.TypeParameters.Count.ToString(provider: System.Globalization.CultureInfo.InvariantCulture)}";
}
