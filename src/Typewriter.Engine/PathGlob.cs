namespace Typewriter.Engine;

internal static class PathGlob
{
    public static PathGlobMatcher CreateMatcher(
        string root,
        string pattern) =>
        new(root: root, pattern: pattern);

    public static bool IsMatch(
        string root,
        string path,
        string pattern)
    {
        return CreateMatcher(root: root, pattern: pattern).IsMatch(path: path);
    }
}
