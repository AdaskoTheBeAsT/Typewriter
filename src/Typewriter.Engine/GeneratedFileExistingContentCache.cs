using System.Runtime.CompilerServices;
using Typewriter.Abstractions;

namespace Typewriter.Engine;

internal static class GeneratedFileExistingContentCache
{
    private static readonly ConditionalWeakTable<GeneratedFile, ExistingContentHolder> Cache = [];

    public static void Set(
        GeneratedFile file,
        string content)
    {
        Cache.Remove(key: file);
        Cache.Add(key: file, value: new ExistingContentHolder(content: content));
    }

    public static bool TryGet(
        GeneratedFile file,
        out string content)
    {
        if (Cache.TryGetValue(key: file, value: out var holder))
        {
            content = holder.Content;
            return true;
        }

        content = string.Empty;
        return false;
    }

    private sealed class ExistingContentHolder
    {
        public ExistingContentHolder(string content)
        {
            Content = content;
        }

        public string Content { get; }
    }
}
