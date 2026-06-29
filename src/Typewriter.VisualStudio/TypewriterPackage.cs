using System;
using System.ComponentModel.Design;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace Typewriter.VisualStudio;

[PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
[InstalledProductRegistration(productName: "Typewriter", productDetails: "Generates TypeScript from C# Typewriter templates.", productId: "4.5.3")]
[ProvideMenuResource(resourceID: "Menus.ctmenu", version: 1)]
[ProvideOptionPage(pageType: typeof(TypewriterOptions), categoryName: "Typewriter", pageName: "General", categoryResourceID: 0, pageNameResourceID: 0, supportsAutomation: true)]
[ProvideAutoLoad(cmdUiContextGuid: VSConstants.UICONTEXT.SolutionExists_string, flags: PackageAutoLoadFlags.BackgroundLoad)]
[ProvideAutoLoad(cmdUiContextGuid: VSConstants.UICONTEXT.NoSolution_string, flags: PackageAutoLoadFlags.BackgroundLoad)]
[ProvideTstFileIcon]
[Guid(guid: TypewriterVisualStudioConstants.PackageGuidString)]
public sealed class TypewriterPackage : AsyncPackage
{
    private TypewriterDiagnosticReporter? _diagnosticReporter;
    private TypewriterCommandService? _commandService;
    private TypewriterPersistentGenerationClient? _persistentGenerationClient;
    private TypewriterSaveListener? _saveListener;
    private IVsRunningDocumentTable? _runningDocumentTable;
    private uint _runningDocumentTableCookie;

    internal static TypewriterPackage? Current { get; private set; }

    internal async Task<object?> GetVisualStudioServiceAsync(
        Type serviceType,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await GetServiceAsync(serviceType: serviceType).ConfigureAwait(continueOnCapturedContext: false);
    }

    internal TypewriterOptions GetTypewriterOptions()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        return (TypewriterOptions)GetDialogPage(dialogPageType: typeof(TypewriterOptions));
    }

    protected override async Task InitializeAsync(
        CancellationToken cancellationToken,
        IProgress<ServiceProgressData> progress)
    {
        await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken: cancellationToken);
        Current = this;

        var outputPane = await TypewriterOutputPane.CreateAsync(package: this, cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: true);
        _diagnosticReporter = new TypewriterDiagnosticReporter(package: this);
        _persistentGenerationClient = new TypewriterPersistentGenerationClient();
        _commandService = new TypewriterCommandService(
            package: this,
            outputPane: outputPane,
            diagnosticReporter: _diagnosticReporter,
            persistentGenerationClient: _persistentGenerationClient);
        await _commandService.InitializeAsync(cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: true);

        _runningDocumentTable = await GetVisualStudioServiceAsync(serviceType: typeof(SVsRunningDocumentTable), cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: true) as IVsRunningDocumentTable;
        if (_runningDocumentTable is not null)
        {
            _saveListener = new TypewriterSaveListener(package: this, commandService: _commandService);
            ErrorHandler.ThrowOnFailure(
                hr: _runningDocumentTable.AdviseRunningDocTableEvents(pSink: _saveListener, pdwCookie: out _runningDocumentTableCookie));
        }
    }

    protected override void Dispose(bool disposing)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (disposing)
        {
            if (_runningDocumentTable is not null && _runningDocumentTableCookie != 0)
            {
                _runningDocumentTable.UnadviseRunningDocTableEvents(dwCookie: _runningDocumentTableCookie);
                _runningDocumentTableCookie = 0;
            }

            _diagnosticReporter?.Dispose();
            _persistentGenerationClient?.Dispose();
            _saveListener?.Dispose();
            if (ReferenceEquals(objA: Current, objB: this))
            {
                Current = null;
            }
        }

        base.Dispose(disposing: disposing);
    }
}
