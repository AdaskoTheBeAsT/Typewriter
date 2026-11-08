using System.Reflection;
using System.Reflection.PortableExecutable;
using Microsoft.CodeAnalysis;

namespace Typewriter.CompilerServices;

/// <summary>
/// Linked into the Roslyn consumers, keeping their reference pools bounded without
/// adding a compiler dependency to Abstractions. Eviction never disposes live references.
/// </summary>
internal sealed class StampedAssemblyReferenceCache(int capacity = 1024)
{
    private readonly Lock _sync = new();
    private readonly Dictionary<string, Entry> _entries = new(comparer: StringComparer.OrdinalIgnoreCase);
    private readonly Queue<string> _order = new();

    public static StampedAssemblyReferenceCache Shared { get; } = new();

    internal int Count
    {
        get
        {
            lock (_sync)
            {
                return _entries.Count;
            }
        }
    }

    public PortableExecutableReference GetReference(string path, MetadataReferenceProperties properties = default)
    {
        var fullPath = Path.GetFullPath(path: path);
        var entry = GetEntry(path: fullPath);
        lock (entry.Sync)
        {
            entry.Validate(path: fullPath);
            if (entry.References.TryGetValue(key: properties, value: out var reference))
            {
                return reference;
            }

            reference = CreateReference(path: fullPath, properties: properties, entry: entry);
            entry.References.Add(key: properties, value: reference);
            return reference;
        }
    }

    public string GetAssemblyIdentityKey(string path)
    {
        var fullPath = Path.GetFullPath(path: path);
        var entry = GetEntry(path: fullPath);
        lock (entry.Sync)
        {
            entry.Validate(path: fullPath);
            if (entry.Identity is not null)
            {
                return entry.Identity;
            }

            try
            {
                var name = AssemblyName.GetAssemblyName(assemblyFile: fullPath).Name;
                if (!string.IsNullOrWhiteSpace(value: name))
                {
                    entry.Identity = "assembly:" + name;
                    return entry.Identity;
                }
            }
            catch (BadImageFormatException)
            {
                // Preserve the resolver's path fallback; a later repaired file is retried.
                return "path:" + fullPath;
            }
            catch (FileLoadException)
            {
                // Do not retain failed identity reads.
                return "path:" + fullPath;
            }
            catch (FileNotFoundException)
            {
                // Restore may create this file on the next request.
                return "path:" + fullPath;
            }

            return "path:" + fullPath;
        }
    }

    private static PortableExecutableReference CreateReference(string path, MetadataReferenceProperties properties, Entry entry)
    {
        // Own images, not file handles, including any modules linked by the manifest.
#pragma warning disable SCS0018,SEC0116 // Assembly paths are selected by the project's/template's reference resolver.
        using var stream = File.OpenRead(path: path);
#pragma warning restore SCS0018,SEC0116
        var reference = MetadataReference.CreateFromStream(peStream: stream, properties: properties, filePath: path);
        if (properties.Kind != MetadataImageKind.Assembly)
        {
            return reference;
        }

        string[] modulePaths;
        try
        {
            using var metadata = reference.GetMetadata();
            var names = ((AssemblyMetadata)metadata).GetModules()[0].GetModuleNames();
            if (names.IsEmpty)
            {
                return reference;
            }

            var directory = Path.GetDirectoryName(path: path)!;
            modulePaths = names.Select(selector: name => Path.Combine(path1: directory, path2: name)).ToArray();
        }
        catch (BadImageFormatException)
        {
            // Let Roslyn report invalid manifest images as compiler diagnostics.
            return reference;
        }

        entry.TrackModules(paths: modulePaths);
        return CreateLinkedReference(path: path, properties: properties, modulePaths: modulePaths);
    }

    private static PortableExecutableReference CreateLinkedReference(string path, MetadataReferenceProperties properties, string[] modulePaths)
    {
        var modules = new List<ModuleMetadata>();
        var transferred = false;
        try
        {
            modules.Add(item: ReadModule(path: path));
            foreach (var modulePath in modulePaths)
            {
                modules.Add(item: ReadModule(path: modulePath));
            }

#pragma warning disable CA2000,IDISP004 // The returned reference owns this metadata; eviction must not dispose live compilations.
            var reference = AssemblyMetadata.Create(modules: modules).GetReference(
                aliases: properties.Aliases,
                embedInteropTypes: properties.EmbedInteropTypes,
                filePath: path);
#pragma warning restore CA2000,IDISP004
            transferred = true;
            return reference;
        }
        catch (IOException exception)
        {
            return new FailedMetadataReference(path: path, properties: properties, error: exception);
        }
        catch (BadImageFormatException exception)
        {
            return new FailedMetadataReference(path: path, properties: properties, error: exception);
        }
        finally
        {
            if (!transferred)
            {
                foreach (var module in modules)
                {
                    module.Dispose();
                }
            }
        }
    }

    private static ModuleMetadata ReadModule(string path)
    {
#pragma warning disable SCS0018,SEC0116 // Module names are validated by Roslyn and resolved beside the selected manifest.
        using var stream = File.OpenRead(path: path);
#pragma warning restore SCS0018,SEC0116
        return ModuleMetadata.CreateFromStream(peStream: stream, options: PEStreamOptions.PrefetchMetadata);
    }

    private Entry GetEntry(string path)
    {
        lock (_sync)
        {
            if (_entries.TryGetValue(key: path, value: out var entry))
            {
                return entry;
            }

            entry = new Entry();

            // The lock covers only bookkeeping, never file IO or metadata loading.
            if (capacity > 0)
            {
                while (_entries.Count >= capacity)
                {
                    _entries.Remove(key: _order.Dequeue());
                }

                _entries.Add(key: path, value: entry);
                _order.Enqueue(item: path);
            }

            return entry;
        }
    }

    private sealed class Entry
    {
        private (long Length, long Modified)? _stamp;
        private Dictionary<string, (long Length, long Modified)?>? _moduleStamps;

        public Lock Sync { get; } = new();

        public Dictionary<MetadataReferenceProperties, PortableExecutableReference> References { get; } = [];

        public string? Identity { get; set; }

        public void TrackModules(string[] paths) =>
            _moduleStamps ??= paths.ToDictionary(keySelector: path => path, elementSelector: ReadStamp, comparer: StringComparer.OrdinalIgnoreCase);

        public void Validate(string path)
        {
            var stamp = ReadStamp(path: path);
            if (_stamp != stamp || stamp is null || !ModulesAreCurrent())
            {
                References.Clear();
                Identity = null;
                _stamp = stamp;
                _moduleStamps = null;
            }
        }

        private static (long Length, long Modified)? ReadStamp(string path)
        {
#pragma warning disable SCS0018 // This is a stamp probe of a resolved assembly path, not a sandbox boundary.
            var file = new FileInfo(fileName: path);
#pragma warning restore SCS0018
            return file.Exists ? (file.Length, file.LastWriteTimeUtc.Ticks) : null;
        }

        private bool ModulesAreCurrent()
        {
            if (_moduleStamps is not null)
            {
                foreach (var (path, stamp) in _moduleStamps)
                {
                    if (ReadStamp(path: path) != stamp)
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }

    private sealed class FailedMetadataReference(string path, MetadataReferenceProperties properties, Exception error)
        : PortableExecutableReference(properties: properties, fullPath: path)
    {
        protected override DocumentationProvider CreateDocumentationProvider() => DocumentationProvider.Default;

        protected override Metadata GetMetadataImpl() => throw error;

        protected override PortableExecutableReference WithPropertiesImpl(MetadataReferenceProperties properties) =>
            new FailedMetadataReference(path: FilePath!, properties: properties, error: error);
    }
}
