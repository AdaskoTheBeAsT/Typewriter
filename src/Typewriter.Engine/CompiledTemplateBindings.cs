using System.Reflection;
using System.Runtime.CompilerServices;

namespace Typewriter.Engine;

internal sealed class CompiledTemplateBindings
{
    // Weak keys allow collectible template assemblies to unload, even though their
    // reflection tables refer back to the declaring types.
    private static readonly ConditionalWeakTable<Type, CompiledTemplateBindings> Cache = [];
    private readonly Type _hostType;
    private readonly ConstructorInfo? _fileConstructor;
    private readonly ConstructorInfo? _settingsConstructor;

    private CompiledTemplateBindings(Type hostType)
    {
        _hostType = hostType;
#pragma warning disable S3011 // Templates can declare non-public constructors and helper methods.
        _fileConstructor = hostType.GetConstructor(
            bindingAttr: BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            types: [typeof(Typewriter.Configuration.Settings), typeof(Typewriter.CodeModel.File)],
            modifiers: null);
        _settingsConstructor = hostType.GetConstructor(
            bindingAttr: BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            types: [typeof(Typewriter.Configuration.Settings)],
            modifiers: null);
        var methods = hostType.GetMethods(
            bindingAttr: BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
#pragma warning restore S3011
        MethodsByName = methods
            .GroupBy(keySelector: method => method.Name, comparer: StringComparer.Ordinal)
            .ToDictionary(
                keySelector: group => group.Key,
                elementSelector: group => (IReadOnlyList<MethodInfo>)group.ToArray(),
                comparer: StringComparer.Ordinal);
        ParametersByMethod = methods.ToDictionary(keySelector: method => method, elementSelector: method => method.GetParameters());
    }

    public IReadOnlyDictionary<string, IReadOnlyList<MethodInfo>> MethodsByName { get; }

    public IReadOnlyDictionary<MethodInfo, ParameterInfo[]> ParametersByMethod { get; }

    public static CompiledTemplateBindings For(Type hostType) =>
        Cache.GetValue(key: hostType, createValueCallback: static type => new CompiledTemplateBindings(hostType: type));

    public object CreateHost(
        Typewriter.Configuration.Settings settings,
        Func<Typewriter.CodeModel.File> fileFactory)
    {
        if (_fileConstructor is not null)
        {
            return _fileConstructor.Invoke(parameters: [settings, fileFactory.Invoke()]);
        }

        return _settingsConstructor is not null
            ? _settingsConstructor.Invoke(parameters: [settings])
            : Activator.CreateInstance(type: _hostType, nonPublic: true)!;
    }
}
