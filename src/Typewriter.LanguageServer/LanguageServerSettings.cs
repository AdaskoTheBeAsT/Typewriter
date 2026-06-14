using System.Text.Json;

namespace Typewriter.LanguageServer;

internal sealed record LanguageServerSettings(
    string? RootPath,
    string? WorkspacePath,
    string? ProjectPath,
    string? Framework,
    bool AllProjects)
{
    public static LanguageServerSettings Default { get; } = new(
        RootPath: null,
        WorkspacePath: null,
        ProjectPath: null,
        Framework: null,
        AllProjects: false);

    public static LanguageServerSettings FromInitializeParams(JsonElement parameters)
    {
        var rootPath = ReadRootPath(parameters: parameters);
        var options = parameters.TryGetProperty(propertyName: "initializationOptions", value: out var initializationOptions)
            ? initializationOptions
            : default;

        return new LanguageServerSettings(
            RootPath: rootPath,
            WorkspacePath: ReadOptionalString(element: options, propertyName: "workspacePath"),
            ProjectPath: ReadOptionalString(element: options, propertyName: "projectPath"),
            Framework: ReadOptionalString(element: options, propertyName: "framework"),
            AllProjects: ReadOptionalBoolean(element: options, propertyName: "allProjects") ?? false);
    }

    public string ResolveWorkspacePath(string documentPath)
    {
        var fallback = RootPath
            ?? Path.GetDirectoryName(path: documentPath)
            ?? Environment.CurrentDirectory;
        return ResolvePath(value: WorkspacePath, basePath: fallback)
            ?? fallback;
    }

    public string? ResolveProjectPath(string workspacePath) =>
        ResolvePath(value: ProjectPath, basePath: GetPathBase(path: workspacePath));

    private static string? ReadRootPath(JsonElement parameters)
    {
        var rootUri = ReadOptionalString(element: parameters, propertyName: "rootUri");
        if (!string.IsNullOrWhiteSpace(value: rootUri))
        {
            return PathFromUri(uri: rootUri);
        }

        return ReadOptionalString(element: parameters, propertyName: "rootPath");
    }

    private static string? ReadOptionalString(
        JsonElement element,
        string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty(propertyName: propertyName, value: out var property)
            || property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        return property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static bool? ReadOptionalBoolean(
        JsonElement element,
        string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty(propertyName: propertyName, value: out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null,
        };
    }

    private static string? ResolvePath(
        string? value,
        string basePath)
    {
        if (string.IsNullOrWhiteSpace(value: value))
        {
            return null;
        }

        return Path.IsPathRooted(path: value)
            ? Path.GetFullPath(path: value)
            : Path.GetFullPath(path: Path.Combine(path1: basePath, path2: value));
    }

    private static string GetPathBase(string path)
    {
        var fullPath = Path.GetFullPath(path: path);
        if (File.Exists(path: fullPath))
        {
            return Path.GetDirectoryName(path: fullPath) ?? Environment.CurrentDirectory;
        }

        return Directory.Exists(path: fullPath) || !Path.HasExtension(path: fullPath)
            ? fullPath
            : Path.GetDirectoryName(path: fullPath) ?? Environment.CurrentDirectory;
    }

    private static string? PathFromUri(string uri)
    {
        return FileUriPath.TryGetPath(uri: uri);
    }
}
