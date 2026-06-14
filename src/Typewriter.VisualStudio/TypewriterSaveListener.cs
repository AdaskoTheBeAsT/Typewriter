using System;
using System.IO;
using System.Threading;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace Typewriter.VisualStudio;

internal sealed class TypewriterSaveListener : IVsRunningDocTableEvents3
{
    private readonly TypewriterPackage _package;
    private readonly TypewriterCommandService _commandService;

    public TypewriterSaveListener(
        TypewriterPackage package,
        TypewriterCommandService commandService)
    {
        _package = package;
        _commandService = commandService;
    }

    public int OnAfterSave(uint docCookie)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var options = _package.GetTypewriterOptions();
        if (!options.GenerateOnSave)
        {
            return VSConstants.S_OK;
        }

        var templatePath = GetDocumentMoniker(docCookie: docCookie) ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(value: templatePath)
            && Path.GetExtension(path: templatePath).Equals(value: ".tst", comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            _ = _package.JoinableTaskFactory.RunAsync(
                asyncMethod: () => _commandService.GenerateSavedTemplateAsync(templatePath: templatePath, cancellationToken: CancellationToken.None));
        }

        return VSConstants.S_OK;
    }

    public int OnBeforeSave(uint docCookie) => VSConstants.S_OK;

    public int OnAfterAttributeChange(
        uint docCookie,
        uint grfAttribs) =>
        VSConstants.S_OK;

    public int OnAfterDocumentWindowHide(
        uint docCookie,
        IVsWindowFrame pFrame) =>
        VSConstants.S_OK;

    public int OnAfterFirstDocumentLock(
        uint docCookie,
        uint dwRDTLockType,
        uint dwReadLocksRemaining,
        uint dwEditLocksRemaining) =>
        VSConstants.S_OK;

    public int OnBeforeDocumentWindowShow(
        uint docCookie,
        int fFirstShow,
        IVsWindowFrame pFrame) =>
        VSConstants.S_OK;

    public int OnBeforeLastDocumentUnlock(
        uint docCookie,
        uint dwRDTLockType,
        uint dwReadLocksRemaining,
        uint dwEditLocksRemaining) =>
        VSConstants.S_OK;

    public int OnAfterAttributeChangeEx(
        uint docCookie,
        uint grfAttribs,
        IVsHierarchy pHierOld,
        uint itemidOld,
        string pszMkDocumentOld,
        IVsHierarchy pHierNew,
        uint itemidNew,
        string pszMkDocumentNew) =>
        VSConstants.S_OK;

    public int OnBeforeDocumentWindowShow(
        uint docCookie,
        int fFirstShow,
        IVsWindowFrame pFrame,
        IVsUIHierarchy pHier,
        uint itemid,
        string pszMkDocument) =>
        VSConstants.S_OK;

    private string? GetDocumentMoniker(uint docCookie)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (_package.GetVisualStudioServiceAsync(serviceType: typeof(SVsRunningDocumentTable), cancellationToken: CancellationToken.None)
                .GetAwaiter()
                .GetResult() is not IVsRunningDocumentTable table)
        {
            return null;
        }

        ErrorHandler.ThrowOnFailure(
            hr: table.GetDocumentInfo(
                docCookie: docCookie,
                pgrfRDTFlags: out _,
                pdwReadLocks: out _,
                pdwEditLocks: out _,
                pbstrMkDocument: out var moniker,
                ppHier: out _,
                pitemid: out _,
                ppunkDocData: out _));
        return moniker;
    }
}
