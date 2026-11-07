using System.Reflection;
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

            // Own the image, not a file handle. Referenced DLLs must remain replaceable
            // while compilations, helpers, and evicted cache entries still use the image.
#pragma warning disable SCS0018,SEC0116 // Assembly paths are selected by the project's/template's reference resolver.
            using var stream = File.OpenRead(path: fullPath);
#pragma warning restore SCS0018,SEC0116
            reference = MetadataReference.CreateFromStream(peStream: stream, properties: properties, filePath: fullPath);
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

        public Lock Sync { get; } = new();

        public Dictionary<MetadataReferenceProperties, PortableExecutableReference> References { get; } = [];

        public string? Identity { get; set; }

        public void Validate(string path)
        {
#pragma warning disable SCS0018 // This is a stamp probe of a resolved assembly path, not a sandbox boundary.
            var file = new FileInfo(fileName: path);
#pragma warning restore SCS0018
            (long Length, long Modified)? stamp = file.Exists ? (file.Length, file.LastWriteTimeUtc.Ticks) : null;
            if (_stamp != stamp || stamp is null)
            {
                References.Clear();
                Identity = null;
                _stamp = stamp;
            }
        }
    }
}
