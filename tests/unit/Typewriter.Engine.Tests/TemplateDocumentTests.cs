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
}
