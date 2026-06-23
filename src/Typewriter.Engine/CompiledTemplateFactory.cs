using Typewriter.Abstractions;

namespace Typewriter.Engine;

internal sealed class CompiledTemplateFactory : IDisposable
{
    private readonly string _templatePath;
    private TemplateAssemblyLoadContext? _loadContext;
    private Type? _hostType;

    public CompiledTemplateFactory(
        string templatePath,
        Type hostType,
        TemplateAssemblyLoadContext loadContext)
    {
        _templatePath = templatePath;
        _hostType = hostType;
        _loadContext = loadContext;
    }

    public CompiledTemplateHelper CreateHelper(
        ProjectMetadata metadata,
        ICollection<GenerationDiagnostic> diagnostics,
        TemplateRenderDefaults defaults)
    {
        if (_hostType is null || _loadContext is null)
        {
            throw new ObjectDisposedException(objectName: nameof(CompiledTemplateFactory));
        }

        return TemplateRuntimeCompiler.CreateHelper(
            templatePath: _templatePath,
            metadata: metadata,
            diagnostics: diagnostics,
            defaults: defaults,
            hostType: _hostType,
            loadContext: _loadContext,
            unloadLoadContextOnDispose: false);
    }

    public void Dispose()
    {
        _hostType = null;
        _loadContext?.Unload();
        _loadContext = null;
    }
}
