using System.Text.RegularExpressions;

namespace Typewriter.Engine;

internal static class PathGlob
{
    public static bool IsMatch(
        string root,
        string path,
        string pattern)
    {
        var relativePath = Path.GetRelativePath(relativeTo: root, path: path).Replace(oldChar: '\\', newChar: '/');
        var normalizedPattern = pattern.Replace(oldChar: '\\', newChar: '/');
        if (!normalizedPattern.Contains(value: '*', comparisonType: StringComparison.Ordinal)
            && !normalizedPattern.Contains(value: '?', comparisonType: StringComparison.Ordinal))
        {
            return relativePath.Equals(value: normalizedPattern, comparisonType: StringComparison.OrdinalIgnoreCase)
                || path.Equals(value: Path.GetFullPath(path: Path.Combine(path1: root, path2: normalizedPattern)), comparisonType: StringComparison.OrdinalIgnoreCase);
        }

        if (normalizedPattern.StartsWith(value: "**/", comparisonType: StringComparison.Ordinal)
            && IsMatch(root: root, path: path, pattern: normalizedPattern[3..]))
        {
            return true;
        }

        var regex = "^" + Regex.Escape(str: normalizedPattern)
            .Replace(oldValue: "\\*\\*", newValue: ".*", comparisonType: StringComparison.Ordinal)
            .Replace(oldValue: "\\*", newValue: "[^/]*", comparisonType: StringComparison.Ordinal)
            .Replace(oldValue: "\\?", newValue: ".", comparisonType: StringComparison.Ordinal) + "$";
        return Regex.IsMatch(input: relativePath, pattern: regex, options: RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeout: TimeSpan.FromSeconds(5));
    }
}
