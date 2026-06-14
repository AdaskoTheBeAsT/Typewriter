namespace Typewriter.LanguageServer;

internal static class FileUriPath
{
    public static string? TryGetPath(string uri)
    {
        if (!Uri.TryCreate(uriString: uri, uriKind: UriKind.Absolute, result: out var parsedUri)
            || !parsedUri.IsFile)
        {
            return null;
        }

        return NormalizeLocalPath(path: parsedUri.LocalPath);
    }

    private static string NormalizeLocalPath(string path)
    {
        if (OperatingSystem.IsWindows()
            && path.Length >= 3
            && (path[index: 0] == '/' || path[index: 0] == '\\')
            && char.IsLetter(c: path[index: 1])
            && path[index: 2] == ':')
        {
            return path[1..];
        }

        return path;
    }
}
