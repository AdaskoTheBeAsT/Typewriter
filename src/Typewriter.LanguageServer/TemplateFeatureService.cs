using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Typewriter.Abstractions;
using Typewriter.Engine;
using Typewriter.Roslyn;

namespace Typewriter.LanguageServer;

internal sealed class TemplateFeatureService : IDisposable
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(seconds: 1);

    private static readonly string[] CSharpCompletionKeywords =
    [
        "bool",
        "break",
        "case",
        "class",
        "const",
        "continue",
        "else",
        "false",
        "foreach",
        "if",
        "int",
        "long",
        "new",
        "null",
        "private",
        "public",
        "return",
        "static",
        "string",
        "switch",
        "true",
        "typeof",
        "using",
        "var",
        "void",
    ];

    private static readonly string[] CSharpCodeModelTypes =
    [
        "Attribute",
        "AttributeArgument",
        "Class",
        "Constant",
        "Delegate",
        "Enum",
        "EnumValue",
        "Field",
        "File",
        "Interface",
        "Method",
        "Parameter",
        "Property",
        "Record",
        "Settings",
        "Type",
    ];

    private static readonly string[] TypeScriptCompletionKeywords =
    [
        "as",
        "async",
        "await",
        "boolean",
        "break",
        "case",
        "catch",
        "class",
        "const",
        "continue",
        "declare",
        "default",
        "delete",
        "do",
        "else",
        "enum",
        "export",
        "extends",
        "false",
        "finally",
        "for",
        "from",
        "function",
        "get",
        "if",
        "implements",
        "import",
        "in",
        "instanceof",
        "interface",
        "keyof",
        "let",
        "namespace",
        "new",
        "null",
        "number",
        "of",
        "private",
        "protected",
        "public",
        "readonly",
        "return",
        "satisfies",
        "set",
        "static",
        "string",
        "super",
        "switch",
        "this",
        "throw",
        "true",
        "try",
        "type",
        "typeof",
        "undefined",
        "var",
        "void",
        "while",
        "yield",
    ];

    private static readonly string[] TypeScriptBuiltInTypes =
    [
        "any",
        "Array",
        "bigint",
        "Date",
        "Map",
        "never",
        "object",
        "Omit",
        "Partial",
        "Pick",
        "Promise",
        "Readonly",
        "Record",
        "Required",
        "Set",
        "symbol",
        "unknown",
    ];

    private readonly EmbeddedCSharpLanguageService _embeddedCSharpService = new();

    public async Task<LspCompletionList> GetCompletionsAsync(
        TextDocumentState document,
        LanguageServerSettings settings,
        LspPosition position,
        CancellationToken cancellationToken)
    {
        var language = TemplateEmbeddedLanguage.GetKindAt(document: document, position: position);
        if (language == EmbeddedLanguageKind.CSharp)
        {
            var embedded = await _embeddedCSharpService.GetCompletionsAsync(
                document: document,
                templateOffset: document.GetOffset(position: position),
                cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
            if (embedded is not null && embedded.Items.Count > 0)
            {
                return new LspCompletionList(
                    IsIncomplete: embedded.IsIncomplete,
                    Items: MergeEmbeddedCSharpCompletions(embeddedItems: embedded.Items));
            }
        }

        var items = await GetAnalysisItemsAsync(document: document, settings: settings, cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        if (language == EmbeddedLanguageKind.CSharp)
        {
            return new LspCompletionList(IsIncomplete: false, Items: CreateCSharpCompletionItems(analysisItems: items));
        }

        if (language == EmbeddedLanguageKind.TypeScript)
        {
            return new LspCompletionList(IsIncomplete: false, Items: CreateTypeScriptCompletionItems(analysisItems: items));
        }

        var templateItems = items
            .GroupBy(keySelector: item => item.Label, comparer: StringComparer.OrdinalIgnoreCase)
            .Select(selector: group => group.First())
            .OrderBy(keySelector: item => item.Label, comparer: StringComparer.OrdinalIgnoreCase)
            .Select(
                selector: item => new LspCompletionItem(
                    Label: item.Label,
                    Kind: item.CompletionKind,
                    Detail: item.Detail,
                    Documentation: item.Documentation,
                    InsertText: item.InsertText,
                    InsertTextFormat: item.InsertTextFormat))
            .ToArray();
        return new LspCompletionList(IsIncomplete: false, Items: templateItems);
    }

    public async Task<LspHover?> GetHoverAsync(
        TextDocumentState document,
        LanguageServerSettings settings,
        LspPosition position,
        CancellationToken cancellationToken)
    {
        if (TemplateEmbeddedLanguage.GetKindAt(document: document, position: position) == EmbeddedLanguageKind.CSharp)
        {
            var embeddedHover = await _embeddedCSharpService.GetHoverAsync(
                document: document,
                templateOffset: document.GetOffset(position: position),
                cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
            if (embeddedHover is not null)
            {
                return embeddedHover;
            }
        }

        var token = document.GetToken(position: position);
        if (string.IsNullOrWhiteSpace(value: token.Text))
        {
            return null;
        }

        var items = await GetAnalysisItemsAsync(document: document, settings: settings, cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        var item = items.FirstOrDefault(predicate: candidate => MatchesToken(item: candidate, token: token.Text));
        if (item is null)
        {
            return null;
        }

        return new LspHover(
            Contents: new LspMarkupContent(Kind: "markdown", Value: CreateHoverMarkdown(item: item)),
            Range: CreateRange(document: document, start: token.Start, end: token.End));
    }

    public async Task<IReadOnlyList<LspLocation>> GetDefinitionsAsync(
        TextDocumentState document,
        LanguageServerSettings settings,
        LspPosition position,
        CancellationToken cancellationToken)
    {
        if (TemplateEmbeddedLanguage.GetKindAt(document: document, position: position) == EmbeddedLanguageKind.CSharp)
        {
            var embeddedDefinitions = await _embeddedCSharpService.GetDefinitionsAsync(
                document: document,
                templateOffset: document.GetOffset(position: position),
                cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
            if (embeddedDefinitions.Count > 0)
            {
                return embeddedDefinitions;
            }
        }

        var token = document.GetToken(position: position);
        var line = document.GetLine(position: position);
        if (IsOutputDirective(line: line))
        {
            var generatedLocations = await GetGeneratedFileLocationsAsync(
                document: document,
                settings: settings,
                cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
            if (generatedLocations.Count > 0)
            {
                return generatedLocations;
            }
        }

        if (string.IsNullOrWhiteSpace(value: token.Text))
        {
            return [];
        }

        var items = await GetAnalysisItemsAsync(document: document, settings: settings, cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        var locations = items
            .Where(predicate: item => ShouldIncludeDefinition(item: item, token: token))
            .Select(selector: item => item.Location)
            .OfType<SourceLocation>()
            .Select(selector: ToLspLocation)
            .GroupBy(keySelector: location => $"{location.Uri}:{location.Range.Start.Line.ToString(CultureInfo.InvariantCulture)}:{location.Range.Start.Character.ToString(CultureInfo.InvariantCulture)}", comparer: StringComparer.OrdinalIgnoreCase)
            .Select(selector: group => group.First())
            .ToArray();

        return locations;
    }

    public void Dispose() => _embeddedCSharpService.Dispose();

    private static void AddTemplateItems(ICollection<TemplateAnalysisItem> items)
    {
        AddTemplateMember(
            items: items,
            label: "Types",
            detail: "Collection",
            documentation: "All public/internal C# types available to this template.",
            insertText: "Types[$0]");
        AddTemplateMember(items: items, label: "Classes", detail: "Collection", documentation: "C# classes.", insertText: "Classes[$0]");
        AddTemplateMember(items: items, label: "Records", detail: "Collection", documentation: "C# records.", insertText: "Records[$0]");
        AddTemplateMember(items: items, label: "Interfaces", detail: "Collection", documentation: "C# interfaces.", insertText: "Interfaces[$0]");
        AddTemplateMember(items: items, label: "Enums", detail: "Collection", documentation: "C# enums.", insertText: "Enums[$0]");
        AddTemplateMember(items: items, label: "Properties", detail: "Collection", documentation: "Properties on the current type.", insertText: "Properties[$0]");
        AddTemplateMember(items: items, label: "Methods", detail: "Collection", documentation: "Methods on the current type.", insertText: "Methods[$0]");
        AddTemplateMember(items: items, label: "Parameters", detail: "Collection", documentation: "Parameters on the current method.", insertText: "Parameters[$0]");
        AddTemplateMember(items: items, label: "Constants", detail: "Collection", documentation: "Constants on the current type.", insertText: "Constants[$0]");
        AddTemplateMember(items: items, label: "Attributes", detail: "Collection", documentation: "Attributes on the current metadata item.", insertText: "Attributes[$0]");
        AddTemplateMember(items: items, label: "Arguments", detail: "Collection", documentation: "Arguments on the current attribute.", insertText: "Arguments[$0]");
        AddTemplateMember(items: items, label: "Values", detail: "Collection", documentation: "Values on the current enum.", insertText: "Values[$0]");
        AddTemplateMember(items: items, label: "EnumValues", detail: "Collection", documentation: "Values on the current enum.", insertText: "EnumValues[$0]");
        AddTemplateMember(items: items, label: "TypeArguments", detail: "Collection", documentation: "Generic type arguments for the current type reference.", insertText: "TypeArguments[$0]");

        foreach (var scalar in CreateScalarItems())
        {
            items.Add(item: scalar);
        }

        foreach (var filter in CreateFilterItems())
        {
            items.Add(item: filter);
        }
    }

    private static IEnumerable<TemplateAnalysisItem> CreateScalarItems()
    {
        yield return Scalar(label: "Name", documentation: "Current item name.");
        yield return Scalar(label: "name", documentation: "Current item name with camel-case formatting.");
        yield return Scalar(label: "FullName", documentation: "Fully-qualified metadata name.");
        yield return Scalar(label: "Namespace", documentation: "Containing namespace.");
        yield return Scalar(label: "Type", documentation: "TypeScript rendering of the current item's type.");
        yield return Scalar(label: "ReturnType", documentation: "Method return type.");
        yield return Scalar(label: "Value", documentation: "Constant, enum, or attribute argument value.");
        yield return Scalar(label: "DefaultValue", documentation: "Parameter default value.");
        yield return Scalar(label: "Parent", documentation: "Parent metadata object.");
        yield return Scalar(label: "IsNullable", documentation: "Whether the current type reference or property is nullable.");
        yield return Scalar(label: "IsStatic", documentation: "Whether the current type/member is static.");
        yield return Scalar(label: "IsAbstract", documentation: "Whether the current method is abstract.");
        yield return Scalar(label: "IsGeneric", documentation: "Whether the current method or type reference is generic.");
        yield return Scalar(label: "HasGetter", documentation: "Whether the current property has a getter.");
        yield return Scalar(label: "HasSetter", documentation: "Whether the current property has a setter.");
        yield return Scalar(label: "IsRequired", documentation: "Whether the current property is required.");
        yield return Scalar(label: "HasDefaultValue", documentation: "Whether the current parameter has a default value.");
        yield return Scalar(label: "IsCollection", documentation: "Whether the current type reference is a collection.");
        yield return Scalar(label: "IsDictionary", documentation: "Whether the current type reference is a dictionary.");
        yield return Scalar(label: "IsEnum", documentation: "Whether the current type reference is an enum.");
        yield return Scalar(label: "IsPrimitive", documentation: "Whether the current type reference is primitive.");
        yield return Scalar(label: "IsDateLike", documentation: "Whether the current type reference represents date/time data.");
        yield return Scalar(label: "Default", documentation: "Default TypeScript value for the current type reference.");
        yield return Scalar(label: "OriginalName", documentation: "Original C# type name.");
        yield return Scalar(label: "ClassName", documentation: "TypeScript-friendly class name.");
        yield return Scalar(label: "TypeParameters", documentation: "Legacy type parameter placeholder.");
    }

    private static IEnumerable<TemplateAnalysisItem> CreateFilterItems()
    {
        yield return Filter(label: "Class", documentation: "Keep only classes.");
        yield return Filter(label: "Record", documentation: "Keep only records.");
        yield return Filter(label: "Interface", documentation: "Keep only interfaces.");
        yield return Filter(label: "Enum", documentation: "Keep only enums.");
        yield return Filter(label: "HasProperties", documentation: "Keep types with properties.");
        yield return Filter(label: "HasMethods", documentation: "Keep types with methods.");
        yield return Filter(label: "HasConstants", documentation: "Keep types with constants.");
        yield return Filter(label: "Static", documentation: "Keep static types.");
        yield return Filter(label: "NonStatic", documentation: "Keep non-static types.");
        yield return Filter(label: "Public", documentation: "Keep public metadata items.");
        yield return Filter(label: "Internal", documentation: "Keep internal metadata items.");
        yield return Filter(label: "Name=", documentation: "Filter by metadata name.");
        yield return Filter(label: "Namespace=", documentation: "Filter by namespace.");
        yield return Filter(label: "[Attribute]", documentation: "Filter by attribute name.");
        yield return Filter(label: ":BaseType", documentation: "Filter by base type or implemented interface.");
    }

    private static TemplateAnalysisItem Scalar(
        string label,
        string documentation) =>
        new(
            Label: label,
            CompletionKind: CompletionKind.Property,
            Detail: "Template member",
            Documentation: documentation,
            TargetKind: TemplateAnalysisTargetKind.TemplateMember);

    private static TemplateAnalysisItem Filter(
        string label,
        string documentation) =>
        new(
            Label: label,
            CompletionKind: CompletionKind.Keyword,
            Detail: "Template filter",
            Documentation: documentation,
            TargetKind: TemplateAnalysisTargetKind.Filter);

    private static void AddTemplateMember(
        ICollection<TemplateAnalysisItem> items,
        string label,
        string detail,
        string documentation,
        string insertText)
    {
        items.Add(
            item: new TemplateAnalysisItem(
                Label: label,
                CompletionKind: CompletionKind.Snippet,
                Detail: detail,
                Documentation: documentation,
                InsertText: insertText,
                InsertTextFormat: InsertTextFormat.Snippet,
                TargetKind: TemplateAnalysisTargetKind.TemplateMember));
    }

    private static void AddHelperItems(
        ICollection<TemplateAnalysisItem> items,
        string content)
    {
        foreach (Match match in Regex.Matches(
                     input: content,
                     pattern: @"(?:public\s+|private\s+|internal\s+|protected\s+|static\s+)*[A-Za-z_][A-Za-z0-9_<>,\.\s\?]*\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\(",
                     options: RegexOptions.CultureInvariant,
                     matchTimeout: RegexTimeout))
        {
            var name = match.Groups[groupname: "name"].Value;
            if (name.Equals(value: "Template", comparisonType: StringComparison.Ordinal)
                || name.Equals(value: "if", comparisonType: StringComparison.Ordinal)
                || name.Equals(value: "for", comparisonType: StringComparison.Ordinal)
                || name.Equals(value: "foreach", comparisonType: StringComparison.Ordinal)
                || name.Equals(value: "while", comparisonType: StringComparison.Ordinal)
                || name.Equals(value: "switch", comparisonType: StringComparison.Ordinal))
            {
                continue;
            }

            items.Add(
                item: new TemplateAnalysisItem(
                    Label: name,
                    CompletionKind: CompletionKind.Function,
                    Detail: "Template helper",
                    Documentation: "Helper method declared in this .tst template.",
                    TargetKind: TemplateAnalysisTargetKind.Helper));
        }
    }

    private static IReadOnlyList<LspCompletionItem> MergeEmbeddedCSharpCompletions(
        IReadOnlyList<LspCompletionItem> embeddedItems)
    {
        var items = new List<LspCompletionItem>(collection: embeddedItems);
        foreach (var keyword in CSharpCompletionKeywords)
        {
            items.Add(
                item: new LspCompletionItem(
                    Label: keyword,
                    Kind: CompletionKind.Keyword,
                    Detail: "C# keyword",
                    Documentation: "Keyword available inside a Typewriter C# helper block."));
        }

        foreach (var typeName in CSharpCodeModelTypes)
        {
            items.Add(
                item: new LspCompletionItem(
                    Label: typeName,
                    Kind: CompletionKind.Class,
                    Detail: "Typewriter.CodeModel type",
                    Documentation: "CodeModel type available to compiled Typewriter helper methods."));
        }

        return DeduplicateCompletions(items: items);
    }

    private static IReadOnlyList<LspCompletionItem> CreateCSharpCompletionItems(
        IReadOnlyList<TemplateAnalysisItem> analysisItems)
    {
        var items = new List<LspCompletionItem>();
        foreach (var keyword in CSharpCompletionKeywords)
        {
            items.Add(
                item: new LspCompletionItem(
                    Label: keyword,
                    Kind: CompletionKind.Keyword,
                    Detail: "C# keyword",
                    Documentation: "Keyword available inside a Typewriter C# helper block."));
        }

        foreach (var typeName in CSharpCodeModelTypes)
        {
            items.Add(
                item: new LspCompletionItem(
                    Label: typeName,
                    Kind: CompletionKind.Class,
                    Detail: "Typewriter.CodeModel type",
                    Documentation: "CodeModel type available to compiled Typewriter helper methods."));
        }

        foreach (var helper in analysisItems.Where(predicate: item => item.TargetKind == TemplateAnalysisTargetKind.Helper))
        {
            items.Add(
                item: new LspCompletionItem(
                    Label: helper.Label,
                    Kind: CompletionKind.Function,
                    Detail: helper.Detail,
                    Documentation: helper.Documentation));
        }

        return DeduplicateCompletions(items: items);
    }

    private static IReadOnlyList<LspCompletionItem> CreateTypeScriptCompletionItems(
        IReadOnlyList<TemplateAnalysisItem> analysisItems)
    {
        var items = new List<LspCompletionItem>();
        AddTypewriterSnippetItems(items: items);

        foreach (var keyword in TypeScriptCompletionKeywords)
        {
            items.Add(
                item: new LspCompletionItem(
                    Label: keyword,
                    Kind: CompletionKind.Keyword,
                    Detail: "TypeScript keyword",
                    Documentation: "Keyword available inside generated TypeScript output text."));
        }

        foreach (var typeName in TypeScriptBuiltInTypes)
        {
            items.Add(
                item: new LspCompletionItem(
                    Label: typeName,
                    Kind: CompletionKind.Class,
                    Detail: "TypeScript type",
                    Documentation: "Built-in or utility type available inside generated TypeScript output text."));
        }

        foreach (var type in analysisItems.Where(predicate: item => item.TargetKind == TemplateAnalysisTargetKind.Type))
        {
            items.Add(
                item: new LspCompletionItem(
                    Label: type.Label,
                    Kind: type.CompletionKind,
                    Detail: type.Detail,
                    Documentation: type.Documentation));
        }

        return DeduplicateCompletions(items: items);
    }

    private static void AddTypewriterSnippetItems(ICollection<LspCompletionItem> items)
    {
        AddTypewriterSnippet(
            items: items,
            label: "$Types",
            insertText: "Types[$0]",
            documentation: "All public/internal C# types available to this template.");
        AddTypewriterSnippet(items: items, label: "$Classes", insertText: "Classes[$0]", documentation: "C# classes.");
        AddTypewriterSnippet(items: items, label: "$Records", insertText: "Records[$0]", documentation: "C# records.");
        AddTypewriterSnippet(items: items, label: "$Interfaces", insertText: "Interfaces[$0]", documentation: "C# interfaces.");
        AddTypewriterSnippet(items: items, label: "$Enums", insertText: "Enums[$0]", documentation: "C# enums.");
        AddTypewriterSnippet(items: items, label: "$Properties", insertText: "Properties[$0]", documentation: "Properties on the current type.");
        AddTypewriterSnippet(items: items, label: "$Methods", insertText: "Methods[$0]", documentation: "Methods on the current type.");
        AddTypewriterSnippet(items: items, label: "$Parameters", insertText: "Parameters[$0]", documentation: "Parameters on the current method.");
        AddTypewriterSnippet(items: items, label: "$Constants", insertText: "Constants[$0]", documentation: "Constants on the current type.");
        AddTypewriterSnippet(items: items, label: "$Name", insertText: "Name", documentation: "Current item name.");
        AddTypewriterSnippet(items: items, label: "$name", insertText: "name", documentation: "Current item name with camel-case formatting.");
        AddTypewriterSnippet(items: items, label: "$Type", insertText: "Type", documentation: "TypeScript rendering of the current item's type.");
        AddTypewriterSnippet(items: items, label: "$Default", insertText: "Default", documentation: "Default TypeScript value for the current type reference.");
    }

    private static void AddTypewriterSnippet(
        ICollection<LspCompletionItem> items,
        string label,
        string insertText,
        string documentation)
    {
        items.Add(
            item: new LspCompletionItem(
                Label: label,
                Kind: CompletionKind.Snippet,
                Detail: "Typewriter template expression",
                Documentation: documentation,
                InsertText: "$" + insertText,
                InsertTextFormat: InsertTextFormat.Snippet));
    }

    private static IReadOnlyList<LspCompletionItem> DeduplicateCompletions(
        IEnumerable<LspCompletionItem> items) =>
        items
            .GroupBy(keySelector: item => item.Label, comparer: StringComparer.Ordinal)
            .Select(selector: group => group.First())
            .OrderBy(keySelector: item => item.Label, comparer: StringComparer.OrdinalIgnoreCase)
            .ToArray();

#pragma warning disable MA0051 // Method is too long
    private static void AddMetadataItems(
        ICollection<TemplateAnalysisItem> items,
        ProjectMetadata project)
#pragma warning restore MA0051 // Method is too long
    {
        foreach (var type in project.Types)
        {
            items.Add(
                item: new TemplateAnalysisItem(
                    Label: type.Name,
                    CompletionKind: GetTypeCompletionKind(type: type),
                    Detail: $"{type.Kind} {type.FullName}",
                    Documentation: type.Documentation ?? $"C# {type.Kind.ToString().ToLowerInvariant()} {type.FullName}.",
                    Location: type.Location,
                    TargetKind: TemplateAnalysisTargetKind.Type));
            items.Add(
                item: new TemplateAnalysisItem(
                    Label: type.FullName,
                    CompletionKind: GetTypeCompletionKind(type: type),
                    Detail: $"{type.Kind} {type.FullName}",
                    Documentation: type.Documentation ?? $"C# {type.Kind.ToString().ToLowerInvariant()} {type.FullName}.",
                    Location: type.Location,
                    TargetKind: TemplateAnalysisTargetKind.Type));

            foreach (var property in type.Properties)
            {
                AddMember(
                    items: items,
                    label: property.Name,
                    completionKind: CompletionKind.Property,
                    detail: $"Property {type.Name}.{property.Name}: {property.Type.Name}",
                    documentation: property.Documentation ?? $"C# property {property.FullName}.",
                    location: property.Location,
                    targetKind: TemplateAnalysisTargetKind.Property);
            }

            foreach (var method in type.Methods)
            {
                AddMember(
                    items: items,
                    label: method.Name,
                    completionKind: CompletionKind.Method,
                    detail: $"Method {type.Name}.{method.Name}(): {method.ReturnType.Name}",
                    documentation: method.Documentation ?? $"C# method {method.FullName}.",
                    location: method.Location,
                    targetKind: TemplateAnalysisTargetKind.Method);

                foreach (var parameter in method.Parameters)
                {
                    AddMember(
                        items: items,
                        label: parameter.Name,
                        completionKind: CompletionKind.Variable,
                        detail: $"Parameter {method.Name}.{parameter.Name}: {parameter.Type.Name}",
                        documentation: parameter.Documentation ?? $"C# parameter {parameter.FullName}.",
                        location: parameter.Location,
                        targetKind: TemplateAnalysisTargetKind.Parameter);
                }
            }

            foreach (var constant in type.Constants)
            {
                AddMember(
                    items: items,
                    label: constant.Name,
                    completionKind: CompletionKind.Constant,
                    detail: $"Constant {type.Name}.{constant.Name}: {constant.Type.Name}",
                    documentation: constant.Documentation ?? $"C# constant {constant.FullName}.",
                    location: constant.Location,
                    targetKind: TemplateAnalysisTargetKind.Constant);
            }

            foreach (var enumValue in type.EnumValues)
            {
                AddMember(
                    items: items,
                    label: enumValue.Name,
                    completionKind: CompletionKind.EnumMember,
                    detail: $"Enum value {type.Name}.{enumValue.Name}",
                    documentation: enumValue.Documentation ?? $"C# enum value {type.FullName}.{enumValue.Name}.",
                    location: enumValue.Location,
                    targetKind: TemplateAnalysisTargetKind.EnumValue);
            }
        }
    }

    private static void AddMember(
        ICollection<TemplateAnalysisItem> items,
        string label,
        int completionKind,
        string detail,
        string documentation,
        SourceLocation? location,
        TemplateAnalysisTargetKind targetKind)
    {
        items.Add(
            item: new TemplateAnalysisItem(
                Label: label,
                CompletionKind: completionKind,
                Detail: detail,
                Documentation: documentation,
                Location: location,
                TargetKind: targetKind));
    }

    private static IReadOnlyList<string> ResolveProjectPaths(
        LanguageServerSettings settings,
        string workspacePath,
        string templatePath)
    {
        var configuredProjectPath = settings.ResolveProjectPath(workspacePath: workspacePath);
        if (!string.IsNullOrWhiteSpace(value: configuredProjectPath))
        {
            return [configuredProjectPath];
        }

        var root = Path.GetFullPath(path: workspacePath);
        if (File.Exists(path: root) && root.EndsWith(value: ".csproj", comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            return [root];
        }

        var projects = FindProjectPaths(root: root);
        if (projects.Length == 1)
        {
            return [projects[0]];
        }

        if (projects.Length > 1 && settings.AllProjects)
        {
            return projects;
        }

        var nearestProjectPath = FindNearestAncestorProjectPath(
            templatePath: templatePath,
            workspacePath: root,
            projectPaths: projects);
        return !string.IsNullOrWhiteSpace(value: nearestProjectPath)
            ? [nearestProjectPath]
            : [];
    }

    private static string? FindNearestAncestorProjectPath(
        string? templatePath,
        string workspacePath,
        IEnumerable<string> projectPaths)
    {
        if (string.IsNullOrWhiteSpace(value: templatePath))
        {
            return null;
        }

        var projectPathSet = projectPaths
            .Select(selector: Path.GetFullPath)
            .ToHashSet(comparer: StringComparer.OrdinalIgnoreCase);
        var workspaceRoot = GetProjectSearchRoot(workspacePath: workspacePath);
        var directory = Path.GetDirectoryName(path: Path.GetFullPath(path: templatePath));
        while (!string.IsNullOrWhiteSpace(value: directory)
            && IsSameOrChildPath(path: directory, root: workspaceRoot))
        {
            if (Directory.Exists(path: directory))
            {
                var projectPath = Directory
                    .EnumerateFiles(path: directory, searchPattern: "*.csproj", searchOption: SearchOption.TopDirectoryOnly)
                    .Select(selector: Path.GetFullPath)
                    .Where(predicate: projectPathSet.Contains)
                    .Order(comparer: StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(value: projectPath))
                {
                    return projectPath;
                }
            }

            if (PathEquals(left: directory, right: workspaceRoot))
            {
                return null;
            }

            directory = Directory.GetParent(path: directory)?.FullName;
        }

        return null;
    }

    private static string GetProjectSearchRoot(string workspacePath)
    {
        var root = Path.GetFullPath(path: workspacePath);
        return File.Exists(path: root)
            ? Path.GetDirectoryName(path: root) ?? root
            : root;
    }

    private static bool IsSameOrChildPath(
        string path,
        string root)
    {
        var relativePath = Path.GetRelativePath(relativeTo: root, path: path);
        return string.Equals(a: relativePath, b: ".", comparisonType: StringComparison.Ordinal)
            || (!relativePath.StartsWith(value: "..", comparisonType: StringComparison.Ordinal)
                && !Path.IsPathRooted(path: relativePath));
    }

    private static bool PathEquals(
        string left,
        string right) =>
        string.Equals(
            a: Path.TrimEndingDirectorySeparator(path: Path.GetFullPath(path: left)),
            b: Path.TrimEndingDirectorySeparator(path: Path.GetFullPath(path: right)),
            comparisonType: StringComparison.OrdinalIgnoreCase);

    private static string[] FindProjectPaths(string root)
    {
        if (File.Exists(path: root)
            && (root.EndsWith(value: ".sln", comparisonType: StringComparison.OrdinalIgnoreCase)
                || root.EndsWith(value: ".slnx", comparisonType: StringComparison.OrdinalIgnoreCase)))
        {
            return root.EndsWith(value: ".slnx", comparisonType: StringComparison.OrdinalIgnoreCase)
                ? GetProjectsFromSlnx(solutionPath: root)
                : GetProjectsFromSln(solutionPath: root);
        }

        if (File.Exists(path: root))
        {
            root = Path.GetDirectoryName(path: root) ?? root;
        }

        return Directory.Exists(path: root)
            ? Directory.EnumerateFiles(path: root, searchPattern: "*.csproj", searchOption: SearchOption.AllDirectories)
                .Where(predicate: path => !path.Contains(value: $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", comparisonType: StringComparison.OrdinalIgnoreCase))
                .Where(predicate: path => !path.Contains(value: $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", comparisonType: StringComparison.OrdinalIgnoreCase))
                .Order(comparer: StringComparer.OrdinalIgnoreCase)
                .Select(selector: Path.GetFullPath)
                .ToArray()
            : [];
    }

    private static string[] GetProjectsFromSlnx(string solutionPath)
    {
        var solutionDirectory = Path.GetDirectoryName(path: solutionPath) ?? Environment.CurrentDirectory;
        var document = XDocument.Load(uri: solutionPath);
        return document
            .Descendants()
            .Where(predicate: element => element.Name.LocalName.Equals(value: "Project", comparisonType: StringComparison.Ordinal))
            .Select(selector: element => element.Attribute(name: "Path")?.Value)
            .Where(predicate: path => !string.IsNullOrWhiteSpace(value: path))
            .Select(selector: path => ResolveSolutionProjectPath(solutionDirectory: solutionDirectory, projectPath: path!))
            .Where(predicate: path => path.EndsWith(value: ".csproj", comparisonType: StringComparison.OrdinalIgnoreCase))
            .Where(predicate: File.Exists)
            .Order(comparer: StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string[] GetProjectsFromSln(string solutionPath)
    {
        var solutionDirectory = Path.GetDirectoryName(path: solutionPath) ?? Environment.CurrentDirectory;
#pragma warning disable SEC0116,SCS0018
        return File.ReadLines(path: solutionPath)
            .Select(selector: TryReadSlnProjectPath)
            .Where(predicate: path => !string.IsNullOrWhiteSpace(value: path))
            .Select(selector: path => ResolveSolutionProjectPath(solutionDirectory: solutionDirectory, projectPath: path!))
            .Where(predicate: path => path.EndsWith(value: ".csproj", comparisonType: StringComparison.OrdinalIgnoreCase))
            .Where(predicate: File.Exists)
            .Order(comparer: StringComparer.OrdinalIgnoreCase)
            .ToArray();
#pragma warning restore SEC0116,SCS0018
    }

    private static string? TryReadSlnProjectPath(string line)
    {
        if (!line.StartsWith(value: "Project(", comparisonType: StringComparison.Ordinal))
        {
            return null;
        }

        var parts = line.Split(separator: ',', options: StringSplitOptions.TrimEntries);
        return parts.Length >= 2
            ? parts[1].Trim(trimChar: '"')
            : null;
    }

    private static string ResolveSolutionProjectPath(
        string solutionDirectory,
        string projectPath)
    {
        var normalized = projectPath.Replace(oldChar: '\\', newChar: Path.DirectorySeparatorChar);
        return Path.GetFullPath(
            path: Path.IsPathRooted(path: normalized)
                ? normalized
                : Path.Combine(path1: solutionDirectory, path2: normalized));
    }

    private static bool ShouldIncludeDefinition(
        TemplateAnalysisItem item,
        TemplateToken token)
    {
        if (item.Location is null)
        {
            return false;
        }

        var text = token.Text.Trim(trimChar: '$');
        if (MatchesLabel(item: item, token: text))
        {
            return true;
        }

        if (!token.HasTemplatePrefix)
        {
            return false;
        }

        return text switch
        {
            "Types" => item.TargetKind == TemplateAnalysisTargetKind.Type,
            "Classes" => item.TargetKind == TemplateAnalysisTargetKind.Type && item.Detail.StartsWith(value: "Class", comparisonType: StringComparison.OrdinalIgnoreCase),
            "Records" => item.TargetKind == TemplateAnalysisTargetKind.Type && item.Detail.StartsWith(value: "Record", comparisonType: StringComparison.OrdinalIgnoreCase),
            "Interfaces" => item.TargetKind == TemplateAnalysisTargetKind.Type && item.Detail.StartsWith(value: "Interface", comparisonType: StringComparison.OrdinalIgnoreCase),
            "Enums" => item.TargetKind == TemplateAnalysisTargetKind.Type && item.Detail.StartsWith(value: "Enum", comparisonType: StringComparison.OrdinalIgnoreCase),
            "Properties" => item.TargetKind == TemplateAnalysisTargetKind.Property,
            "Methods" => item.TargetKind == TemplateAnalysisTargetKind.Method,
            "Parameters" => item.TargetKind == TemplateAnalysisTargetKind.Parameter,
            "Constants" => item.TargetKind == TemplateAnalysisTargetKind.Constant,
            "Values" or "EnumValues" => item.TargetKind == TemplateAnalysisTargetKind.EnumValue,
            _ => false,
        };
    }

    private static bool MatchesToken(
        TemplateAnalysisItem item,
        string token)
    {
        var normalizedToken = token.Trim(trimChar: '$');
        return MatchesLabel(item: item, token: normalizedToken)
            || item.Detail.Contains(value: normalizedToken, comparisonType: StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesLabel(
        TemplateAnalysisItem item,
        string token)
    {
        var normalizedToken = token.Trim(trimChar: '$');
        return item.Label.Equals(value: normalizedToken, comparisonType: StringComparison.OrdinalIgnoreCase);
    }

    private static string CreateHoverMarkdown(TemplateAnalysisItem item)
    {
        var prefix = item.TargetKind is TemplateAnalysisTargetKind.TemplateMember
            or TemplateAnalysisTargetKind.Filter
            or TemplateAnalysisTargetKind.Helper
                ? "$"
                : string.Empty;
        return $"**{prefix}{item.Label}**\n\n{item.Detail}\n\n{item.Documentation}";
    }

    private static LspRange CreateRange(
        TextDocumentState document,
        int start,
        int end) =>
        new(Start: OffsetToPosition(text: document.Text, offset: start), End: OffsetToPosition(text: document.Text, offset: end));

    private static LspPosition OffsetToPosition(
        string text,
        int offset)
    {
        var line = 0;
        var character = 0;
        for (var index = 0; index < offset && index < text.Length; index++)
        {
            if (text[index: index] == '\n')
            {
                line++;
                character = 0;
                continue;
            }

            character++;
        }

        return new LspPosition(Line: line, Character: character);
    }

    private static LspLocation ToLspLocation(SourceLocation location)
    {
        var line = Math.Max(val1: location.Line - 1, val2: 0);
        var column = Math.Max(val1: location.Column - 1, val2: 0);
        return new LspLocation(
            Uri: UriFromPath(path: location.Path),
            Range: new LspRange(
                Start: new LspPosition(Line: line, Character: column),
                End: new LspPosition(Line: line, Character: column + 1)));
    }

    private static bool IsOutputDirective(string line)
    {
        var trimmed = line.TrimStart();
        return trimmed.StartsWith(value: "// output:", comparisonType: StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith(value: "// typewriter-output:", comparisonType: StringComparison.OrdinalIgnoreCase);
    }

    private static int GetTypeCompletionKind(TypeMetadata type) =>
        type.Kind switch
        {
            TypeMetadataKind.Interface => CompletionKind.Interface,
            TypeMetadataKind.Enum => CompletionKind.Enum,
            _ => CompletionKind.Class,
        };

    private static string UriFromPath(string path) => new Uri(uriString: Path.GetFullPath(path: path)).AbsoluteUri;

    private async Task<IReadOnlyList<TemplateAnalysisItem>> GetAnalysisItemsAsync(
        TextDocumentState document,
        LanguageServerSettings settings,
        CancellationToken cancellationToken)
    {
        var items = new List<TemplateAnalysisItem>();
        AddTemplateItems(items: items);
        AddHelperItems(items: items, content: document.Text);

        var project = await LoadProjectMetadataAsync(document: document, settings: settings, cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        if (project is not null)
        {
            AddMetadataItems(items: items, project: project);
        }

        return items;
    }

#pragma warning disable CC0091,S2325
    private async Task<ProjectMetadata?> LoadProjectMetadataAsync(
        TextDocumentState document,
        LanguageServerSettings settings,
        CancellationToken cancellationToken)
#pragma warning restore CC0091,S2325
    {
        var workspacePath = settings.ResolveWorkspacePath(documentPath: document.Path);
        var projectPaths = ResolveProjectPaths(settings: settings, workspacePath: workspacePath, templatePath: document.Path);
        if (projectPaths.Count == 0)
        {
            return null;
        }

        var metadataProvider = new CSharpProjectMetadataProvider();
        var sourceFiles = new List<SourceFileMetadata>();
        var types = new List<TypeMetadata>();
        var diagnostics = new List<GenerationDiagnostic>();
        foreach (var projectPath in projectPaths)
        {
            var metadata = await metadataProvider.GetMetadataAsync(
                project: new ProjectContext(
                    ProjectPath: projectPath,
                    WorkspacePath: workspacePath,
                    TargetFramework: settings.Framework),
                cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
            sourceFiles.AddRange(collection: metadata.SourceFiles);
            types.AddRange(collection: metadata.Types);
            diagnostics.AddRange(collection: metadata.Diagnostics);
        }

        return new ProjectMetadata(
            ProjectPath: projectPaths[index: 0],
            SourceFiles: sourceFiles,
            Types: types,
            Diagnostics: diagnostics);
    }

#pragma warning disable CC0091
    private async Task<IReadOnlyList<LspLocation>> GetGeneratedFileLocationsAsync(
        TextDocumentState document,
        LanguageServerSettings settings,
        CancellationToken cancellationToken)
#pragma warning restore CC0091
    {
        var workspacePath = settings.ResolveWorkspacePath(documentPath: document.Path);
        var projectPath = settings.ResolveProjectPath(workspacePath: workspacePath);
        var configuration = await TypewriterConfigurationLoader.LoadAsync(
            workspacePath: workspacePath,
            projectPath: projectPath,
            cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        configuration = configuration with
        {
            DefaultTargetFramework = settings.Framework ?? configuration.DefaultTargetFramework,
            Output = configuration.Output with
            {
                DryRun = true,
            },
        };

        var request = new GenerationRequest(
            WorkspacePath: workspacePath,
            ProjectPath: projectPath,
            TemplatePath: document.Path,
            Mode: GenerationMode.Validate,
            Configuration: configuration,
            AllProjects: settings.AllProjects);
        var generator = new TypewriterGenerator(
            templateDiscovery: new InMemoryTemplateDiscovery(document: document),
            metadataProvider: new CSharpProjectMetadataProvider(),
            fileWriter: new FileSystemGeneratedFileWriter());
        var result = await generator.GenerateAsync(request: request, cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        return result.GeneratedFiles
            .Select(selector: file => new LspLocation(
                Uri: UriFromPath(path: file.Path),
                Range: new LspRange(
                    Start: new LspPosition(Line: 0, Character: 0),
                    End: new LspPosition(Line: 0, Character: 1))))
            .ToArray();
    }

    private static class CompletionKind
    {
        public const int Method = 2;
        public const int Function = 3;
        public const int Variable = 6;
        public const int Class = 7;
        public const int Interface = 8;
        public const int Property = 10;
        public const int Enum = 13;
        public const int Keyword = 14;
        public const int Snippet = 15;
        public const int EnumMember = 20;
        public const int Constant = 21;
    }

    private static class InsertTextFormat
    {
        public const int Snippet = 2;
    }
}
