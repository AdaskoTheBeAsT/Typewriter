using Typewriter.CodeModel;

namespace Typewriter.Engine;

internal sealed class MappedCodeType : Typewriter.CodeModel.Type
{
    private readonly Lazy<IInterfaceCollection> _interfaces;

    public MappedCodeType(Func<IInterfaceCollection> interfacesFactory)
    {
        // Interface arguments can refer back to the type whose model is being built.
        _interfaces = new Lazy<IInterfaceCollection>(valueFactory: interfacesFactory);
    }

    public override IInterfaceCollection Interfaces
    {
        get => _interfaces.Value;
        init => _interfaces = new Lazy<IInterfaceCollection>(valueFactory: () => value);
    }

    public bool UseResolvedDefault { get; init; }
}
