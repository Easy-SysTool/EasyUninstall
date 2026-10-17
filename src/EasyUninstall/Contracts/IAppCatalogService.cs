using EasyUninstall.Models;

namespace EasyUninstall.Contracts;

/// <summary>A source of installed applications (registry, package manager, ...).</summary>
public interface IAppSource
{
    /// <summary>Human-readable source name for diagnostics.</summary>
    string Name { get; }

    /// <summary>Discovers installed apps. Never throws for individual read failures.</summary>
    Task<IReadOnlyList<InstalledApp>> GetAppsAsync(CancellationToken cancellationToken);
}

/// <summary>Merges multiple <see cref="IAppSource"/> results with dedup, filtering, sorting.</summary>
public interface IAppCatalogService
{
    Task<IReadOnlyList<InstalledApp>> GetCatalogAsync(CancellationToken cancellationToken);

    /// <summary>Forces a rescan of all sources.</summary>
    Task<IReadOnlyList<InstalledApp>> RefreshAsync(CancellationToken cancellationToken);
}

/// <summary>Parses a raw UninstallString into a verified executable + argument pair.</summary>
public interface IUninstallCommandParser
{
    /// <summary>Parses the command. Returns null when the command is ambiguous or unverifiable.</summary>
    UninstallCommand? Parse(string? uninstallString, string? quietSwitches);
}

/// <summary>A verified uninstall command: a single executable path plus arguments.</summary>
public sealed record UninstallCommand(string ExecutablePath, string Arguments, bool IsMsi);

/// <summary>Executes the uninstall for one app and reports the verified outcome.</summary>
public interface IUninstallService
{
    Task<UninstallResult> UninstallAsync(InstalledApp app, CancellationToken cancellationToken);
}

/// <summary>Outcome of an uninstall attempt.</summary>
public sealed record UninstallResult(UninstallOutcome Outcome, string Message);

/// <summary>Persists user settings (theme, language, etc.).</summary>
public interface ISettingsService
{
    Task<string> GetThemeAsync();
    Task SetThemeAsync(string theme);

    /// <summary>Gets the persisted UI language: "System", "zh-CN", "en-US" or "ja-JP".</summary>
    Task<string> GetLanguageAsync();
    Task SetLanguageAsync(string language);
}
