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
                    IconPath = ResolvePackageIconPath(package),
#pragma warning restore CA1416
                    PackageFullName = package.Id.FullName,
                    CanUninstall = true,
                    IsSystemComponent = true // Store packages always use the package manager instead
                });
            }
        }

        return Task.FromResult<IReadOnlyList<InstalledApp>>(apps);
    }

    /// <summary>
    /// Resolves a package logo to a displayable image path. Package logos on disk are
    /// already image files, so this passes the path through IconExtractService's image
    /// check; scale-factor variants (e.g. Square44x44Logo.scale-200.png) are common,
    /// so the first existing candidate wins.
    /// </summary>
    private static string ResolvePackageIconPath(Package package)
    {
#pragma warning disable CA1416
        try
        {
            var logo = package.Logo;
            if (logo is not null && logo.IsFile)
            {
                return IconExtractService.GetImageFilePath(logo.LocalPath) ?? string.Empty;
            }
            var installedPath = package.InstalledPath;
            if (!string.IsNullOrEmpty(installedPath))
            {
                foreach (var name in new[] { "Square44x44Logo.png", "Square150x150Logo.png", "Logo.png", "AssetLogo.png" })
                {
                    var candidate = IconExtractService.GetImageFilePath(Path.Combine(installedPath, "Assets", name));
                    if (candidate is not null) return candidate;
                }
            }
        }
        catch
        {
            // Logo access can fail for some packages; fall through to empty (placeholder shown).
        }
#pragma warning restore CA1416
        return string.Empty;
    }
}
