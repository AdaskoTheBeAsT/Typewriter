using System.Text.Json;
using System.Text.Json.Serialization;
using Typewriter.Abstractions;

namespace Typewriter.Engine;

public static class TypewriterConfigurationLoader
{
    private static readonly string[] ConfigurationNames =
    [
        "typewriter.json",
        "typewriter.config.json",
        ".typewriterrc.json",
    ];

    public static async Task<TypewriterConfiguration> LoadAsync(
        string? workspacePath,
        string? projectPath,
        CancellationToken cancellationToken)
    {
        var configuration = TypewriterConfiguration.Default;
        foreach (var configurationPath in FindConfigurationFiles(workspacePath: workspacePath, projectPath: projectPath))
        {
            var loadedConfiguration = await ReadConfigurationAsync(path: configurationPath, cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
            configuration = Merge(current: configuration, loaded: loadedConfiguration);
        }

        return ApplyEnvironment(configuration: configuration);
    }

    private static IEnumerable<string> FindConfigurationFiles(
        string? workspacePath,
        string? projectPath)
    {
        var workspaceDirectory = ResolveDirectory(path: workspacePath);
        var projectDirectory = ResolveDirectory(path: projectPath);

        foreach (var path in FindConfigurationFilesInDirectory(directory: workspaceDirectory))
        {
            yield return path;
        }

        if (!string.Equals(a: workspaceDirectory, b: projectDirectory, comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            foreach (var path in FindConfigurationFilesInDirectory(directory: projectDirectory))
            {
                yield return path;
            }
        }
    }

    private static IEnumerable<string> FindConfigurationFilesInDirectory(string directory)
    {
        return ConfigurationNames
            .Select(selector: name => Path.Combine(path1: directory, path2: name))
            .Where(predicate: File.Exists);
    }

    private static string ResolveDirectory(string? path)
    {
        if (string.IsNullOrWhiteSpace(value: path))
        {
            return Environment.CurrentDirectory;
        }

        var fullPath = Path.GetFullPath(path: path);
        return File.Exists(path: fullPath)
            ? Path.GetDirectoryName(path: fullPath) ?? Environment.CurrentDirectory
            : fullPath;
    }

    private static async Task<ConfigurationFile> ReadConfigurationAsync(
        string path,
        CancellationToken cancellationToken)
    {
#pragma warning disable MA0004,SCS0018,SEC0116
        await using var stream = File.OpenRead(path: path);
#pragma warning restore MA0004,SCS0018,SEC0116
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        };
        options.Converters.Add(item: new JsonStringEnumConverter<FileNameConvention>(namingPolicy: JsonNamingPolicy.CamelCase));
        options.Converters.Add(item: new JsonStringEnumConverter<IndentStyle>(namingPolicy: JsonNamingPolicy.CamelCase));
        options.Converters.Add(item: new JsonStringEnumConverter<QuoteStyle>(namingPolicy: JsonNamingPolicy.CamelCase));

        return await JsonSerializer.DeserializeAsync<ConfigurationFile>(
                utf8Json: stream,
                options: options,
                cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false)
            ?? new ConfigurationFile();
    }

    private static TypewriterConfiguration Merge(
        TypewriterConfiguration current,
        ConfigurationFile loaded)
    {
        return current with
        {
            Templates = loaded.Templates ?? current.Templates,
            Exclude = loaded.Exclude ?? current.Exclude,
            InputExtensions = NormalizeExtensions(extensions: loaded.InputExtensions) ?? current.InputExtensions,
            DefaultTargetFramework = loaded.DefaultTargetFramework ?? current.DefaultTargetFramework,
            Output = current.Output with
            {
                Newline = loaded.Output?.Newline ?? current.Output.Newline,
                Encoding = loaded.Output?.Encoding ?? current.Output.Encoding,
                WriteOnlyWhenChanged = loaded.Output?.WriteOnlyWhenChanged ?? current.Output.WriteOnlyWhenChanged,
                DryRun = loaded.Output?.DryRun ?? current.Output.DryRun,
                FileNameConvention = loaded.Output?.FileNameConvention ?? current.Output.FileNameConvention,
                StrictNull = loaded.Output?.StrictNull ?? current.Output.StrictNull,
                IndentStyle = loaded.Output?.IndentStyle ?? current.Output.IndentStyle,
                IndentSize = loaded.Output?.IndentSize ?? current.Output.IndentSize,
                InsertFinalNewline = loaded.Output?.InsertFinalNewline ?? current.Output.InsertFinalNewline,
                TrimTrailingWhitespace = loaded.Output?.TrimTrailingWhitespace ?? current.Output.TrimTrailingWhitespace,
                QuoteStyle = loaded.Output?.QuoteStyle ?? current.Output.QuoteStyle,
                DateType = loaded.Output?.DateType ?? current.Output.DateType,
                DateInitializer = loaded.Output?.DateInitializer ?? current.Output.DateInitializer,
                DateOnlyType = loaded.Output?.DateOnlyType ?? current.Output.DateOnlyType,
                DateOnlyInitializer = loaded.Output?.DateOnlyInitializer ?? current.Output.DateOnlyInitializer,
                TimeOnlyType = loaded.Output?.TimeOnlyType ?? current.Output.TimeOnlyType,
                TimeOnlyInitializer = loaded.Output?.TimeOnlyInitializer ?? current.Output.TimeOnlyInitializer,
                GuidType = loaded.Output?.GuidType ?? current.Output.GuidType,
                DecimalType = loaded.Output?.DecimalType ?? current.Output.DecimalType,
            },
            Diagnostics = current.Diagnostics with
            {
                FailOnWarning = loaded.Diagnostics?.FailOnWarning ?? current.Diagnostics.FailOnWarning,
            },
        };
    }

    private static TypewriterConfiguration ApplyEnvironment(TypewriterConfiguration configuration)
    {
        return configuration with
        {
            InputExtensions = ReadStringListEnvironment(name: "TYPEWRITER_INPUT_EXTENSIONS") ?? configuration.InputExtensions,
            DefaultTargetFramework = Environment.GetEnvironmentVariable(variable: "TYPEWRITER_DEFAULT_TARGET_FRAMEWORK")
                ?? configuration.DefaultTargetFramework,
            Output = configuration.Output with
            {
                Newline = Environment.GetEnvironmentVariable(variable: "TYPEWRITER_OUTPUT_NEWLINE")
                    ?? configuration.Output.Newline,
                DateType = Environment.GetEnvironmentVariable(variable: "TYPEWRITER_OUTPUT_DATE_TYPE")
                    ?? configuration.Output.DateType,
                DateInitializer = Environment.GetEnvironmentVariable(variable: "TYPEWRITER_OUTPUT_DATE_INITIALIZER")
                    ?? configuration.Output.DateInitializer,
                DateOnlyType = Environment.GetEnvironmentVariable(variable: "TYPEWRITER_OUTPUT_DATE_ONLY_TYPE")
                    ?? configuration.Output.DateOnlyType,
                DateOnlyInitializer = Environment.GetEnvironmentVariable(variable: "TYPEWRITER_OUTPUT_DATE_ONLY_INITIALIZER")
                    ?? configuration.Output.DateOnlyInitializer,
                TimeOnlyType = Environment.GetEnvironmentVariable(variable: "TYPEWRITER_OUTPUT_TIME_ONLY_TYPE")
                    ?? configuration.Output.TimeOnlyType,
                TimeOnlyInitializer = Environment.GetEnvironmentVariable(variable: "TYPEWRITER_OUTPUT_TIME_ONLY_INITIALIZER")
                    ?? configuration.Output.TimeOnlyInitializer,
                GuidType = Environment.GetEnvironmentVariable(variable: "TYPEWRITER_OUTPUT_GUID_TYPE")
                    ?? configuration.Output.GuidType,
                DecimalType = Environment.GetEnvironmentVariable(variable: "TYPEWRITER_OUTPUT_DECIMAL_TYPE")
                    ?? configuration.Output.DecimalType,
            },
            Diagnostics = configuration.Diagnostics with
            {
                FailOnWarning = ReadBooleanEnvironment(name: "TYPEWRITER_FAIL_ON_WARNING")
                    ?? configuration.Diagnostics.FailOnWarning,
            },
        };
    }

    private static bool? ReadBooleanEnvironment(string name)
    {
        var value = Environment.GetEnvironmentVariable(variable: name);
        if (string.IsNullOrWhiteSpace(value: value))
        {
            return null;
        }

        return value.Equals(value: "true", comparisonType: StringComparison.OrdinalIgnoreCase)
            || value.Equals(value: "1", comparisonType: StringComparison.OrdinalIgnoreCase)
            || value.Equals(value: "yes", comparisonType: StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string>? ReadStringListEnvironment(string name)
    {
        var value = Environment.GetEnvironmentVariable(variable: name);
        if (string.IsNullOrWhiteSpace(value: value))
        {
            return null;
        }

        return NormalizeExtensions(
            extensions: value.Split(
                separator: [',', ';', ' '],
                options: StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static IReadOnlyList<string>? NormalizeExtensions(IReadOnlyList<string>? extensions)
    {
        if (extensions is null)
        {
            return null;
        }

        var normalized = extensions
            .Select(selector: extension => extension.Trim())
            .Where(predicate: extension => !string.IsNullOrWhiteSpace(value: extension))
            .Select(selector: extension => extension[0] == '.' ? extension : "." + extension)
            .Distinct(comparer: StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return normalized.Length == 0 ? TypewriterConfiguration.DefaultInputExtensions : normalized;
    }

    internal sealed record ConfigurationFile
    {
        public IReadOnlyList<string>? Templates { get; init; }

        public IReadOnlyList<string>? Exclude { get; init; }

        public IReadOnlyList<string>? InputExtensions { get; init; }

        public string? DefaultTargetFramework { get; init; }

        public OutputConfigurationFile? Output { get; init; }

        public DiagnosticsConfigurationFile? Diagnostics { get; init; }
    }

    internal sealed record OutputConfigurationFile
    {
        public string? Newline { get; init; }

        public string? Encoding { get; init; }

        public bool? WriteOnlyWhenChanged { get; init; }

        public bool? DryRun { get; init; }

        public FileNameConvention? FileNameConvention { get; init; }

        public bool? StrictNull { get; init; }

        public IndentStyle? IndentStyle { get; init; }

        public int? IndentSize { get; init; }

        public bool? InsertFinalNewline { get; init; }

        public bool? TrimTrailingWhitespace { get; init; }

        public QuoteStyle? QuoteStyle { get; init; }

        public string? DateType { get; init; }

        public string? DateInitializer { get; init; }

        public string? DateOnlyType { get; init; }

        public string? DateOnlyInitializer { get; init; }

        public string? TimeOnlyType { get; init; }

        public string? TimeOnlyInitializer { get; init; }

        public string? GuidType { get; init; }

        public string? DecimalType { get; init; }
    }

    internal sealed record DiagnosticsConfigurationFile
    {
        public bool? FailOnWarning { get; init; }
    }
}
