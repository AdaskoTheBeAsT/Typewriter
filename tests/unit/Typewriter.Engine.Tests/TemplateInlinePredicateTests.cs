using AwesomeAssertions;
using Typewriter.Abstractions;
using Xunit;

namespace Typewriter.Engine.Tests;

[Collection("Compiled template cache")]
public sealed class TemplateInlinePredicateTests
{
    [Theory]
    [InlineData("m => true", "Post;Get;")]
    [InlineData("m => false", "")]
    [InlineData("m => ReturnTrue(m)", "Post;Get;")]
    [InlineData("m => MethodType(m) == \"post\"", "Post;Get;")]
    [InlineData("m => MethodType(m) != \"get\"", "Post;Get;")]
    [InlineData("m => MethodType(m) == \"get\"", "")]
    [InlineData("m => MethodType(m) == m.Name.ToLowerInvariant()", "Post;")]
    [InlineData("m => MethodType(m).Equals(\"POST\", StringComparison.OrdinalIgnoreCase)", "Post;Get;")]
    [InlineData("m => MethodType(m) is \"post\" && m.Name.Length > 3", "Post;")]
    [InlineData("(Method m) => MethodType(m) == \"post\"", "Post;Get;")]
    [InlineData("(m => MethodType(m) == \"post\")", "Post;Get;")]
    [InlineData("m => { var verb = MethodType(m); return verb == \"post\"; }", "Post;Get;")]
    [InlineData("IsPost", "Post;Get;")]
    public void RenderCompilesInlineFiltersWithTemplateHelpers(string filter, string expected)
    {
        var template = $$"""
            ${
                bool ReturnTrue(Method m) => true;
                string MethodType(Method m) => "post";
                bool IsPost(Method m) => MethodType(m) == "post";
            }
            $Classes[$Methods({{filter}})[$Name;]]
            """;

        var (output, diagnostics) = Render(template: template);

        diagnostics.Should().BeEmpty();
        output.Trim().Should().Be(expected);
    }

    [Theory]
    [InlineData("m => m.Name.Length > 3", "Post;")]
    [InlineData("m => m.Name is \"Post\"", "Post;")]
    [InlineData("static m => m.Name == \"Get\"", "Get;")]
    [InlineData("m => new[] { \")\", \"]\", \"post\" }.Contains(\"post\")", "Post;Get;")]
    [InlineData("m => @\")\" == \")\" && \"\\\"[\".Length == 2", "Post;Get;")]
    [InlineData("m => /* ) ] */ m.Name == \"Post\"", "Post;")]
    public void RenderCompilesInlineFiltersWithoutCodeBlocks(string filter, string expected)
    {
        var document = new TemplateDocument(Path: "inline.tst", Content: $"$Classes[$Methods({filter})[$Name;]]", OutputPath: null);
        var diagnostics = new List<GenerationDiagnostic>();
        var renderer = new TemplateRenderer(typeMapper: new TypeScriptTypeMapper());

        var output = renderer.Render(template: document, metadata: CreateMetadata(), diagnostics: diagnostics);

        diagnostics.Should().BeEmpty();
        output.Trim().Should().Be(expected);
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void RenderCompilesMultilineFiltersWithEitherLineEnding(string lineEnding)
    {
        const string template = """
            ${
                string MethodType(Method m) => "post";
            }
            $Classes[$Methods(
                m => {
                    return MethodType(m) == "post";
                })[$Name;]]
            """;

        var (output, diagnostics) = Render(template: template.ReplaceLineEndings(replacementText: lineEnding));

        diagnostics.Should().BeEmpty();
        output.Trim().Should().Be("Post;Get;");
    }

    [Theory]
    [InlineData("// output:")]
    [InlineData("// typewriter-output:")]
    [InlineData("// typewriter-template:")]
    public void RenderDoesNotCompileFiltersInIgnoredDirectives(string directive)
    {
        var template = $$"""
            {{directive}} $Methods(m => MissingHelper(m))[ignored]
            $Classes[$Methods(m => m.Name.Length > 2)[$Name;]]
            """;

        var (output, diagnostics) = Render(template: template);

        diagnostics.Should().BeEmpty();
        output.Trim().Should().Be("Post;Get;");
    }

    [Fact]
    public void RenderCompilesNestedLinqPredicatesAndCapturesOuterParameters()
    {
        const string template = """
            ${
                string MethodType(Method m) => "post";
            }
            $Classes(c => c.Methods.Any(m => MethodType(m) == "post" && m.Parent.Name == c.Name))[$Name;]
            """;

        var (output, diagnostics) = Render(template: template);

        diagnostics.Should().BeEmpty();
        output.Trim().Should().Be("Endpoints;");
    }

    [Fact]
    public void RenderUsesTheSameHostForInlinePredicatesAndHelpers()
    {
        const string template = """
            ${
                int calls;
                string verb;
                Template(Settings settings) { verb = "post"; }
                string MethodType(Method m) { calls++; return verb; }
                string Calls(Class c) => calls.ToString();
            }
            $Classes[$Methods(m => MethodType(m) == "post")[$Name;]calls=$Calls]
            """;

        var (output, diagnostics) = Render(template: template);

        diagnostics.Should().BeEmpty();
        output.Trim().Should().Be("Post;Get;calls=2");
    }

    [Fact]
    public void RenderCompilesFiltersOnCustomHelperCollections()
    {
        const string template = """
            ${
                IEnumerable<Method> CustomMethods(Class c) => c.Methods;
                string MethodType(Method m) => "post";
            }
            $Classes[$CustomMethods(m => MethodType(m) == "post")[$Name;]]
            """;

        var (output, diagnostics) = Render(template: template);

        diagnostics.Should().BeEmpty();
        output.Trim().Should().Be("Post;Get;");
    }

    [Fact]
    public void RenderDistinguishesMethodParametersFromDocumentationParameters()
    {
        const string template = """
            $Classes[$Methods[$Parameters(p => p.Name == "id")[$Name;]$DocComment[$Parameters(p => p.Name == "id")[$Description;]]]]
            """;

        var (output, diagnostics) = Render(template: template);

        diagnostics.Should().BeEmpty();
        output.Trim().Should().Be("id;Identifier;");
    }

    [Fact]
    public void RenderCompilesFiltersOnTypeParameters()
    {
        var metadata = CreateMetadata();
        metadata = metadata with
        {
            Types = [metadata.Types[0] with { TypeParameters = [new TypeParameterMetadata(Name: "T"), new TypeParameterMetadata(Name: "TResult")] }],
        };

        var (output, diagnostics) = Render(template: "$Classes[$TypeParameters(t => t.Name.Length == 1)[$Name;]]", metadata: metadata);

        diagnostics.Should().BeEmpty();
        output.TrimEnd().Should().Be("T;");
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void RenderReportsInvalidInlinePredicateAtItsTemplateLocation(string lineEnding)
    {
        const string template = """
            ${
                string MethodType(Method m) => "post";
            }
            $Classes[$Methods(m => MissingHelper(m) == "post")[$Name;]]
            """;

        var (_, diagnostics) = Render(template: template.ReplaceLineEndings(replacementText: lineEnding));

        var diagnostic = diagnostics.Should().ContainSingle(predicate: diagnostic => diagnostic.Code == DiagnosticCodes.TemplateParseError).Which;
        diagnostic.File.Should().Be("inline.tst");
        diagnostic.Line.Should().Be(4);
        diagnostic.Column.Should().Be(template.Split(separator: '\n')[3].IndexOf(value: "MissingHelper", comparisonType: StringComparison.Ordinal) + 1);
        diagnostic.Message.Should().Contain("CS0103").And.Contain("MissingHelper");
    }

    [Fact]
    public void RenderPreservesPredicateLocationsWhenCloningADocument()
    {
        const string template = """
            ${
                string MethodType(Method m) => "post";
            }
            $Classes[$Methods(m => MissingHelper(m))[$Name;]]
            """;
        var diagnostics = new List<GenerationDiagnostic>();
        var original = TemplateDocument.Parse(template: new TemplateFile(Path: "inline.tst", Content: template), diagnostics: diagnostics);
        var document = original with { OutputPath = "cloned.ts" };
        var renderer = new TemplateRenderer(typeMapper: new TypeScriptTypeMapper());

        _ = renderer.Render(template: document, metadata: CreateMetadata(), diagnostics: diagnostics);

        diagnostics.Should().ContainSingle().Which.Line.Should().Be(4);
    }

    [Fact]
    public void RenderReportsInlinePredicateInvocationErrors()
    {
        const string template = """
            ${
                bool Fail(Method m) => throw new InvalidOperationException("bad predicate");
            }
            $Classes[$Methods(m => Fail(m))[$Name;]]
            """;

        var (output, diagnostics) = Render(template: template);

        output.Trim().Should().BeEmpty();
        diagnostics.Should().ContainSingle().Which.Message.Should().Contain("bad predicate");
    }

    [Fact]
    public void RenderCachesCompiledInlineFiltersAndIncludesTheirBodiesInTheCacheKey()
    {
        var path = $"inline-{Guid.NewGuid():N}.tst";
        var document = new TemplateDocument(Path: path, Content: "$Classes[$Methods(m => m.Name is \"Post\")[$Name;]]", OutputPath: null);
        var diagnostics = new List<GenerationDiagnostic>();
        var renderer = new TemplateRenderer(typeMapper: new TypeScriptTypeMapper());
        var hashCode = document.GetHashCode();

        renderer.Render(template: document, metadata: CreateMetadata(), diagnostics: diagnostics).Should().Be("Post;");
        renderer.Render(template: document, metadata: CreateMetadata(), diagnostics: diagnostics).Should().Be("Post;");
        TemplateRuntimeCompiler.GetCompileCacheMissCountForTests(template: document, diagnostics: diagnostics).Should().Be(1);
        document.GetHashCode().Should().Be(hashCode);
        var changed = document with { Content = document.Content.Replace(oldValue: "\"Post\"", newValue: "\"Get\"", comparisonType: StringComparison.Ordinal) };
        renderer.Render(template: changed, metadata: CreateMetadata(), diagnostics: diagnostics).Should().Be("Get;");
        TemplateRuntimeCompiler.GetCompileCacheMissCountForTests(template: changed, diagnostics: diagnostics).Should().Be(1);
        diagnostics.Should().BeEmpty();
    }

    private static (string Output, List<GenerationDiagnostic> Diagnostics) Render(
        string template,
        ProjectMetadata? metadata = null)
    {
        var diagnostics = new List<GenerationDiagnostic>();
        var document = TemplateDocument.Parse(template: new TemplateFile(Path: "inline.tst", Content: template), diagnostics: diagnostics);
        var renderer = new TemplateRenderer(typeMapper: new TypeScriptTypeMapper());
        var output = renderer.Render(template: document, metadata: metadata ?? CreateMetadata(), diagnostics: diagnostics);
        return (output, diagnostics);
    }

    private static ProjectMetadata CreateMetadata()
    {
        var returnType = new TypeMetadataReference(
            Name: "Void",
            FullName: "System.Void",
            Namespace: "System",
            IsNullable: false,
            IsCollection: false,
            IsDictionary: false,
            IsEnum: false,
            IsPrimitive: true,
            IsDateLike: false,
            ElementType: null,
            TypeArguments: []);
        var post = new MethodMetadata(
            Name: "Post",
            FullName: "Sample.Endpoints.Post",
            ReturnType: returnType,
            Accessibility: MetadataAccessibility.Public,
            IsStatic: false,
            IsAbstract: false,
            IsGeneric: false,
            Parameters:
            [
                new ParameterMetadata(
                    Name: "id",
                    FullName: "Sample.Endpoints.Post.id",
                    Type: returnType,
                    HasDefaultValue: false,
                    DefaultValue: null,
                    Attributes: [],
                    ParentMethodFullName: "Sample.Endpoints.Post"),
            ],
            Attributes: [],
            ParentTypeFullName: "Sample.Endpoints")
        {
            DocComment = new DocCommentMetadata(Summary: string.Empty, Returns: string.Empty, Parameters: [new ParameterCommentMetadata(Name: "id", Description: "Identifier")]),
        };
        var get = post with { Name = "Get", FullName = "Sample.Endpoints.Get", Parameters = [], DocComment = null };
        var type = new TypeMetadata(
            Name: "Endpoints",
            FullName: "Sample.Endpoints",
            Namespace: "Sample",
            Kind: TypeMetadataKind.Class,
            Accessibility: MetadataAccessibility.Public,
            Properties: [],
            Attributes: [],
            BaseTypes: [],
            EnumValues: [],
            IsNullableAware: true)
        {
            Methods = [post, get],
        };
        return new ProjectMetadata(ProjectPath: "Sample.csproj", SourceFiles: [], Types: [type], Diagnostics: []);
    }
}
