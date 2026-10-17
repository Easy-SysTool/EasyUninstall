namespace EasyUninstall.Models;

/// <summary>Category of an installed application.</summary>
public enum AppCategory
{
    /// <summary>Win32 desktop application discovered from the registry.</summary>
    Desktop,
    /// <summary>Microsoft Store / MSIX packaged application.</summary>
    Store
}

/// <summary>Result state of an uninstall attempt.</summary>
public enum UninstallOutcome
{
    /// <summary>The uninstaller ran and the app no longer appears in the catalog.</summary>
    Success,
    /// <summary>The uninstaller reported a failure or exited with an error code.</summary>
    Failed,
    /// <summary>The user cancelled before the uninstaller started.</summary>
    Cancelled,
    /// <summary>The uninstaller ran but the result could not be verified.</summary>
    Unknown
}

/// <summary>A single installed application shown in the catalog.</summary>
public sealed class InstalledApp
{
    /// <summary>Stable identity within its source (registry key name or package full name).</summary>
    public required string Id { get; init; }

    /// <summary>Which source discovered this app.</summary>
    public required AppCategory Category { get; init; }

    public required string DisplayName { get; init; }
    public string DisplayVersion { get; init; } = string.Empty;
    public string Publisher { get; init; } = string.Empty;
    public DateTime? InstallDate { get; init; }
    public string? InstallLocation { get; init; }
    public string IconPath { get; init; } = string.Empty;
    public string? UninstallCommandRaw { get; init; }
    public string? PackageFullName { get; init; }

    /// <summary>Whether a verified uninstall mechanism is available.</summary>
    public bool CanUninstall { get; init; }

    /// <summary>Reason uninstall is unavailable, when <see cref="CanUninstall"/> is false.</summary>
    public string? UnavailableReason { get; init; }
}
