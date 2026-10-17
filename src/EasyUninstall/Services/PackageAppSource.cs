using EasyUninstall.Contracts;
using EasyUninstall.Models;
using Windows.ApplicationModel;
using Windows.Management.Deployment;

namespace EasyUninstall.Services;

/// <summary>Discovers current-user MSIX/Store packages via PackageManager.</summary>
public sealed class PackageAppSource : IAppSource
{
    public string Name => "Packages";

    public Task<IReadOnlyList<InstalledApp>> GetAppsAsync(CancellationToken cancellationToken)
    {
        var manager = new PackageManager();
        var packages = manager.FindPackagesForUser(string.Empty);

        var apps = new List<InstalledApp>();
        foreach (var package in packages)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Only show main packages that can plausibly be removed; skip frameworks/resources.
            if (!package.IsFramework && package.SignatureKind != PackageSignatureKind.System)
            {
                apps.Add(new InstalledApp
                {
                    Id = package.Id.FullName,
                    Category = AppCategory.Store,
#pragma warning disable CS8601 // DisplayName is non-null when IsNullOrWhiteSpace is false
                    DisplayName = string.IsNullOrWhiteSpace(package.DisplayName) ? package.Id.Name : package.DisplayName!.Trim(),
#pragma warning restore CS8601
                    DisplayVersion = package.Id.Version.ToString() ?? string.Empty,
                    Publisher = package.Id.Publisher is { Length: > 0 } pub ? pub : string.Empty,
                    InstallDate = package.InstalledDate.UtcDateTime,
#pragma warning disable CA1416
                    InstallLocation = package.InstalledPath is { Length: > 0 } loc ? loc : null,
#pragma warning restore CA1416
                    IconPath = string.Empty,
                    PackageFullName = package.Id.FullName,
                    CanUninstall = true
                });
            }
        }

        return Task.FromResult<IReadOnlyList<InstalledApp>>(apps);
    }
}
