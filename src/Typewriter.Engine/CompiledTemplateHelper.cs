using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using Typewriter.Abstractions;

namespace Typewriter.Engine;

internal sealed class CompiledTemplateHelper : IDisposable
{
    private readonly bool _unloadLoadContextOnDispose;
    private TemplateCodeModelAdapterFactory? _adapterFactory;
    private object? _host;
    private TemplateAssemblyLoadContext? _loadContext;
    private IReadOnlyDictionary<string, IReadOnlyList<MethodInfo>> _methodsByName;
    private IReadOnlyDictionary<MethodInfo, ParameterInfo[]> _parametersByMethod;
    private Typewriter.Configuration.Settings? _settings;

    public CompiledTemplateHelper(
        object host,
        TemplateCodeModelAdapterFactory adapterFactory,
        Typewriter.Configuration.Settings settings,
        TemplateAssemblyLoadContext loadContext,
        bool unloadLoadContextOnDispose = true)
    {
        _host = host;
        _adapterFactory = adapterFactory;
        _settings = settings;
        _loadContext = loadContext;
        _unloadLoadContextOnDispose = unloadLoadContextOnDispose;
#pragma warning disable S3011 // Reflection should not be used to increase accessibility of classes, methods, or fields
        var methods = host.GetType().GetMethods(
            bindingAttr: BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
#pragma warning restore S3011 // Reflection should not be used to increase accessibility of classes, methods, or fields
        _methodsByName = methods
            .GroupBy(keySelector: method => method.Name, comparer: StringComparer.Ordinal)
            .ToDictionary(
                keySelector: group => group.Key,
                elementSelector: group => (IReadOnlyList<MethodInfo>)group.ToArray(),
                comparer: StringComparer.Ordinal);
        _parametersByMethod = methods.ToDictionary(keySelector: method => method, elementSelector: method => method.GetParameters());
        AssemblyLoadContextReference = new WeakReference(target: loadContext);
    }

    public WeakReference AssemblyLoadContextReference { get; }

    public bool UsesOutputFilenameFactory => _settings?.OutputFilenameFactory is not null;

    public Typewriter.Configuration.Settings Settings =>
        _settings ?? throw new ObjectDisposedException(objectName: nameof(CompiledTemplateHelper));

    public bool HasMethod(string methodName)
    {
        return _methodsByName.TryGetValue(key: methodName, value: out var methods)
            && methods.Any(predicate: method => GetParameters(method: method).Length is 1 or 2);
    }

    public bool TryInvoke(
        string methodName,
        object context,
        out object? value,
        out string? error)
    {
        value = null;
        error = null;
        return TryInvoke(methodName: methodName, contexts: [context], value: out value, error: out error);
    }

    public bool TryInvoke(
        string methodName,
        object firstContext,
        object secondContext,
        out object? value,
        out string? error)
    {
        return TryInvoke(methodName: methodName, contexts: [firstContext, secondContext], value: out value, error: out error);
    }

    public bool TryInvokeRenderComplete(
        ProjectMetadata metadata,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(argument: metadata);

        error = null;
        if (_host is null || _adapterFactory is null)
        {
            return false;
        }

        if (!_methodsByName.TryGetValue(key: "OnRenderComplete", value: out var methods))
        {
            return false;
        }

        foreach (var method in methods)
        {
            var parameters = GetParameters(method: method);
            object?[] arguments;
            if (parameters.Length == 0)
            {
                arguments = [];
            }
            else if (parameters.Length == 1
                && _adapterFactory.TryAdapt(context: metadata, targetType: parameters[0].ParameterType, adapted: out var file))
            {
                arguments = [file];
            }
            else
            {
                continue;
            }

            try
            {
                _ = method.Invoke(obj: method.IsStatic ? null : _host, parameters: arguments);
            }
            catch (TargetInvocationException ex)
            {
                error = FormatInvocationError(exception: ex.InnerException ?? ex);
            }

            return true;
        }

        return false;
    }

    public string? ResolveConfiguredOutputPath(
        ProjectMetadata metadata,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(argument: metadata);

        error = null;
        if (_settings is null
            || _adapterFactory is null)
        {
            throw new ObjectDisposedException(objectName: nameof(CompiledTemplateHelper));
        }

        if (_settings.IsSingleFileMode
            && !string.IsNullOrWhiteSpace(value: _settings.SingleFileName))
        {
            return _settings.SingleFileName;
        }

        if (_settings.OutputFilenameFactory is null)
        {
            return null;
        }

        try
        {
            var outputPath = _settings.OutputFilenameFactory(arg: _adapterFactory.CreateFile(project: metadata));
            if (string.IsNullOrWhiteSpace(value: outputPath))
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(value: _settings.OutputDirectory)
                && !Path.IsPathRooted(path: outputPath))
            {
                return Path.Combine(path1: _settings.OutputDirectory, path2: outputPath);
            }

            return outputPath;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            error = FormatInvocationError(exception: ex);
            return null;
        }
    }

    public void Dispose()
    {
        if (_host is IDisposable disposable)
        {
            disposable.Dispose();
        }

        _methodsByName = new Dictionary<string, IReadOnlyList<MethodInfo>>(comparer: StringComparer.Ordinal);
        _parametersByMethod = new Dictionary<MethodInfo, ParameterInfo[]>();
        _adapterFactory = null;
        _host = null;
        _settings = null;
        if (_unloadLoadContextOnDispose)
        {
            _loadContext?.Unload();
        }

        _loadContext = null;
    }

    private static string FormatInvocationError(Exception exception)
    {
        var builder = new StringBuilder()
            .Append(value: exception.GetType().Name)
            .Append(value: ": ")
            .Append(value: exception.Message);
        var templateFrames = ReadTemplateStackFrames(exception: exception);
        if (templateFrames.Count > 0)
        {
            builder.Append(value: " at ").AppendJoin(separator: " <- ", values: templateFrames);
        }

        return builder.ToString();
    }

    private static List<string> ReadTemplateStackFrames(Exception exception)
    {
        var frames = new List<string>();
        foreach (var frame in new StackTrace(e: exception, fNeedFileInfo: true).GetFrames())
        {
            var method = frame.GetMethod();
            if (method?.DeclaringType?.FullName?.StartsWith(
                    value: "Typewriter.Engine.TemplateRuntime.Generated",
                    comparisonType: StringComparison.Ordinal) != true)
            {
                continue;
            }

            var signature = string.Concat(
                str0: method.Name,
                str1: "(",
                str2: string.Join(separator: ", ", values: method.GetParameters().Select(selector: parameter => parameter.ParameterType.Name)),
                str3: ")");
            var fileName = frame.GetFileName();
            var line = frame.GetFileLineNumber();
            frames.Add(
                item: string.IsNullOrEmpty(value: fileName) || line <= 0
                    ? signature
                    : string.Concat(
                        signature, " in ", fileName, ":line ", line.ToString(provider: CultureInfo.InvariantCulture)));
        }

        return frames;
    }

#pragma warning disable S3776
    private bool TryInvoke(
        string methodName,
        object[] contexts,
        out object? value,
        out string? error)
#pragma warning restore S3776
    {
        value = null;
        error = null;
        if (!_methodsByName.TryGetValue(key: methodName, value: out var methods))
        {
            return false;
        }

        foreach (var method in methods)
        {
            var parameters = GetParameters(method: method);
            if (parameters.Length != contexts.Length
                || _adapterFactory is null
                || _host is null)
            {
                continue;
            }

            var arguments = new object?[parameters.Length];
            var canAdapt = true;
            for (var i = 0; i < parameters.Length; i++)
            {
                if (!_adapterFactory.TryAdapt(context: contexts[i], targetType: parameters[i].ParameterType, adapted: out arguments[i]))
                {
                    canAdapt = false;
                    break;
                }
            }

            if (!canAdapt)
            {
                continue;
            }

            try
            {
                value = method.Invoke(obj: method.IsStatic ? null : _host, parameters: arguments);
                return true;
            }
            catch (TargetInvocationException ex)
            {
                error = FormatInvocationError(exception: ex.InnerException ?? ex);
                return true;
            }
            catch (ArgumentException ex)
            {
                error = FormatInvocationError(exception: ex);
                return true;
            }
        }

        return false;
    }

    private ParameterInfo[] GetParameters(MethodInfo method) =>
        _parametersByMethod.TryGetValue(key: method, value: out var parameters)
            ? parameters
            : method.GetParameters();
}
