using System.Globalization;
using AwesomeAssertions;
using Typewriter.Abstractions;
using Xunit;

namespace Typewriter.Engine.Tests;

public sealed class TemplateHelperScalingTests
{
    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    [InlineData("\r")]
    public void LeadingSingleLineCommentsPreserveCompiledHelpers(string lineEnding)
    {
        var template = "${// comment" + lineEnding + "string Render(Class c) => \"ok\";}\n$Classes[$Render]";

        var output = Render(template: template, project: AdapterTestData.CreateProject(types: [AdapterTestData.CreateType()]));

        output.Trim().Should().Be("ok");
    }

    [Theory]
    [InlineData(TypeMetadataKind.Class, "Classes", "Class")]
    [InlineData(TypeMetadataKind.Record, "Records", "Record")]
    [InlineData(TypeMetadataKind.Struct, "Structs", "Struct")]
    [InlineData(TypeMetadataKind.Interface, "Interfaces", "Interface")]
    [InlineData(TypeMetadataKind.Class, "Classes", "Typewriter.CodeModel.Type")]
    public void ParentAwareScalarHelpersEnumerateMemberMetadataLinearly(TypeMetadataKind kind, string collection, string helperType)
    {
        var properties = new AdapterCountingList<PropertyMetadata>(
            items: Enumerable.Range(start: 0, count: 128)
                .Select(selector: index => CreateProperty(name: "Value" + index.ToString(provider: CultureInfo.InvariantCulture)))
                .ToArray());
        var declaration = AdapterTestData.CreateType() with { Kind = kind, Properties = properties };
        var template = $$"""
            ${
                string Qualified({{helperType}} c, Property p) => c.Name + "." + p.Name;
            }
            ${{collection}}[$Properties[$Qualified;]]
            """;

        var output = Render(template: template, project: AdapterTestData.CreateProject(types: [declaration]));

        output.Should().Contain("Model.Value0;").And.Contain("Model.Value127;");
        properties.Enumerations.Should().BeLessThanOrEqualTo(4);
    }

    [Theory]
    [InlineData("Methods", "Method")]
    [InlineData("Properties", "Property")]
    [InlineData("Delegates", "Typewriter.CodeModel.Delegate")]
    public void ParentAwareScalarHelpersDoNotRebuildParameterCollections(string collection, string helperType)
    {
        var parameters = new AdapterCountingList<ParameterMetadata>(
            items: Enumerable.Range(start: 0, count: 128)
                .Select(selector: index => new ParameterMetadata(
                    Name: "Argument" + index.ToString(provider: CultureInfo.InvariantCulture),
                    FullName: "Sample.Model.Member.Argument" + index.ToString(provider: CultureInfo.InvariantCulture),
                    Type: AdapterTestData.CreateReference(),
                    HasDefaultValue: false,
                    DefaultValue: null,
                    Attributes: [],
                    ParentMethodFullName: "Sample.Model.Member"))
                .ToArray());
        var method = new MethodMetadata(
            Name: "Member",
            FullName: "Sample.Model.Member",
            ReturnType: AdapterTestData.CreateReference(),
            Accessibility: MetadataAccessibility.Public,
            IsStatic: false,
            IsAbstract: false,
            IsGeneric: false,
            Parameters: parameters,
            Attributes: [],
            ParentTypeFullName: "Sample.Model");
        var @delegate = new DelegateMetadata(
            Name: "Member",
            FullName: "Sample.Model.Member",
            Accessibility: MetadataAccessibility.Public,
            ReturnType: AdapterTestData.CreateReference(),
            IsGeneric: false,
            Parameters: parameters,
            Attributes: [],
            ParentTypeFullName: "Sample.Model");
        var declaration = AdapterTestData.CreateType() with
        {
            Methods = collection == "Methods" ? [method] : [],
            Properties = collection == "Properties"
                ? [CreateProperty(name: "Member") with { IsIndexer = true, Parameters = parameters }]
                : [],
            Delegates = collection == "Delegates" ? [@delegate] : [],
        };
        var template = $$"""
            ${
                string Qualified({{helperType}} owner, Parameter p) => owner.Name + "." + p.Name;
            }
            $Classes[${{collection}}[$Parameters[$Qualified;]]]
            """;

        var output = Render(template: template, project: AdapterTestData.CreateProject(types: [declaration]));

        output.Should().Contain("Member.Argument0;").And.Contain("Member.Argument127;");
        parameters.Enumerations.Should().BeLessThanOrEqualTo(4);
    }

    [Fact]
    public void ParentAwareAttributeHelpersDoNotReformatUnusedArgumentGraphs()
    {
        var arguments = new AdapterCountingList<AttributeArgumentMetadata>(
            items: Enumerable.Range(start: 0, count: 128)
                .Select(selector: index => new AttributeArgumentMetadata(
                    Name: "Argument" + index.ToString(provider: CultureInfo.InvariantCulture),
                    Value: "text"))
                .ToArray());
        var declaration = AdapterTestData.CreateType() with
        {
            Attributes = [new AttributeMetadata(Name: "Example", FullName: "Sample.Example", Arguments: arguments)],
        };
        const string Template = """
            ${
                string Qualified(Typewriter.CodeModel.Attribute owner, AttributeArgument argument) => owner.Name + "." + argument.Name;
            }
            $Classes[$Attributes[$Arguments[$Qualified;]]]
            """;

        var output = Render(template: Template, project: AdapterTestData.CreateProject(types: [declaration]));

        output.Should().Contain("Example.Argument0;").And.Contain("Example.Argument127;");
        arguments.Enumerations.Should().BeLessThanOrEqualTo(4);
    }

    private static PropertyMetadata CreateProperty(string name) =>
        new(
            Name: name,
            FullName: "Sample.Model." + name,
            Type: AdapterTestData.CreateReference(),
            Accessibility: MetadataAccessibility.Public,
            HasGetter: true,
            HasSetter: true,
            IsRequired: false,
            Attributes: [])
        {
            ParentTypeFullName = "Sample.Model",
        };

    private static string Render(string template, ProjectMetadata project)
    {
        var diagnostics = new List<GenerationDiagnostic>();
        var renderer = new TemplateRenderer(typeMapper: new TypeScriptTypeMapper());
        var document = TemplateDocument.Parse(
            template: new TemplateFile(Path: "models.tst", Content: template),
            diagnostics: diagnostics);

        var output = renderer.Render(template: document, metadata: project, diagnostics: diagnostics);

        diagnostics.Should().BeEmpty();
        return output;
    }
}
