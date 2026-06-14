using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace Typewriter.VisualStudio;

internal sealed class TypewriterOutputPane
{
    private static readonly Guid PaneGuid = new(g: "bd6288b2-986d-4498-a90d-02c6bcbdd3c7");
    private readonly IVsOutputWindowPane _pane;

    private TypewriterOutputPane(IVsOutputWindowPane pane)
    {
        _pane = pane;
    }

    public static async Task<TypewriterOutputPane> CreateAsync(
        TypewriterPackage package,
        CancellationToken cancellationToken)
    {
        await package.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken: cancellationToken);
        var outputWindow = await package.GetVisualStudioServiceAsync(serviceType: typeof(SVsOutputWindow), cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: true) as IVsOutputWindow;
        if (outputWindow is null)
        {
            throw new InvalidOperationException(message: "Visual Studio output window service is unavailable.");
        }

        var paneGuid = PaneGuid;
        ErrorHandler.ThrowOnFailure(hr: outputWindow.CreatePane(rguidPane: ref paneGuid, pszPaneName: "Typewriter", fInitVisible: 1, fClearWithSolution: 1));
        ErrorHandler.ThrowOnFailure(hr: outputWindow.GetPane(rguidPane: ref paneGuid, ppPane: out var pane));
        return new TypewriterOutputPane(pane: pane);
    }

    public void WriteLine(string message)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        _pane.OutputString(pszOutputString: message + Environment.NewLine);
    }

    public void Show()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        _pane.Activate();
    }
}
