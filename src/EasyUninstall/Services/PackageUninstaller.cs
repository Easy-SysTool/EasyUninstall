using EasyUninstall.Models;

namespace EasyUninstall.Services;

/// <summary>Uninstalls MSIX packaged apps via PackageManager (official mechanism, per-user).</summary>
public sealed class PackageUninstaller
{
    /// <summary>Removes the package for the current user. Throws on failure with the stage message.</summary>
    public async Task RemovePackageAsync(string packageFullName, CancellationToken cancellationToken)
    {
        var manager = new Windows.Management.Deployment.PackageManager();
        var operation = manager.RemovePackageAsync(packageFullName);

        // Await WinRT IAsyncOperation with progress on the caller's context.
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));

        operation.Completed = (info, status) =>
        {
            if (status == Windows.Foundation.AsyncStatus.Completed)
                tcs.TrySetResult(true);
            else if (status == Windows.Foundation.AsyncStatus.Error)
                tcs.TrySetException(new InvalidOperationException(
                    $"Package removal failed (0x{info.ErrorCode:H8})."));
            else if (status == Windows.Foundation.AsyncStatus.Canceled)
                tcs.TrySetCanceled(cancellationToken);
        };

        await tcs.Task;
    }
}
