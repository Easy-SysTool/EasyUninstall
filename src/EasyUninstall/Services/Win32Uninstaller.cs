using EasyUninstall.Contracts;
using EasyUninstall.Models;

namespace EasyUninstall.Services;

/// <summary>Uninstalls classic Win32 applications via a verified uninstall command.</summary>
public sealed class Win32Uninstaller
{
    private readonly IUninstallCommandParser _parser;

    public Win32Uninstaller(IUninstallCommandParser parser) => _parser = parser;

    /// <summary>Builds a verified, runnable ProcessStartInfo for the app, or null when unverifiable.</summary>
    public UninstallCommand? GetVerifiedCommand(InstalledApp app)
        => _parser.Parse(app.UninstallCommandRaw, quietSwitches: null);

    /// <summary>
    /// Launches the verified uninstaller (interactive). Returns true only when the process
    /// started successfully — launching is NOT uninstall success; caller must rescan to verify.
    /// </summary>
    public async Task<bool> LaunchAsync(UninstallCommand command, CancellationToken cancellationToken)
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = command.ExecutablePath,
            Arguments = command.Arguments,
            UseShellExecute = false
        };

        using var process = System.Diagnostics.Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start uninstaller: {command.ExecutablePath}");

        // Do not wait for interactive uninstallers to exit here — many spawn a child and return.
        // We give the initial process a moment so obvious immediate failures surface.
        await process.WaitForExitAsync(new CancellationTokenSource(TimeSpan.FromSeconds(3)).Token)
            .ContinueWith(_ => { }, CancellationToken.None);

        return true;
    }
}
