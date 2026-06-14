using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Typewriter.Abstractions;

namespace Typewriter.Engine;

internal static class TemplateRuntimeCompiler
{
    private const string HostTypeFullName = "Typewriter.Engine.TemplateRuntime.Generated.TypewriterTemplateHost";
    private const string HostTypeName = "TypewriterTemplateHost";
    private const int MaxRemoteLoadBytes = 1024 * 1024;
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(seconds: 1);
    private static readonly TimeSpan RemoteLoadTimeout = TimeSpan.FromSeconds(seconds: 30);
    private static readonly ConcurrentDictionary<string, RemoteLoadCacheEntry> RemoteLoadCache = new(comparer: StringComparer.OrdinalIgnoreCase);

    public static CompiledTemplateHelper? Compile(
        TemplateDocument template,
        ProjectMetadata metadata,
        ICollection<GenerationDiagnostic> diagnostics) =>
        Compile(template: template, metadata: metadata, diagnostics: diagnostics, defaults: TemplateRenderDefaults.Default);

#pragma warning disable MA0051 // Method is too long
    public static CompiledTemplateHelper? Compile(
        TemplateDocument template,
        ProjectMetadata metadata,
        ICollection<GenerationDiagnostic> diagnostics,
        TemplateRenderDefaults defaults)
#pragma warning restore MA0051 // Method is too long
    {
        if (template.CodeBlocks.Count == 0)
        {
            return null;
        }

        var source = BuildSource(templatePath: template.Path, codeBlocks: template.CodeBlocks, diagnostics: diagnostics, referenceDirectives: out var referenceDirectives);
        var resolver = new TemplateReferenceResolver(templatePath: template.Path, diagnostics: diagnostics);
        var references = resolver.CreateReferences(referenceDirectives: referenceDirectives);
        var syntaxTree = CSharpSyntaxTree.ParseText(
            text: source,
            options: CSharpParseOptions.Default.WithLanguageVersion(version: LanguageVersion.Preview),
            path: template.Path,
            encoding: Encoding.UTF8);
        var compilation = CSharpCompilation.Create(
            assemblyName: "TypewriterTemplate_" + Guid.NewGuid().ToString(format: "N"),
            syntaxTrees: [syntaxTree],
            references: references,
            options: new CSharpCompilationOptions(
                outputKind: OutputKind.DynamicallyLinkedLibrary,
                optimizationLevel: OptimizationLevel.Debug,
                nullableContextOptions: NullableContextOptions.Disable));

        using var assemblyStream = new MemoryStream();

        // The embedded portable PDB keeps #line-mapped template positions available in
        // runtime helper exception stack traces.
        var emitResult = compilation.Emit(
            peStream: assemblyStream,
            options: new EmitOptions(debugInformationFormat: DebugInformationFormat.Embedded));
        if (!emitResult.Success)
        {
            AddCompileDiagnostics(templatePath: template.Path, diagnostics: diagnostics, compileDiagnostics: emitResult.Diagnostics);
            return null;
        }

        assemblyStream.Position = 0;
        var assemblyLoadContext = new TemplateAssemblyLoadContext(
            name: compilation.AssemblyName ?? "TypewriterTemplate",
            referencePaths: references.Select(selector: reference => reference.Display));
        var assembly = assemblyLoadContext.LoadFromStream(assembly: assemblyStream);
        var hostType = assembly.GetType(name: HostTypeFullName, throwOnError: false);
        if (hostType is null)
        {
            assemblyLoadContext.Unload();
            diagnostics.Add(
                item: new GenerationDiagnostic(
                    File: template.Path,
                    Line: null,
                    Column: null,
                    Severity: Typewriter.Abstractions.DiagnosticSeverity.Error,
                    Message: "Compiled template host type was not found.",
                    Code: DiagnosticCodes.TemplateParseError));
            return null;
        }

        var settings = new Typewriter.Configuration.Settings
        {
            TemplatePath = template.Path,
            SolutionFullName = defaults.SolutionFullName,
            Log = new TemplateDiagnosticsLog(templatePath: template.Path, diagnostics: diagnostics),
        };
        settings.ApplyConfigurationDefaults(
            strictNullGeneration: defaults.StrictNullGeneration,
            utf8BomGeneration: defaults.Utf8BomGeneration,
            stringLiteralCharacter: defaults.StringLiteralCharacter);
        var adapterFactory = new TemplateCodeModelAdapterFactory(metadata: metadata, settings: settings);
        var host = CreateHost(hostType: hostType, settings: settings, file: adapterFactory.CreateFile(project: metadata));
        return new CompiledTemplateHelper(
            host: host,
            adapterFactory: adapterFactory,
            settings: settings,
            loadContext: assemblyLoadContext);
    }

    private static object CreateHost(
        System.Type hostType,
        Typewriter.Configuration.Settings settings,
        Typewriter.CodeModel.File file)
    {
#pragma warning disable S3011 // Reflection should not be used to increase accessibility of classes, methods, or fields
        var fileConstructor = hostType.GetConstructor(
            bindingAttr: BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            types: [typeof(Typewriter.Configuration.Settings), typeof(Typewriter.CodeModel.File)],
            modifiers: null);
#pragma warning restore S3011 // Reflection should not be used to increase accessibility of classes, methods, or fields
        if (fileConstructor is not null)
        {
            return fileConstructor.Invoke(parameters: [settings, file]);
        }

#pragma warning disable S3011 // Reflection should not be used to increase accessibility of classes, methods, or fields
        var settingsConstructor = hostType.GetConstructor(
            bindingAttr: BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            types: [typeof(Typewriter.Configuration.Settings)],
            modifiers: null);
#pragma warning restore S3011 // Reflection should not be used to increase accessibility of classes, methods, or fields
        if (settingsConstructor is not null)
        {
            return settingsConstructor.Invoke(parameters: [settings]);
        }

        return Activator.CreateInstance(type: hostType, nonPublic: true)!;
    }

#pragma warning disable MA0051 // Method is too long
    private static string BuildSource(
        string templatePath,
        IEnumerable<TemplateCodeBlock> codeBlocks,
        ICollection<GenerationDiagnostic> diagnostics,
        out IReadOnlyList<string> referenceDirectives)
#pragma warning restore MA0051 // Method is too long
    {
        var usings = new SortedSet<string>(comparer: StringComparer.Ordinal)
        {
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
        };
        var references = new List<string>();
        var members = new StringBuilder();
        var loadedPaths = new HashSet<string>(comparer: StringComparer.OrdinalIgnoreCase);

        foreach (var codeBlock in codeBlocks)
        {
            var loads = new List<LoadDirective>();
            members.AppendLine(value: CreateLineDirective(line: codeBlock.StartLine, templatePath: templatePath));
            members.AppendLine(value: ExtractDirectives(codeBlock: codeBlock.Content, usings: usings, references: references, loads: loads));
            members.AppendLine(value: "#line default");
            foreach (var load in loads)
            {
                AppendLoadedSource(loadingFilePath: templatePath, load: load, members: members, usings: usings, references: references, loadedPaths: loadedPaths, diagnostics: diagnostics);
            }
        }

        var source = new StringBuilder();
        source.AppendLine(value: "#nullable disable");
        foreach (var usingLine in usings)
        {
            source.AppendLine(value: usingLine);
        }

        source.AppendLine(value: "namespace Typewriter.Engine.TemplateRuntime.Generated");
        source.AppendLine(value: "{");
        source.AppendLine(value: "    public sealed class " + HostTypeName);
        source.AppendLine(value: "    {");
        source.AppendLine(value: "        public " + HostTypeName + "() { }");
        source.AppendLine(value: TransformTemplateConstructor(members: members.ToString()));
        source.AppendLine(value: "    }");
        source.AppendLine(value: "}");

        referenceDirectives = references;
        return source.ToString();
    }

    private static string ExtractDirectives(
        string codeBlock,
        ISet<string> usings,
        ICollection<string> references,
        ICollection<LoadDirective> loads)
    {
        var members = new StringBuilder(capacity: codeBlock.Length);
        using var reader = new StringReader(s: codeBlock);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            var trimmed = line.Trim();
            if (TryReadUsing(line: trimmed, usingLine: out var usingLine))
            {
                usings.Add(item: usingLine);
                members.AppendLine();
                continue;
            }

            if (TryReadReference(line: trimmed, reference: out var reference))
            {
                references.Add(item: reference);
                members.AppendLine();
                continue;
            }

            if (TryReadLoad(line: trimmed, load: out var load))
            {
                loads.Add(item: load);
                members.AppendLine();
                continue;
            }

            members.AppendLine(value: line);
        }

        return members.ToString();
    }

    private static void AppendLoadedSource(
        string loadingFilePath,
        LoadDirective load,
        StringBuilder members,
        ISet<string> usings,
        ICollection<string> references,
        ISet<string> loadedPaths,
        ICollection<GenerationDiagnostic> diagnostics)
    {
        if (!TryResolveLoadTarget(loadingFilePath: loadingFilePath, loadPath: load.Path, diagnostics: diagnostics, loadedPath: out var loadedPath, isRemote: out var isRemote)
            || !TryResolveCacheDuration(loadingFilePath: loadingFilePath, load: load, isRemote: isRemote, diagnostics: diagnostics, cacheDuration: out var cacheDuration))
        {
            return;
        }

        if (!loadedPaths.Add(item: loadedPath))
        {
            return;
        }

        if (!TryReadLoadedSource(loadingFilePath: loadingFilePath, loadedPath: loadedPath, isRemote: isRemote, cacheDuration: cacheDuration, diagnostics: diagnostics, source: out var source))
        {
            return;
        }

        var loads = new List<LoadDirective>();
        var extracted = ExtractDirectives(codeBlock: source, usings: usings, references: references, loads: loads);
        members.AppendLine(value: CreateLineDirective(line: 1, templatePath: loadedPath));
        members.AppendLine(value: extracted);
        members.AppendLine(value: "#line default");
        foreach (var nestedLoad in loads)
        {
            AppendLoadedSource(loadingFilePath: loadedPath, load: nestedLoad, members: members, usings: usings, references: references, loadedPaths: loadedPaths, diagnostics: diagnostics);
        }
    }

    private static bool TryResolveCacheDuration(
        string loadingFilePath,
        LoadDirective load,
        bool isRemote,
        ICollection<GenerationDiagnostic> diagnostics,
        out TimeSpan? cacheDuration)
    {
        cacheDuration = null;
        if (string.IsNullOrWhiteSpace(value: load.CacheDuration))
        {
            return true;
        }

        if (!isRemote)
        {
            AddLoadDiagnostic(diagnostics: diagnostics, file: loadingFilePath, message: $"#load cache duration is only supported for remote URLs: '{load.Path}'.");
            return false;
        }

        if (!TimeSpan.TryParse(input: load.CacheDuration, formatProvider: CultureInfo.InvariantCulture, result: out var parsed)
            || parsed <= TimeSpan.Zero)
        {
            AddLoadDiagnostic(diagnostics: diagnostics, file: loadingFilePath, message: $"#load cache duration is invalid: '{load.CacheDuration}'. Use a positive TimeSpan value, for example '00:10:00'.");
            return false;
        }

        cacheDuration = parsed;
        return true;
    }

    private static bool TryResolveLoadTarget(
        string loadingFilePath,
        string loadPath,
        ICollection<GenerationDiagnostic> diagnostics,
        out string loadedPath,
        out bool isRemote)
    {
        loadedPath = string.Empty;
        isRemote = false;
        if (TryCreateRemoteUri(value: loadPath, uri: out var remoteLoadUri))
        {
            loadedPath = NormalizeRemoteUri(uri: remoteLoadUri);
            isRemote = true;
            return true;
        }

        if (LooksLikeAbsoluteUri(value: loadPath))
        {
            AddLoadDiagnostic(diagnostics: diagnostics, file: loadingFilePath, message: $"#load URI scheme is not supported: '{loadPath}'. Use http or https.");
            return false;
        }

        if (TryCreateRemoteUri(value: loadingFilePath, uri: out var remoteLoadingUri))
        {
            if (LooksLikeLocalRootedPath(value: loadPath))
            {
                AddLoadDiagnostic(diagnostics: diagnostics, file: loadingFilePath, message: $"Remote #load cannot reference a local file path: '{loadPath}'.");
                return false;
            }

            loadedPath = NormalizeRemoteUri(uri: new Uri(baseUri: remoteLoadingUri, relativeUri: loadPath));
            isRemote = true;
            return true;
        }

        var directory = Path.GetDirectoryName(path: Path.GetFullPath(path: loadingFilePath)) ?? Environment.CurrentDirectory;
        loadedPath = Path.IsPathRooted(path: loadPath)
            ? Path.GetFullPath(path: loadPath)
            : Path.GetFullPath(path: Path.Combine(path1: directory, path2: loadPath));
        return true;
    }

    private static bool TryReadLoadedSource(
        string loadingFilePath,
        string loadedPath,
        bool isRemote,
        TimeSpan? cacheDuration,
        ICollection<GenerationDiagnostic> diagnostics,
        out string source)
    {
        source = string.Empty;
        if (isRemote)
        {
            return TryReadRemoteLoadedSource(loadingFilePath: loadingFilePath, loadedPath: loadedPath, cacheDuration: cacheDuration, diagnostics: diagnostics, source: out source);
        }

        if (!File.Exists(path: loadedPath))
        {
            AddLoadDiagnostic(diagnostics: diagnostics, file: loadingFilePath, message: $"#load file was not found: '{loadedPath}'.");
            return false;
        }

#pragma warning disable SCS0018,SEC0116
        source = File.ReadAllText(path: loadedPath);
#pragma warning restore SCS0018,SEC0116
        return true;
    }

    private static bool TryReadRemoteLoadedSource(
        string loadingFilePath,
        string loadedPath,
        TimeSpan? cacheDuration,
        ICollection<GenerationDiagnostic> diagnostics,
        out string source)
    {
        source = string.Empty;
        var now = DateTimeOffset.UtcNow;
        if (TryReadCachedRemoteLoadedSource(loadedPath: loadedPath, cacheDuration: cacheDuration, now: now, source: out source))
        {
            return true;
        }

        try
        {
            using var client = new HttpClient
            {
                Timeout = RemoteLoadTimeout,
            };
            using var request = new HttpRequestMessage(method: HttpMethod.Get, requestUri: loadedPath);
            using var response = client.Send(request: request, completionOption: HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode)
            {
                AddLoadDiagnostic(
                    diagnostics: diagnostics,
                    file: loadingFilePath,
                    message: $"#load URL returned HTTP {((int)response.StatusCode).ToString(provider: CultureInfo.InvariantCulture)} {response.ReasonPhrase}: '{loadedPath}'.");
                return false;
            }

            if (!TryReadRemoteResponseSource(
                    loadingFilePath: loadingFilePath,
                    loadedPath: loadedPath,
                    response: response,
                    diagnostics: diagnostics,
                    source: out source))
            {
                return false;
            }

            if (cacheDuration is not null)
            {
                RemoteLoadCache[key: loadedPath] = new RemoteLoadCacheEntry(Source: source, FetchedAtUtc: now);
            }

            return true;
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or TaskCanceledException)
        {
            AddLoadDiagnostic(diagnostics: diagnostics, file: loadingFilePath, message: $"#load URL could not be read: '{loadedPath}'. {ex.Message}");
            return false;
        }
    }

    private static bool TryReadCachedRemoteLoadedSource(
        string loadedPath,
        TimeSpan? cacheDuration,
        DateTimeOffset now,
        out string source)
    {
        source = string.Empty;
        if (cacheDuration is not { } duration
            || !RemoteLoadCache.TryGetValue(key: loadedPath, value: out var cached)
            || now - cached.FetchedAtUtc > duration)
        {
            if (cacheDuration is not null)
            {
                _ = RemoteLoadCache.TryRemove(key: loadedPath, value: out _);
            }

            return false;
        }

        source = cached.Source;
        return true;
    }

    private static bool TryReadRemoteResponseSource(
        string loadingFilePath,
        string loadedPath,
        HttpResponseMessage response,
        ICollection<GenerationDiagnostic> diagnostics,
        out string source)
    {
        source = string.Empty;
        var contentLength = response.Content.Headers.ContentLength;
        if (contentLength > MaxRemoteLoadBytes)
        {
            AddRemoteLoadTooLargeDiagnostic(diagnostics: diagnostics, file: loadingFilePath, loadedPath: loadedPath);
            return false;
        }

        using var stream = response.Content.ReadAsStream();
        using var memory = new MemoryStream();
        var buffer = new byte[81920];
        int bytesRead;
        while ((bytesRead = stream.Read(buffer: buffer)) > 0)
        {
            if (memory.Length + bytesRead > MaxRemoteLoadBytes)
            {
                AddRemoteLoadTooLargeDiagnostic(diagnostics: diagnostics, file: loadingFilePath, loadedPath: loadedPath);
                return false;
            }

            memory.Write(buffer: buffer, offset: 0, count: bytesRead);
        }

        memory.Position = 0;
        using var reader = new StreamReader(stream: memory, encoding: Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        source = reader.ReadToEnd();
        return true;
    }

    private static void AddRemoteLoadTooLargeDiagnostic(
        ICollection<GenerationDiagnostic> diagnostics,
        string file,
        string loadedPath)
    {
        AddLoadDiagnostic(
            diagnostics: diagnostics,
            file: file,
            message: $"#load URL is too large: '{loadedPath}'. Maximum size is {MaxRemoteLoadBytes.ToString(provider: CultureInfo.InvariantCulture)} bytes.");
    }

    private static bool TryCreateRemoteUri(
        string value,
        out Uri uri)
    {
        if (Uri.TryCreate(uriString: value, uriKind: UriKind.Absolute, result: out uri!)
            && uri.Scheme is "http" or "https")
        {
            return true;
        }

        uri = null!;
        return false;
    }

    private static bool LooksLikeAbsoluteUri(string value) =>
        Regex.IsMatch(
            input: value,
            pattern: "^[A-Za-z][A-Za-z0-9+.-]*://",
            options: RegexOptions.CultureInvariant,
            matchTimeout: RegexTimeout);

    private static bool LooksLikeLocalRootedPath(string value) =>
        Regex.IsMatch(
            input: value,
            pattern: @"^(?:[A-Za-z]:[\\/]|\\\\)",
            options: RegexOptions.CultureInvariant,
            matchTimeout: RegexTimeout);

    private static string NormalizeRemoteUri(Uri uri)
    {
        var builder = new UriBuilder(uri: uri)
        {
            Fragment = string.Empty,
        };
        return builder.Uri.AbsoluteUri;
    }

    private static void AddLoadDiagnostic(
        ICollection<GenerationDiagnostic> diagnostics,
        string file,
        string message)
    {
        diagnostics.Add(
            item: new GenerationDiagnostic(
                File: file,
                Line: null,
                Column: null,
                Severity: Typewriter.Abstractions.DiagnosticSeverity.Error,
                Message: message,
                Code: DiagnosticCodes.TemplateParseError));
    }

    private static string CreateLineDirective(
        int line,
        string templatePath)
    {
        var escapedPath = templatePath
            .Replace(oldValue: "\\", newValue: "\\\\", comparisonType: StringComparison.Ordinal)
            .Replace(oldValue: "\"", newValue: "\\\"", comparisonType: StringComparison.Ordinal);
        return $"#line {line.ToString(provider: CultureInfo.InvariantCulture)} \"{escapedPath}\"";
    }

    private static bool TryReadUsing(
        string line,
        out string usingLine)
    {
        usingLine = string.Empty;
        if (!line.StartsWith(value: "using ", comparisonType: StringComparison.Ordinal)
            || !line.EndsWith(value: ';'))
        {
            return false;
        }

        usingLine = line;
        return true;
    }

    private static bool TryReadReference(
        string line,
        out string reference)
    {
        reference = string.Empty;
        var match = Regex.Match(
            input: line,
            pattern: @"^(?:#r|#reference)\s+[""'](?<reference>[^""']+)[""']",
            options: RegexOptions.CultureInvariant,
            matchTimeout: RegexTimeout);
        if (!match.Success)
        {
            return false;
        }

        reference = match.Groups[groupname: nameof(reference)].Value;
        return true;
    }

    private static bool TryReadLoad(
        string line,
        out LoadDirective load)
    {
        load = default!;
        var match = Regex.Match(
            input: line,
            pattern: @"^#load\s+(?<quote>[""'])(?<path>[^""']+)\k<quote>(?:\s*,\s*(?:(?<cacheQuote>[""'])(?<cacheQuoted>[^""']+)\k<cacheQuote>|(?<cacheBare>[^,\s]+)))?\s*$",
            options: RegexOptions.CultureInvariant,
            matchTimeout: RegexTimeout);
        if (!match.Success)
        {
            return false;
        }

        string? cacheDuration = null;
        if (match.Groups[groupname: "cacheQuoted"].Success)
        {
            cacheDuration = match.Groups[groupname: "cacheQuoted"].Value;
        }
        else if (match.Groups[groupname: "cacheBare"].Success)
        {
            cacheDuration = match.Groups[groupname: "cacheBare"].Value;
        }

        load = new LoadDirective(Path: match.Groups[groupname: "path"].Value, CacheDuration: cacheDuration);
        return true;
    }

    private static string TransformTemplateConstructor(string members)
    {
#pragma warning disable SA1118 // Parameter should not span multiple lines
        var transformed = Regex.Replace(
            input: members,
            pattern:
            @"(?m)^(?<indent>\s*)(?:(?:public|private|internal|protected)\s+)?Template\s*\(\s*Settings\s+(?<settings>[A-Za-z_][A-Za-z0-9_]*)\s*,\s*(?:Typewriter\.CodeModel\.)?File\s+(?<file>[A-Za-z_][A-Za-z0-9_]*)\s*\)",
            evaluator: match => string.Concat(
                match.Groups[groupname: "indent"].Value,
                "public ",
                HostTypeName,
                "(Settings ",
                match.Groups[groupname: "settings"].Value,
                ", File ",
                match.Groups[groupname: "file"].Value,
                ")"),
            options: RegexOptions.CultureInvariant,
            matchTimeout: RegexTimeout);
#pragma warning restore SA1118 // Parameter should not span multiple lines

#pragma warning disable SA1118 // Parameter should not span multiple lines
        return Regex.Replace(
            input: transformed,
            pattern:
            @"(?m)^(?<indent>\s*)(?:(?:public|private|internal|protected)\s+)?Template\s*\(\s*Settings\s+(?<parameter>[A-Za-z_][A-Za-z0-9_]*)\s*\)",
            evaluator: match => string.Concat(
                match.Groups[groupname: "indent"].Value,
                "public ",
                HostTypeName,
                "(Settings ",
                match.Groups[groupname: "parameter"].Value,
                ")"),
            options: RegexOptions.CultureInvariant,
            matchTimeout: RegexTimeout);
#pragma warning restore SA1118 // Parameter should not span multiple lines
    }

    private static void AddCompileDiagnostics(
        string templatePath,
        ICollection<GenerationDiagnostic> diagnostics,
        IEnumerable<Diagnostic> compileDiagnostics)
    {
        foreach (var diagnostic in compileDiagnostics.Where(predicate: diagnostic => diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error))
        {
            var lineSpan = diagnostic.Location.GetLineSpan();
            var hasLocation = diagnostic.Location != Location.None && lineSpan.IsValid;
            diagnostics.Add(
                item: new GenerationDiagnostic(
                    File: string.IsNullOrWhiteSpace(value: lineSpan.Path) ? templatePath : lineSpan.Path,
                    Line: hasLocation ? lineSpan.StartLinePosition.Line + 1 : null,
                    Column: hasLocation ? lineSpan.StartLinePosition.Character + 1 : null,
                    Severity: Typewriter.Abstractions.DiagnosticSeverity.Error,
                    Message: "Template C# compile error " + diagnostic.Id + ": " + diagnostic.GetMessage(formatProvider: CultureInfo.InvariantCulture),
                    Code: DiagnosticCodes.TemplateParseError));
        }
    }

    private sealed record LoadDirective(string Path, string? CacheDuration);

    private sealed record RemoteLoadCacheEntry(string Source, DateTimeOffset FetchedAtUtc);
}
