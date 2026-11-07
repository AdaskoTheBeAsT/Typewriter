using AwesomeAssertions;
using Xunit;

namespace Typewriter.Engine.Tests;

public sealed class GlobScalingTests
{
    [Fact]
    public void RepeatedWildcardsDoNotBacktrackExponentially()
    {
        var root = Path.GetFullPath(path: "glob-root");
        var pattern = string.Concat(values: Enumerable.Repeat(element: "*a", count: 24)) + "b.tst";
        var matcher = new PathGlobMatcher(root: root, pattern: pattern);
        var path = Path.Combine(path1: root, path2: new string(c: 'a', count: 48) + "c.tst");

        matcher.IsMatch(path: path).Should().BeFalse();
    }

    [Theory]
    [InlineData("**/*.tst", "one.tst", true)]
    [InlineData("**/*.tst", "one/two.TST", true)]
    [InlineData("*.tst", "one/two.tst", false)]
    [InlineData("**/Models/**", "Models/nested/one.cs", true)]
    [InlineData("a?c.tst", "abc.tst", true)]
    [InlineData("a?c.tst", "ac.tst", false)]
    [InlineData("*[x]*.tst", "a[x]b.tst", true)]
    [InlineData("*[x]*.tst", "axb.tst", false)]
    public void WildcardMatchingPreservesPathSemantics(string pattern, string relativePath, bool expected)
    {
        var root = Path.GetFullPath(path: "glob-root");
        var matcher = new PathGlobMatcher(root: root, pattern: pattern);

        matcher.IsMatch(path: Path.Combine(path1: root, path2: relativePath)).Should().Be(expected);
    }
}
