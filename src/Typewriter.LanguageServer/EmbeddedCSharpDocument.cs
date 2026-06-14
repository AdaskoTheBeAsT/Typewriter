using System.Text;
using System.Text.RegularExpressions;

namespace Typewriter.LanguageServer;

/// <summary>
/// Projects the C# helper blocks of a .tst template into a single virtual C# document
/// shaped like the runtime template host produced by TemplateRuntimeCompiler, while
/// keeping a bidirectional offset map between template text and virtual text.
/// </summary>
internal sealed class EmbeddedCSharpDocument
{
    private const string HostTypeName = "TypewriterTemplateHost";
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(seconds: 1);

    private static readonly string[] DefaultUsings =
    [
        "using System;",
        "using System.Collections.Generic;",
        "using System.Linq;",
        "using System.Text;",
        "using System.Text.RegularExpressions;",
        "using Typewriter.CodeModel;",
        "using Typewriter.Configuration;",
        "using Typewriter.Extensions.Types;",
        "using Typewriter.Extensions.WebApi;",
        "using Typewriter.VisualStudio;",
        "using Attribute = Typewriter.CodeModel.Attribute;",
        "using Class = Typewriter.CodeModel.Class;",
        "using Constant = Typewriter.CodeModel.Constant;",
        "using Delegate = Typewriter.CodeModel.Delegate;",
        "using Enum = Typewriter.CodeModel.Enum;",
        "using EnumValue = Typewriter.CodeModel.EnumValue;",
        "using File = Typewriter.CodeModel.File;",
        "using Interface = Typewriter.CodeModel.Interface;",
        "using Method = Typewriter.CodeModel.Method;",
        "using Parameter = Typewriter.CodeModel.Parameter;",
        "using Property = Typewriter.CodeModel.Property;",
        "using Record = Typewriter.CodeModel.Record;",
        "using Type = Typewriter.CodeModel.Type;",
    ];

    private readonly IReadOnlyList<EmbeddedSpanMapping> _mappings;

    private EmbeddedCSharpDocument(
        string source,
        IReadOnlyList<EmbeddedSpanMapping> mappings)
    {
        Source = source;
        _mappings = mappings;
    }

    public string Source { get; }

    public static EmbeddedCSharpDocument? Create(string templateText)
    {
        var regions = FindCSharpRegions(templateText: templateText);
        if (regions.Count == 0)
        {
            return null;
        }

        var usings = new SortedSet<string>(collection: DefaultUsings, comparer: StringComparer.Ordinal);
        foreach (var region in regions)
        {
            CollectUsingDirectives(templateText: templateText, region: region, usings: usings);
        }

        var source = new StringBuilder();
        var mappings = new List<EmbeddedSpanMapping>();
        source.Append(value: "#nullable disable\n");
        foreach (var usingLine in usings)
        {
            source.Append(value: usingLine).Append(value: '\n');
        }

        source.Append(value: "namespace Typewriter.Engine.TemplateRuntime.Generated\n");
        source.Append(value: "{\n");
        source.Append(value: "public sealed class ").Append(value: HostTypeName).Append(value: '\n');
        source.Append(value: "{\n");
        source.Append(value: "public ").Append(value: HostTypeName).Append(value: "() { }\n");
        source.Append(value: "public ").Append(value: HostTypeName).Append(value: "(Typewriter.Configuration.Settings settings) { }\n");

        foreach (var region in regions)
        {
            AppendRegion(templateText: templateText, region: region, source: source, mappings: mappings);
        }

        source.Append(value: "}\n");
        source.Append(value: "}\n");
        return new EmbeddedCSharpDocument(source: source.ToString(), mappings: mappings);
    }

    public bool TryMapToVirtual(
        int templateOffset,
        out int virtualOffset)
    {
        foreach (var mapping in _mappings)
        {
            if (templateOffset >= mapping.TemplateStart
                && templateOffset <= mapping.TemplateStart + mapping.Length)
            {
                virtualOffset = mapping.VirtualStart + (templateOffset - mapping.TemplateStart);
                return true;
            }
        }

        virtualOffset = 0;
        return false;
    }

    public bool TryMapToTemplate(
        int virtualOffset,
        out int templateOffset)
    {
        foreach (var mapping in _mappings)
        {
            if (virtualOffset >= mapping.VirtualStart
                && virtualOffset <= mapping.VirtualStart + mapping.Length)
            {
                templateOffset = mapping.TemplateStart + (virtualOffset - mapping.VirtualStart);
                return true;
            }
        }

        templateOffset = 0;
        return false;
    }

    private static IReadOnlyList<EmbeddedLanguageRegion> FindCSharpRegions(string templateText)
    {
        var regions = new List<EmbeddedLanguageRegion>();
        var index = 0;
        while (index < templateText.Length)
        {
            if (templateText[index: index] != '$'
                || !TemplateEmbeddedLanguage.TryReadCSharpBlock(text: templateText, dollarIndex: index, allowUnclosed: true, region: out var region))
            {
                index++;
                continue;
            }

            regions.Add(item: region);
            index = Math.Max(val1: region.End, val2: index + 1);
        }

        return regions;
    }

    private static void CollectUsingDirectives(
        string templateText,
        EmbeddedLanguageRegion region,
        ISet<string> usings)
    {
        foreach (var line in EnumerateLines(templateText: templateText, region: region))
        {
            var trimmed = templateText[line.Start..line.End].Trim();
            if (IsUsingDirective(line: trimmed))
            {
                _ = usings.Add(item: trimmed);
            }
        }
    }

    private static void AppendRegion(
        string templateText,
        EmbeddedLanguageRegion region,
        StringBuilder source,
        ICollection<EmbeddedSpanMapping> mappings)
    {
        foreach (var line in EnumerateLines(templateText: templateText, region: region))
        {
            var trimmed = templateText[line.Start..line.End].Trim();
            if (IsUsingDirective(line: trimmed) || IsReferenceDirective(line: trimmed))
            {
                source.Append(value: '\n');
                continue;
            }

            mappings.Add(item: new EmbeddedSpanMapping(TemplateStart: line.Start, VirtualStart: source.Length, Length: line.End - line.Start));
            source.Append(value: templateText, startIndex: line.Start, count: line.End - line.Start).Append(value: '\n');
        }
    }

    private static IEnumerable<(int Start, int End)> EnumerateLines(
        string templateText,
        EmbeddedLanguageRegion region)
    {
        var index = region.ContentStart;
        while (index < region.ContentEnd)
        {
            var newLine = templateText.IndexOf(value: '\n', startIndex: index);
            var lineEnd = newLine < 0 || newLine >= region.ContentEnd
                ? region.ContentEnd
                : newLine;
            var contentEnd = lineEnd;
            if (contentEnd > index && templateText[index: contentEnd - 1] == '\r')
            {
                contentEnd--;
            }

            yield return (index, contentEnd);
            index = lineEnd < region.ContentEnd ? lineEnd + 1 : region.ContentEnd;
        }
    }

    private static bool IsUsingDirective(string line) =>
        line.StartsWith(value: "using ", comparisonType: StringComparison.Ordinal)
        && line.EndsWith(value: ';');

    private static bool IsReferenceDirective(string line) =>
        Regex.IsMatch(
            input: line,
            pattern: @"^(?:#r|#reference)\s",
            options: RegexOptions.CultureInvariant,
            matchTimeout: RegexTimeout);
}
