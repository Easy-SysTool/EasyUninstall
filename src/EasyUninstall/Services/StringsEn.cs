namespace EasyUninstall.Services;

/// <summary>English string table.</summary>
internal static class StringsEn
{
    public static readonly IReadOnlyDictionary<string, string> Table = new Dictionary<string, string>
    {
        // Main window
        ["AppTitle"] = "EasyUninstall",
        ["NavAll"] = "All apps",
        ["NavDesktop"] = "Desktop apps",
        ["NavStore"] = "Store apps",
        ["NavSettings"] = "Settings",
        ["SearchPlaceholder"] = "Search by name…",
        ["SortName"] = "Name",
        ["SortInstallDate"] = "Install date",
        ["SortPublisher"] = "Publisher",
        ["SortDescending"] = "Descending",
        ["Refresh"] = "Refresh",
        ["EmptyList"] = "No apps found",
        ["Uninstall"] = "Uninstall",
        ["Unknown"] = "Unknown",

        // Detail pane
        ["DetailVersion"] = "Version: {0}",
        ["DetailPublisher"] = "Publisher: {0}",
        ["DetailInstallDate"] = "Installed: {0}",
        ["DetailLocation"] = "Install location: {0}",

        // Uninstall confirm dialog
        ["ConfirmTitle"] = "Uninstall {0}",
        ["ConfirmContent"] = "Uninstall {0} via its official uninstaller?\n\nVersion: {1}\nPublisher: {2}",
        ["Cancel"] = "Cancel",

        // Status messages
        ["StatusLoading"] = "Loading installed apps…",
        ["StatusLoaded"] = "{0} apps found.",
        ["StatusLoadCancelled"] = "Loading cancelled.",
        ["StatusLoadFailed"] = "Failed to load apps: {0}",
        ["StatusRefreshing"] = "Refreshing…",
        ["StatusRefreshed"] = "Refreshed, {0} apps found.",
        ["StatusRefreshCancelled"] = "Refresh cancelled.",
        ["StatusRefreshFailed"] = "Refresh failed: {0}",
        ["StatusUninstalling"] = "Uninstalling {0}…",
        ["StatusCancelled"] = "Cancelled.",
        ["StatusUninstallFailed"] = "Uninstall failed: {0}",
        ["CannotUninstall"] = "Cannot uninstall.",

        // Uninstall results
        ["ResultSuccess"] = "✔ {0}: {1}",
        ["ResultCancelled"] = "Uninstall of {0} cancelled.",
        ["ResultUnknown"] = "? {0}: {1}",
        ["ResultFailed"] = "✘ {0}: {1}",

        // Uninstall service
        ["UninstallNoVerifiedMethod"] = "No verifiable uninstall method for this app; uninstall is disabled.",
        ["UninstallCancelled"] = "Cancelled.",
        ["UninstallError"] = "An error occurred during uninstall: {0}",
        ["UninstallCommandUnverifiable"] = "Cannot verify the uninstall command (ambiguous or points to a missing program); execution blocked: {0}",
        ["UninstallLaunchFailed"] = "Failed to start the uninstaller.",
        ["UninstallStillPresent"] = "The uninstaller started, but the app is still listed. It may not have finished or was cancelled; please refresh to confirm.",
        ["UninstallStillPresentPackage"] = "Uninstall was requested, but the app is still listed; please refresh to confirm.",
        ["UninstallComplete"] = "Uninstalled.",
        ["UninstallMissingPackageId"] = "Missing package identity; cannot uninstall.",

        // Settings dialog
        ["SettingsTitle"] = "Settings",
        ["SettingsTheme"] = "Theme",
        ["ThemeSystem"] = "Follow system",
        ["ThemeLight"] = "Light",
        ["ThemeDark"] = "Dark",
        ["SettingsLanguage"] = "Language",
        ["LanguageSystem"] = "Follow system",
        ["LanguageZh"] = "简体中文",
        ["LanguageEn"] = "English",
        ["LanguageJa"] = "日本語",
        ["Save"] = "Save",
        ["LanguageChangedNote"] = "The language takes effect immediately after saving.",

        // Warnings
        ["WarnUnverifiable"] = "The uninstall command for this app could not be verified; uninstall is disabled.",
    };
}
