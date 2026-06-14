using System.Reflection;
using System.Runtime.Loader;

namespace Typewriter.Engine;

internal sealed class TemplateAssemblyLoadContext : AssemblyLoadContext
{
    private readonly IReadOnlyDictionary<string, string> _referencePaths;

    public TemplateAssemblyLoadContext(
        string name,
        IEnumerable<string?> referencePaths)
        : base(name: name, isCollectible: true)
    {
        _referencePaths = referencePaths
            .Where(predicate: path => !string.IsNullOrWhiteSpace(value: path) && File.Exists(path: path))
            .Select(selector: path => TryCreateReferencePath(path: path!))
            .Where(predicate: item => item is not null)
            .GroupBy(keySelector: item => item!.Value.Name, comparer: StringComparer.OrdinalIgnoreCase)
            .ToDictionary(keySelector: group => group.Key, elementSelector: group => group.First()!.Value.Path, comparer: StringComparer.OrdinalIgnoreCase);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var sharedAssembly = AssemblyLoadContext.Default.Assemblies.FirstOrDefault(
            predicate: assembly => AssemblyName.ReferenceMatchesDefinition(reference: assemblyName, definition: assembly.GetName()));
        if (sharedAssembly is not null)
        {
            return sharedAssembly;
        }

        if (assemblyName.Name is not null
            && _referencePaths.TryGetValue(key: assemblyName.Name, value: out var referencePath))
        {
            return LoadFromAssemblyPath(assemblyPath: referencePath);
        }

        return null;
    }

    private static (string Name, string Path)? TryCreateReferencePath(string path)
    {
        try
        {
            var assemblyName = AssemblyName.GetAssemblyName(assemblyFile: path).Name;
            return string.IsNullOrWhiteSpace(value: assemblyName)
                ? null
                : (assemblyName, path);
        }
        catch (BadImageFormatException)
        {
            return null;
        }
        catch (FileLoadException)
        {
            return null;
        }
        catch (FileNotFoundException)
        {
            return null;
        }
    }
}
