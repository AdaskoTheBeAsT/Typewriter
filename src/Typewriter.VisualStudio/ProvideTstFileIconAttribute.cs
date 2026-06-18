using System;
using Microsoft.VisualStudio.Shell;

namespace Typewriter.VisualStudio;

[AttributeUsage(validOn: AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
internal sealed class ProvideTstFileIconAttribute : RegistrationAttribute
{
    private const string AssociationKey = @"ShellFileAssociations\.tst";
    private const string DefaultIconMoniker = "cd3658cd-b731-4e06-84e9-4d913758da43:1";

    public override void Register(RegistrationContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(paramName: nameof(context));
        }

        using var key = context.CreateKey(name: AssociationKey);
        key.SetValue(valueName: "DefaultIconMoniker", value: DefaultIconMoniker);
    }

    public override void Unregister(RegistrationContext context)
    {
        context?.RemoveKey(name: AssociationKey);
    }
}
