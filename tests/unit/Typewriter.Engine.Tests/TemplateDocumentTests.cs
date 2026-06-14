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

        Assert.Empty(collection: diagnostics);
        Assert.Equal(expected: "index.d.ts", actual: document.OutputPath);
        Assert.Equal(expected: "export interface Marker {}", actual: document.Content.Trim());
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

        Assert.Empty(collection: diagnostics);
        Assert.Contains(expectedSubstring: "`${environment.apiBaseUrl}/api`", actualString: document.Content, comparisonType: StringComparison.Ordinal);
        Assert.DoesNotContain(expectedSubstring: "string Name", actualString: document.Content, comparisonType: StringComparison.Ordinal);
    }
}
