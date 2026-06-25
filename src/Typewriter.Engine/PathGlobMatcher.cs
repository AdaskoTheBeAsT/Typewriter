using System.Text;
using System.Text.RegularExpressions;

namespace Typewriter.Engine;

internal sealed class PathGlobMatcher
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(seconds: 5);
    private readonly string _root;
    private readonly string? _exactFullPath;
    private readonly string? _exactRelativePath;
    private readonly Regex? _regex;

    public PathGlobMatcher(
        string root,
        string pattern)
    {
        _root = Path.GetFullPath(path: root);
        var normalizedPattern = pattern.Replace(oldChar: '\\', newChar: '/');
        if (!normalizedPattern.Contains(value: '*', comparisonType: StringComparison.Ordinal)
            && !normalizedPattern.Contains(value: '?', comparisonType: StringComparison.Ordinal))
        {
            _exactRelativePath = normalizedPattern;
            _exactFullPath = Path.GetFullPath(path: Path.Combine(path1: _root, path2: normalizedPattern));
            return;
        }

        _regex = new Regex(
            pattern: ConvertGlobToRegex(pattern: normalizedPattern),
            options: RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled,
            matchTimeout: RegexTimeout);
    }

    public bool IsMatch(string path)
    {
        var relativePath = Path.GetRelativePath(relativeTo: _root, path: path).Replace(oldChar: '\\', newChar: '/');
        if (_regex is null)
        {
            return relativePath.Equals(value: _exactRelativePath, comparisonType: StringComparison.OrdinalIgnoreCase)
                || Path.GetFullPath(path: path).Equals(value: _exactFullPath, comparisonType: StringComparison.OrdinalIgnoreCase);
        }

        return _regex.IsMatch(input: relativePath);
    }

    private static string ConvertGlobToRegex(string pattern)
    {
        var builder = new StringBuilder(value: "^");
        var index = 0;
        while (index < pattern.Length)
        {
            var current = pattern[index: index];
            if (current == '*')
            {
                index = AppendStarPattern(pattern: pattern, index: index, builder: builder);
                continue;
            }

            _ = current == '?'
                ? builder.Append(value: "[^/]")
                : builder.Append(value: Regex.Escape(str: current.ToString()));
            index++;
        }

        builder.Append(value: '$');
        return builder.ToString();
    }

    private static int AppendStarPattern(
        string pattern,
        int index,
        StringBuilder builder)
    {
        if (index + 1 < pattern.Length && pattern[index: index + 1] == '*')
        {
            if (index + 2 < pattern.Length && pattern[index: index + 2] == '/')
            {
                builder.Append(value: "(?:.*/)?");
                return index + 3;
            }

            builder.Append(value: ".*");
            return index + 2;
        }

        builder.Append(value: "[^/]*");
        return index + 1;
    }
}
