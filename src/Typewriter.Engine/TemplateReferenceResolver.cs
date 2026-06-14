using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Typewriter.Abstractions;

namespace Typewriter.Engine;

internal sealed class TemplateReferenceResolver
{
    private static readonly TimeSpan RestoreTimeout = TimeSpan.FromMinutes(minutes: 2);

    private static readonly string[] TargetFrameworkPreference =
    [
        "net10.0",
        "net9.0",
        "net8.0",
        "net7.0",
        "net6.0",
        "net5.0",
        "netcoreapp3.1",
        "netstandard2.1",
        "netstandard2.0",
        "netstandard1.6",
        "net48",
        "net472",
        "net471",
        "net461",
    ];

    private readonly ICollection<GenerationDiagnostic> _diagnostics;
    private readonly string _templateDirectory;
    private readonly string _templatePath;

    public TemplateReferenceResolver(
        string templatePath,
        ICollection<GenerationDiagnostic> diagnostics)
    {
        _templatePath = templatePath;
        _templateDirectory = Path.GetDirectoryName(path: Path.GetFullPath(path: templatePath)) ?? Environment.CurrentDirectory;
        _diagnostics = diagnostics;
    }

    public IReadOnlyList<MetadataReference> CreateReferences(IEnumerable<string> referenceDirectives)
    {
        var paths = GetTrustedPlatformAssemblyPaths()
            .Concat(second: GetLocalAssemblyPaths())
            .Concat(second: referenceDirectives.SelectMany(selector: ResolveDirective))
            .Where(predicate: path => !string.IsNullOrWhiteSpace(value: path))
            .Distinct(comparer: StringComparer.OrdinalIgnoreCase)
            .Where(predicate: File.Exists)
            .ToArray();

        return paths.Select(selector: path => MetadataReference.CreateFromFile(path: path)).ToArray();
    }

    private static IEnumerable<string> GetTrustedPlatformAssemblyPaths()
    {
        return (AppContext.GetData(name: "TRUSTED_PLATFORM_ASSEMBLIES") as string ?? string.Empty)
            .Split(separator: Path.PathSeparator, options: StringSplitOptions.RemoveEmptyEntries);
    }

    private static IEnumerable<string> GetLocalAssemblyPaths()
    {
        return new[]
            {
                typeof(TemplateRenderer).Assembly.Location,
                typeof(ProjectMetadata).Assembly.Location,
                typeof(Enumerable).Assembly.Location,
                typeof(RuntimeInformation).Assembly.Location,
            }
            .Where(predicate: path => !string.IsNullOrWhiteSpace(value: path));
    }

    private static string GetNuGetPackageRoot()
    {
        var configuredRoot = Environment.GetEnvironmentVariable(variable: "NUGET_PACKAGES");
        if (!string.IsNullOrWhiteSpace(value: configuredRoot))
        {
            return configuredRoot;
        }

        var userProfile = Environment.GetFolderPath(folder: Environment.SpecialFolder.UserProfile);
        return Path.Combine(path1: userProfile, path2: ".nuget", path3: "packages");
    }

    private static string? FindVersionDirectory(
        string packageDirectory,
        string? version)
    {
        if (!string.IsNullOrWhiteSpace(value: version)
            && IsExactPackageVersion(version: version))
        {
            var exact = Path.Combine(path1: packageDirectory, path2: version.ToLowerInvariant());
            return Directory.Exists(path: exact) ? exact : null;
        }

        // For non-exact versions (ranges, floating), prefer newest stable then newest prerelease
        return Directory.EnumerateDirectories(path: packageDirectory)
            .Select(selector: directory => new
            {
                Path = directory,
                Version = ParseVersionOrDefault(value: Path.GetFileName(path: directory)),
                Prerelease = Path.GetFileName(path: directory).Contains(value: '-'),
                RawName = Path.GetFileName(path: directory),
            })
            .OrderByDescending(keySelector: v => v.Prerelease)
            .ThenByDescending(keySelector: v => v.Version)
            .ThenByDescending(keySelector: v => v.RawName, comparer: StringComparer.OrdinalIgnoreCase)
            .Select(selector: v => v.Path)
            .FirstOrDefault();
    }

    private static Version ParseVersionOrDefault(string value)
    {
        var normalized = value.Split(separator: '-', count: 2)[0];
        return Version.TryParse(input: normalized, result: out var version)
            ? version
            : new Version();
    }

    private static string CreateRestoreProject(
        string packageId,
        string? version)
    {
        var escapedPackageId = SecurityElement.Escape(str: packageId) ?? packageId;
        var escapedVersion = SecurityElement.Escape(str: string.IsNullOrWhiteSpace(value: version) ? "*" : version) ?? "*";
#pragma warning disable SA1118 // Parameter should not span multiple lines
        return string.Create(
            provider: CultureInfo.InvariantCulture,
            handler: $$"""
                       <Project Sdk="Microsoft.NET.Sdk">
                         <PropertyGroup>
                           <TargetFramework>net10.0</TargetFramework>
                           <DisableImplicitNuGetFallbackFolder>true</DisableImplicitNuGetFallbackFolder>
                         </PropertyGroup>
                         <ItemGroup>
                           <PackageReference Include="{{escapedPackageId}}" Version="{{escapedVersion}}" PrivateAssets="all" />
                         </ItemGroup>
                       </Project>
                       """);
#pragma warning restore SA1118 // Parameter should not span multiple lines
    }

    private static string? GetLibraryPath(
        JsonElement libraries,
        string libraryName)
    {
        if (!libraries.TryGetProperty(propertyName: libraryName, value: out var library)
            || !library.TryGetProperty(propertyName: "path", value: out var path)
            || path.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return path.GetString();
    }

    private static string? ResolveRestoredAsset(
        IEnumerable<string> packageFolderPaths,
        string libraryPath,
        string assetPath)
    {
        var relativePath = Path.Combine(
            paths: [.. libraryPath.Split(separator: '/'), .. assetPath.Split(separator: '/')]);
        return packageFolderPaths
            .Select(selector: packageFolder => Path.Combine(path1: packageFolder, path2: relativePath))
            .FirstOrDefault(predicate: File.Exists);
    }

    private static bool IsExactPackageVersion(string version) =>
        version.IndexOfAny(anyOf: ['*', '[', ']', '(', ')', ',']) < 0;

    private static string TrimProcessOutput(string output)
    {
        var trimmed = output.Trim();
        return trimmed.Length <= 4000
            ? trimmed
            : trimmed[..4000] + "...";
    }

    private static void TryDeleteDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(path: directory))
            {
#pragma warning disable SCS0018 // Potential Path Traversal vulnerability was found where '{0}' in '{1}' may be tainted by user-controlled data from '{2}' in method '{3}'.
                Directory.Delete(path: directory, recursive: true);
#pragma warning restore SCS0018 // Potential Path Traversal vulnerability was found where '{0}' in '{1}' may be tainted by user-controlled data from '{2}' in method '{3}'.
            }
        }
        catch (IOException ex)
        {
            _ = ex;
        }
        catch (UnauthorizedAccessException ex)
        {
            _ = ex;
        }
    }

    private static string? FindAssetDirectory(string versionDirectory)
    {
        foreach (var assetRootName in new[] { "ref", "lib" })
        {
            var assetRoot = Path.Combine(path1: versionDirectory, path2: assetRootName);
            if (!Directory.Exists(path: assetRoot))
            {
                continue;
            }

            var targetFrameworkDirectories = Directory.EnumerateDirectories(path: assetRoot).ToArray();
            foreach (var targetFramework in TargetFrameworkPreference)
            {
                var match = targetFrameworkDirectories.FirstOrDefault(
                    predicate: directory => Path.GetFileName(path: directory).Equals(value: targetFramework, comparisonType: StringComparison.OrdinalIgnoreCase));
                if (match is not null)
                {
                    return match;
                }
            }

            var firstWithAssemblies = targetFrameworkDirectories.FirstOrDefault(
                predicate: directory => Directory.EnumerateFiles(path: directory, searchPattern: "*.dll", searchOption: SearchOption.TopDirectoryOnly).Any());
            if (firstWithAssemblies is not null)
            {
                return firstWithAssemblies;
            }
        }

        return Directory.EnumerateFiles(path: versionDirectory, searchPattern: "*.dll", searchOption: SearchOption.AllDirectories)
            .Select(selector: Path.GetDirectoryName)
            .FirstOrDefault(predicate: directory => directory is not null);
    }

    private IEnumerable<string> ResolveDirective(string directive)
    {
        if (directive.StartsWith(value: "nuget:", comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            return ResolveNuGetDirective(directive: directive["nuget:".Length..]);
        }

        return ResolveFileDirective(directive: directive);
    }

    private IEnumerable<string> ResolveFileDirective(string directive)
    {
        var candidates = Path.IsPathRooted(path: directive)
            ? [directive]
            : new[]
            {
                Path.Combine(path1: _templateDirectory, path2: directive),
                Path.Combine(path1: Environment.CurrentDirectory, path2: directive),
            };

        var match = candidates.FirstOrDefault(predicate: File.Exists);
        if (match is not null)
        {
            return [match];
        }

        AddDiagnostic(message: $"Template reference '{directive}' was not found.");
        return [];
    }

    private IEnumerable<string> ResolveNuGetDirective(string directive)
    {
        var parts = directive
            .Split(separator: ',', options: StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            AddDiagnostic(message: "NuGet reference is missing a package id.");
            return [];
        }

        var packageId = parts[0];
        var version = parts.Length > 1 ? string.Join(separator: ',', value: parts[1..]) : null;
        var packageRoot = GetNuGetPackageRoot();
        var packageDirectory = Path.Combine(path1: packageRoot, path2: packageId.ToLowerInvariant());
        var versionDirectory = Directory.Exists(path: packageDirectory)
            ? FindVersionDirectory(packageDirectory: packageDirectory, version: version)
            : null;
        if (versionDirectory is null)
        {
            if (TryRestorePackage(packageId: packageId, version: version, packageRoot: packageRoot, references: out var restoredReferences))
            {
                return restoredReferences;
            }

            AddDiagnostic(
                message: version is null
                    ? $"NuGet package '{packageId}' has no locally available versions."
                    : $"NuGet package '{packageId}' version '{version}' was not found in '{packageRoot}'.");
            return [];
        }

        var assetDirectory = FindAssetDirectory(versionDirectory: versionDirectory);
        if (assetDirectory is null)
        {
            AddDiagnostic(message: $"NuGet package '{packageId}' does not contain compatible compile assets.");
            return [];
        }

        return Directory.EnumerateFiles(path: assetDirectory, searchPattern: "*.dll", searchOption: SearchOption.TopDirectoryOnly);
    }

    private bool TryRestorePackage(
        string packageId,
        string? version,
        string packageRoot,
        out IReadOnlyList<string> references)
    {
        references = [];
        var restoreDirectory = Path.Combine(
            path1: Path.GetTempPath(),
            path2: "Typewriter.TemplateRestore",
            path3: Guid.NewGuid().ToString(format: "N"));
        var projectPath = Path.Combine(path1: restoreDirectory, path2: "TemplateReferenceRestore.csproj");
        try
        {
            Directory.CreateDirectory(path: restoreDirectory);
#pragma warning disable SCS0018,SEC0116
            File.WriteAllText(path: projectPath, contents: CreateRestoreProject(packageId: packageId, version: version));
#pragma warning restore SCS0018,SEC0116

            var result = RunDotNetRestore(projectPath: projectPath, packageRoot: packageRoot);
            if (!result.Success)
            {
                AddDiagnostic(
                    message: $"NuGet package '{packageId}' restore failed. {result.Message}");
                return false;
            }

            references = ReadRestoreCompileAssets(projectAssetsPath: Path.Combine(path1: restoreDirectory, path2: "obj", path3: "project.assets.json"));
            if (references.Count == 0)
            {
                AddDiagnostic(message: $"NuGet package '{packageId}' restore produced no compatible compile assets.");
                return false;
            }

            return true;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            AddDiagnostic(message: $"NuGet package '{packageId}' restore failed: {ex.Message}");
            return false;
        }
        finally
        {
            TryDeleteDirectory(directory: restoreDirectory);
        }
    }

    private RestoreResult RunDotNetRestore(
        string projectPath,
        string packageRoot)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                WorkingDirectory = _templateDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            },
        };
        process.StartInfo.ArgumentList.Add(item: "restore");
        process.StartInfo.ArgumentList.Add(item: projectPath);
        process.StartInfo.ArgumentList.Add(item: "--packages");
        process.StartInfo.ArgumentList.Add(item: packageRoot);
        process.StartInfo.ArgumentList.Add(item: "--nologo");
        process.StartInfo.ArgumentList.Add(item: "--verbosity");
        process.StartInfo.ArgumentList.Add(item: "quiet");

        var nugetConfig = FindNearestNuGetConfig();
        if (nugetConfig is not null)
        {
            process.StartInfo.ArgumentList.Add(item: "--configfile");
            process.StartInfo.ArgumentList.Add(item: nugetConfig);
        }

        if (!process.Start())
        {
            return new RestoreResult(Success: false, Message: "The dotnet restore process could not be started.");
        }

        if (!process.WaitForExit(timeout: RestoreTimeout))
        {
            process.Kill(entireProcessTree: true);
            return new RestoreResult(
                Success: false,
                Message: string.Create(provider: CultureInfo.InvariantCulture, handler: $"The dotnet restore process timed out after {RestoreTimeout.TotalSeconds:0} seconds."));
        }

        var output = string.Concat(str0: process.StandardOutput.ReadToEnd(), str1: process.StandardError.ReadToEnd());
        return process.ExitCode == 0
            ? new RestoreResult(Success: true, Message: string.Empty)
            : new RestoreResult(Success: false, Message: TrimProcessOutput(output: output));
    }

#pragma warning disable CC0091,MA0051,S2325,S3776
    private IReadOnlyList<string> ReadRestoreCompileAssets(string projectAssetsPath)
    {
        if (!File.Exists(path: projectAssetsPath))
        {
            return [];
        }

#pragma warning disable SCS0018,SEC0116
        using var document = JsonDocument.Parse(json: File.ReadAllText(path: projectAssetsPath));
#pragma warning restore SCS0018,SEC0116
        var root = document.RootElement;
        if (!root.TryGetProperty(propertyName: "targets", value: out var targets)
            || !root.TryGetProperty(propertyName: "libraries", value: out var libraries)
            || !root.TryGetProperty(propertyName: "packageFolders", value: out var packageFolders))
        {
            return [];
        }

        var packageFolderPaths = new List<string>();
        using (var packageFolderEnumerator = packageFolders.EnumerateObject())
        {
            while (packageFolderEnumerator.MoveNext())
            {
                packageFolderPaths.Add(item: packageFolderEnumerator.Current.Name);
            }
        }

        var references = new List<string>();
        using var targetEnumerator = targets.EnumerateObject();
        while (targetEnumerator.MoveNext())
        {
            var target = targetEnumerator.Current;
            using var libraryEnumerator = target.Value.EnumerateObject();
            while (libraryEnumerator.MoveNext())
            {
                var library = libraryEnumerator.Current;
                if (!library.Value.TryGetProperty(propertyName: "type", value: out var type)
                    || !type.ValueEquals(text: "package")
                    || !library.Value.TryGetProperty(propertyName: "compile", value: out var compileAssets))
                {
                    continue;
                }

                var libraryPath = GetLibraryPath(libraries: libraries, libraryName: library.Name);
                if (libraryPath is null)
                {
                    continue;
                }

                using var compileAssetEnumerator = compileAssets.EnumerateObject();
                while (compileAssetEnumerator.MoveNext())
                {
                    var compileAsset = compileAssetEnumerator.Current;
                    if (!compileAsset.Name.EndsWith(value: ".dll", comparisonType: StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var reference = ResolveRestoredAsset(packageFolderPaths: packageFolderPaths, libraryPath: libraryPath, assetPath: compileAsset.Name);
                    if (reference is not null)
                    {
                        references.Add(item: reference);
                    }
                }
            }
        }

        return references
            .Distinct(comparer: StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
#pragma warning restore CC0091,MA0051,S2325,S3776

    private string? FindNearestNuGetConfig()
    {
        var directory = new DirectoryInfo(path: _templateDirectory);
        while (directory is not null)
        {
            foreach (var fileName in new[] { "NuGet.config", "nuget.config" })
            {
                var candidate = Path.Combine(path1: directory.FullName, path2: fileName);
                if (File.Exists(path: candidate))
                {
                    return candidate;
                }
            }

            directory = directory.Parent;
        }

        return null;
    }

    private void AddDiagnostic(string message)
    {
        _diagnostics.Add(
            item: new GenerationDiagnostic(
                File: _templatePath,
                Line: null,
                Column: null,
                Severity: Typewriter.Abstractions.DiagnosticSeverity.Error,
                Message: message,
                Code: DiagnosticCodes.TemplateParseError));
    }

    private sealed record RestoreResult(bool Success, string Message);
}
