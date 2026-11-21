using AwesomeAssertions;
using Typewriter.Abstractions;
using Typewriter.Engine;
using Xunit;

namespace Typewriter.Engine.Tests;

public sealed class TemplateDocumentTests
{
    [Fact]
    public void ParseUsesSingleFileModeFromCompatibilityBlock()
    {
        const string content = """
            ${
                Template(Settings settings) {
                    settings.SingleFileMode("index.d.ts");
                }
            }
            export interface Marker {}
            """;
        var diagnostics = new List<GenerationDiagnostic>();

        var document = TemplateDocument.Parse(template: new TemplateFile(Path: "models.tst", Content: content), diagnostics: diagnostics);

        diagnostics.Should().BeEmpty();
        document.OutputPath.Should().Be("index.d.ts");
        document.Content.Trim().Should().Be("export interface Marker {}");
    }

    [Fact]
    public void ParseEvaluatesTypewriterPreprocessorSymbolForCodeBlocksAndSingleFileMode()
    {
        const string content = """
            ${
                #if !TYPEWRITER
                Template(Settings settings) {
                    settings.SingleFileMode("inactive.d.ts");
                }
                #else
                Template(Settings settings) {
                    settings.SingleFileMode("active.d.ts");
                }
                #endif
            }
            export interface Marker {}
            """;
        var diagnostics = new List<GenerationDiagnostic>();

        var document = TemplateDocument.Parse(template: new TemplateFile(Path: "models.tst", Content: content), diagnostics: diagnostics);

        diagnostics.Should().BeEmpty();
        document.CodeBlocks.Should().ContainSingle();
        document.OutputPath.Should().Be("active.d.ts");
        document.Content.Trim().Should().Be("export interface Marker {}");
    }

    [Fact]
    public void ParseEvaluatesInlineTypewriterPreprocessorDirectiveForSingleFileMode()
    {
        const string content = """
            ${ #if !TYPEWRITER
                Template(Settings settings) {
                    settings.SingleFileMode("inactive.d.ts");
                }
            #else
                Template(Settings settings) {
                    settings.SingleFileMode("active.d.ts");
                }
            #endif }
            export interface Marker {}
            """;
        var diagnostics = new List<GenerationDiagnostic>();

        var document = TemplateDocument.Parse(template: new TemplateFile(Path: "models.tst", Content: content), diagnostics: diagnostics);

        diagnostics.Should().BeEmpty();
        document.CodeBlocks.Should().ContainSingle();
        document.OutputPath.Should().Be("active.d.ts");
        document.Content.Trim().Should().Be("export interface Marker {}");
    }

    [Theory]
    [InlineData("#if !TYPEWRITER")]
    [InlineData("#if(!TYPEWRITER)")]
    [InlineData("#if!TYPEWRITER")]
    [InlineData("#if\t!TYPEWRITER")]
    [InlineData("# if !TYPEWRITER")]
    public void ParseRecognizesPreprocessorIfDirectiveVariationsInCodeBlocks(string ifDirective)
    {
        var content = $$"""
            ${
                {{ifDirective}}
                string Inactive(Class c) => c.Name;
                #else
                string Active(Class c) => c.Name;
                #endif
            }
            $Classes[$Active]
            """;
        var diagnostics = new List<GenerationDiagnostic>();

        var document = TemplateDocument.Parse(template: new TemplateFile(Path: "models.tst", Content: content), diagnostics: diagnostics);

        diagnostics.Should().BeEmpty();
        document.CodeBlocks.Should().ContainSingle();
        document.CompatibilityMethods.Should().ContainKey("Active");
        document.CompatibilityMethods.Should().NotContainKey("Inactive");
        document.Content.Trim().Should().Be("$Classes[$Active]");
    }

    [Fact]
    public void ParseDoesNotTreatNonIfHashPrefixAsCompatibilityCodeBlock()
    {
        const string content = "const x = `${#ifdef foo}`;";
        var diagnostics = new List<GenerationDiagnostic>();

        var document = TemplateDocument.Parse(template: new TemplateFile(Path: "models.tst", Content: content), diagnostics: diagnostics);

        diagnostics.Should().BeEmpty();
        document.CodeBlocks.Should().BeEmpty();
        document.Content.Trim().Should().Be(content);
    }

    [Fact]
    public void ParseKeepsTypeScriptTemplateLiteralInterpolation()
    {
        const string content = """
            ${
                string Name(Class c) => c.Name;
            }
            const getUrl = () => `${environment.apiBaseUrl}/api`;
            $Classes[export class $Name {}]
            """;
        var diagnostics = new List<GenerationDiagnostic>();

        var document = TemplateDocument.Parse(template: new TemplateFile(Path: "services.tst", Content: content), diagnostics: diagnostics);

        diagnostics.Should().BeEmpty();
        document.Content.Should().Contain("`${environment.apiBaseUrl}/api`");
        document.Content.Should().NotContain("string Name");
    }

    [Fact]
    public void ParseHandlesEscapedAndStringBracesInHelperCodeBlockIssue114()
    {
        const string content = """
            ${
                string ModelName(Class model) => model.Name;

                string Foo(Class model)
                {
                    var methodName = "foo";
                    var sb = new StringBuilder();
                    sb.AppendLine($"public {methodName}() {{");
                    sb.AppendLine("}");
                    return sb.ToString();
                }
            }
            $Classes[export class $ModelName {
                $Foo
            }]
            """;
        var diagnostics = new List<GenerationDiagnostic>();

        var document = TemplateDocument.Parse(template: new TemplateFile(Path: "models.tst", Content: content), diagnostics: diagnostics);

        diagnostics.Should().BeEmpty();
        document.CodeBlocks.Should().ContainSingle();
        document.CodeBlocks[0].Content.Should().Contain("sb.AppendLine($\"public {methodName}() {{\");");
        document.CodeBlocks[0].Content.Should().Contain("return sb.ToString();");
        document.Content.Trim().Should().Be("$Classes[export class $ModelName {\n    $Foo\n}]");
    }

    [Fact]
    public void ParseHandlesCommentsCharLiteralsVerbatimAndRawStringsWithBraces()
    {
        const string content = """"
            ${
                // Leading comment with unmatched } brace
                /* Block comment with {{ and }} */
                string Format(Class model)
                {
                    var openBrace = '{';
                    var closeBrace = '}';
                    var escapedQuote = '\'';
                    var verbatim = @"literal } and ""quoted }""";
                    var interpVerbatim = $@"prefix {{ {model.Name} }} suffix";
                    var altInterpVerbatim = @$"prefix {{ {new { X = 1 }.X} }} suffix";
                    var raw = """raw } brace""";
                    var rawInterp = $$"""raw {{ {{model.Name}} }}""";
                    return $"{openBrace}{verbatim}{interpVerbatim}{altInterpVerbatim}{raw}{rawInterp}{escapedQuote}{closeBrace}";
                }
            }
            $Classes[$Format]
            """";
        var diagnostics = new List<GenerationDiagnostic>();

        var document = TemplateDocument.Parse(template: new TemplateFile(Path: "models.tst", Content: content), diagnostics: diagnostics);

        diagnostics.Should().BeEmpty();
        document.CodeBlocks.Should().ContainSingle();
        document.Content.Trim().Should().Be("$Classes[$Format]");
    }

    [Fact]
    public void ParseReportsDiagnosticWhenCodeBlockIsUnclosedBecauseCloseBraceIsInsideString()
    {
        const string content = """
            ${
                string Foo(Class model)
                {
                    return "}";
            """;
        var diagnostics = new List<GenerationDiagnostic>();

        _ = TemplateDocument.Parse(template: new TemplateFile(Path: "models.tst", Content: content), diagnostics: diagnostics);

        var diagnostic = diagnostics.Should().ContainSingle(d => d.Code == "TW0002").Which;
        diagnostic.Message.Should().Contain("Template code block is not closed");
    }
}
