using Typewriter.Abstractions;
using Typewriter.Engine;
using Typewriter.Roslyn;

namespace Typewriter.LanguageServer;

internal sealed class TemplateDiagnosticService : ITemplateDiagnosticService
{
    public async Task<IReadOnlyList<GenerationDiagnostic>> ValidateAsync(
        TextDocumentState document,
        LanguageServerSettings settings,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(argument: document);
        ArgumentNullException.ThrowIfNull(argument: settings);

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
        return result.Diagnostics;
    }
}
