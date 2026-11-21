using System;
using System.Linq;
using AwesomeAssertions;
using Xunit;

namespace Typewriter.VisualStudio.Tests;

public sealed class CSharpBlockScannerAndCompletionTests
{
    [Theory]
    [InlineData("#if !TYPEWRITER\nstring A() => \"a\";\n#endif")]
    [InlineData("#if(!TYPEWRITER)\nstring A() => \"a\";\n#endif")]
    [InlineData("#if!TYPEWRITER\nstring A() => \"a\";\n#endif")]
    [InlineData("#if\t!TYPEWRITER\nstring A() => \"a\";\n#endif")]
    [InlineData("# if !TYPEWRITER\nstring A() => \"a\";\n#endif")]
    public void IsCSharpBlockRecognizesPreprocessorIfDirectiveVariations(string block)
    {
        CSharpBlockScanner.IsCSharpBlock(block: block, allowPartial: false).Should().BeTrue();
    }

    [Fact]
    public void IsCSharpBlockRejectsNonIfHashDirectivePrefix()
    {
        CSharpBlockScanner.IsCSharpBlock(block: "#ifdef FOO", allowPartial: false).Should().BeFalse();
    }

    [Fact]
    public void IsInsideCSharpBlockRecognizesInlinePreprocessorIfBlock()
    {
        const string template = "${ #if(!TYPEWRITER)\nstring A(Class c) => c.Name;\n#else\nstring B(Class c) => c.Name;\n#endif }\n$Classes[$B]";
        var markerIndex = template.IndexOf(value: "string B", comparisonType: StringComparison.Ordinal);

        CSharpBlockScanner.IsInsideCSharpBlock(text: template, position: markerIndex).Should().BeTrue();
    }

    [Fact]
    public void GetHelperNamesOnlyReturnsHelpersFromActiveTypewriterPreprocessorBranches()
    {
        const string template = """
            export const ignored = 'not a helper';
            ${ #if !TYPEWRITER
                public partial class _
                {
                    #if TYPEWRITER
                    string NestedDisabled(Class c) => c.Name;
                    #endif
                    string DisabledHelper(Class c) => c.Name;
                }
            #elif TYPEWRITER && !DEBUG
                string ActiveElifHelper(Class c) => c.Name;
            #else
                string DisabledElseHelper(Class c) => c.Name;
            #endif }
            ${
                #if(!TYPEWRITER)
                string SecondDisabledHelper(Class c) => c.Name;
                #else
                string SecondActiveHelper(Class c) => c.Name;
                #endif
            }
            $Classes[$ActiveElifHelper]
            """;

        var helpers = CSharpBlockScanner.GetHelperNames(text: template).ToArray();

        helpers.Should().Contain("ActiveElifHelper");
        helpers.Should().Contain("SecondActiveHelper");
        helpers.Should().NotContain("NestedDisabled");
        helpers.Should().NotContain("DisabledHelper");
        helpers.Should().NotContain("DisabledElseHelper");
        helpers.Should().NotContain("SecondDisabledHelper");
    }
}
