using EasyUninstall.Contracts;
using EasyUninstall.Models;

namespace EasyUninstall.Services;

/// <summary>
/// Orchestrates uninstall: verifies the mechanism, launches the official uninstaller,
/// rescans the catalog, and classifies the outcome (Success / Failed / Cancelled / Unknown).
/// Launching an uninstaller is NOT treated as success by itself.
/// </summary>
public sealed class UninstallService : IUninstallService
{
    private readonly Win32Uninstaller _win32;
    private readonly PackageUninstaller _package;
    private readonly IAppCatalogService _catalog;

    public UninstallService(Win32Uninstaller win32, PackageUninstaller package, IAppCatalogService catalog)
    {
        _win32 = win32;
        _package = package;
        _catalog = catalog;
    }

    public async Task<UninstallResult> UninstallAsync(InstalledApp app, CancellationToken cancellationToken)
    {
        if (!app.CanUninstall)
            return new UninstallResult(UninstallOutcome.Failed,
                app.UnavailableReason ?? LocalizationService.Get("UninstallNoVerifiedMethod"));

        try
        {
            if (app.Category == AppCategory.Store)
            {
                return await UninstallPackageAsync(app, cancellationToken);
            }
            return await UninstallWin32Async(app, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return new UninstallResult(UninstallOutcome.Cancelled, LocalizationService.Get("UninstallCancelled"));
        }
        catch (Exception ex)
        {
            return new UninstallResult(UninstallOutcome.Failed,
                LocalizationService.Format("UninstallError", ex.Message));
        }
    }

    public async Task<UninstallResult> ForceUninstallAsync(InstalledApp app, CancellationToken ct)
    {
        // Only meaningful for desktop apps with an install location outside MSIX.
        if (app.Category == AppCategory.Store)
            return new UninstallResult(UninstallOutcome.Failed,
                LocalizationService.Get("ForceNotApplicable"));

        var guard = ForceUninstallGuard.GuardPath(app.InstallLocation);
        if (guard is not null)
            return new UninstallResult(UninstallOutcome.Failed,
                LocalizationService.Format("ForceRefused", guard));

        // Find running processes whose executable lives under the install directory.
        var processIds = new List<int>();
        if (app.InstallLocation is { Length: > 0 })
        {
            foreach (var process in System.Diagnostics.Process.GetProcesses())
            {
                try
                {
                    var exePath = process.MainModule?.FileName;
                    if (exePath is not null &&
                        exePath.StartsWith(app.InstallLocation, StringComparison.OrdinalIgnoreCase))
                    {
                        processIds.Add(process.Id);
                    }
                }
                catch (System.ComponentModel.Win32Exception) { }
                catch (InvalidOperationException) { }
                finally { process.Dispose(); }
            }
        }

        var keyPaths = RegistryAppSource.GetUninstallKeyPathsFor(app.Id);
        var instruction = new ForceUninstallGuard.ForceInstruction(
            [.. processIds], app.InstallLocation, [.. keyPaths]);

        var launched = await ForceUninstallService.LaunchElevatedHelperAsync(instruction, ct);
        if (!launched)
            return new UninstallResult(UninstallOutcome.Cancelled,
                LocalizationService.Get("ForceElevationCancelled"));

        await Task.Delay(1500, ct);
        var stillPresent = await IsStillPresentAsync(app, ct);
        return stillPresent
            ? new UninstallResult(UninstallOutcome.Unknown, LocalizationService.Get("UninstallStillPresent"))
            : new UninstallResult(UninstallOutcome.Success, LocalizationService.Get("ForceComplete"));
    }

    private async Task<UninstallResult> UninstallWin32Async(InstalledApp app, CancellationToken ct)
    {
        var command = _win32.GetVerifiedCommand(app);
        if (command is null)
            return new UninstallResult(UninstallOutcome.Failed,
                LocalizationService.Format("UninstallCommandUnverifiable", app.UninstallCommandRaw ?? string.Empty));

        var launched = await _win32.LaunchAsync(command, ct);
        if (!launched)
            return new UninstallResult(UninstallOutcome.Failed, LocalizationService.Get("UninstallLaunchFailed"));

        // Verify by rescanning: the app must disappear from the catalog.
        await Task.Delay(2000, ct); // give the uninstaller time to run / cascade
        var stillPresent = await IsStillPresentAsync(app, ct);
        if (stillPresent)
            return new UninstallResult(UninstallOutcome.Unknown,
                LocalizationService.Get("UninstallStillPresent"));

        return new UninstallResult(UninstallOutcome.Success, LocalizationService.Get("UninstallComplete"));
    }

    private async Task<UninstallResult> UninstallPackageAsync(InstalledApp app, CancellationToken ct)
    {
        if (app.PackageFullName is null)
            return new UninstallResult(UninstallOutcome.Failed, LocalizationService.Get("UninstallMissingPackageId"));

        await _package.RemovePackageAsync(app.PackageFullName, ct);

        var stillPresent = await IsStillPresentAsync(app, ct);
        return stillPresent
            ? new UninstallResult(UninstallOutcome.Unknown, LocalizationService.Get("UninstallStillPresentPackage"))
            : new UninstallResult(UninstallOutcome.Success, LocalizationService.Get("UninstallComplete"));
    }

    private async Task<bool> IsStillPresentAsync(InstalledApp app, CancellationToken ct)
    {
        var catalog = await _catalog.GetCatalogAsync(ct);
        return catalog.Any(a =>
            string.Equals(a.Id, app.Id, StringComparison.OrdinalIgnoreCase) &&
            a.Category == app.Category);
    }
}
