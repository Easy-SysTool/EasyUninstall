using EasyUninstall.Contracts;
using EasyUninstall.Models;
using Microsoft.Win32;

namespace EasyUninstall.Services;

/// <summary>Discovers classic Win32 desktop apps from the uninstall registry keys (HKLM + HKCU, per-user scope).</summary>
public sealed class RegistryAppSource : IAppSource
{
    public string Name => "Registry";

    private static readonly string[] UninstallKeys =
    [
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
        @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
    ];

    public Task<IReadOnlyList<InstalledApp>> GetAppsAsync(CancellationToken cancellationToken)
    {
        var apps = new List<InstalledApp>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var root in new[] { Registry.LocalMachine, Registry.CurrentUser })
        {
            foreach (var sub in UninstallKeys)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var key = root.OpenSubKey(sub);
                if (key is null) continue;

                foreach (var childName in key.GetSubKeyNames())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    using var child = key.OpenSubKey(childName);
                    if (child is null) continue;

                    var app = ReadApp(child, childName);
                    if (app is null) continue;
                    if (!seen.Add(app.Id)) continue;
                    apps.Add(app);
                }
            }
        }

        return Task.FromResult<IReadOnlyList<InstalledApp>>(apps);
    }

    /// <summary>
    /// Enumerates the uninstall registry key paths ("HKLM|SOFTWARE\...\Uninstall\Name")
    /// whose key name matches the app id. The elevated force-uninstall helper deletes
    /// exactly these keys and nothing else.
    /// </summary>
    public static IReadOnlyList<string> GetUninstallKeyPathsFor(string appId)
    {
        var paths = new List<string>();
        foreach (var (hive, label) in new[] { (Registry.LocalMachine, "HKLM"), (Registry.CurrentUser, "HKCU") })
        {
            foreach (var sub in UninstallKeys)
            {
                using var key = hive.OpenSubKey(sub);
                if (key is null) continue;
                if (key.GetSubKeyNames().Any(n => n.Equals(appId, StringComparison.OrdinalIgnoreCase)))
                {
                    paths.Add($"{label}|{sub}\\{appId}");
                }
            }
        }
        return paths;
    }

    private static InstalledApp? ReadApp(RegistryKey key, string keyName)
    {
        var displayName = key.GetValue("DisplayName") as string;
        if (string.IsNullOrWhiteSpace(displayName)) return null;

        // System components / hotfixes are noise.
        if (key.GetValue("SystemComponent") is int sc && sc != 0) return null;
        if (key.GetValue("ParentKeyName") is string) return null;
        if (displayName.StartsWith("KB", StringComparison.OrdinalIgnoreCase) &&
            key.GetValue("UninstallString") is null)
            return null;

        var uninstallString = key.GetValue("UninstallString") as string;
        var quietSwitches = key.GetValue("QuietUninstallString") as string;
        var installLocation = key.GetValue("InstallLocation") as string;
        var iconPath = key.GetValue("DisplayIcon") as string;
        var version = key.GetValue("DisplayVersion") as string ?? string.Empty;
        var publisher = key.GetValue("Publisher") as string ?? string.Empty;
        var installDate = ParseInstallDate(key.GetValue("InstallDate") as string);

        // Verify uninstallability using the parser rules (existence of a runnable exe / valid MSI GUID).
        var parser = new UninstallCommandParser();
        var command = parser.Parse(uninstallString, quietSwitches);
        var canUninstall = command is not null;

        return new InstalledApp
        {
            Id = keyName,
            Category = AppCategory.Desktop,
            DisplayName = displayName,
            DisplayVersion = version,
            Publisher = publisher,
            InstallDate = installDate,
            InstallLocation = string.IsNullOrWhiteSpace(installLocation) ? null : installLocation,
            IconPath = ResolveIconPath(iconPath, command?.ExecutablePath),
            UninstallCommandRaw = uninstallString,
            CanUninstall = canUninstall,
            IsSystemComponent = IsSystemComponent(keyName, displayName, publisher),
            UnavailableReason = canUninstall
                ? null
                : "卸载命令缺失或无法验证，已禁用卸载。"
        };
    }

    /// <summary>
    /// Heuristics identifying OS components / platform runtimes that must never be
    /// force-removed. Conservative: matched against registry key name, display name
    /// and publisher; anything not matched stays force-eligible.
    /// </summary>
    public static bool IsSystemComponent(string keyName, string displayName, string publisher)
    {
        // Microsoft platform runtimes: redistributables, .NET, Edge WebView etc.
        if (publisher.Contains("Microsoft Corporation", StringComparison.OrdinalIgnoreCase))
        {
            string[] runtimeMarkers =
            [
                "Visual C++", "Redistributable", ".NET", "Microsoft Edge",
                "WebView", "OneDrive", "Microsoft Visual Studio", "Windows App",
                "SQL Server", "Office"
            ];
            if (runtimeMarkers.Any(m => displayName.Contains(m, StringComparison.OrdinalIgnoreCase)))
                return true;
        }

        // Driver packages, update helpers, platform keys.
        string[] keyMarkers = ["driver", "kb", "update", "runtime", "redist"];
        if (keyMarkers.Any(m => keyName.Contains(m, StringComparison.OrdinalIgnoreCase)))
            return true;

        string[] nameMarkers =
        [
            "Microsoft Visual C++", "redistributable", "runtime", "driver",
            "Windows Driver", "系統组件", "系统组件"
        ];
        if (nameMarkers.Any(m => displayName.Contains(m, StringComparison.OrdinalIgnoreCase)))
            return true;

        return false;
    }

    public static DateTime? ParseInstallDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        // Standard registry format: yyyymmdd
        if (raw.Length == 8 && raw.All(char.IsDigit) &&
            DateTime.TryParseExact(raw, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var date))
            return date;
        if (DateTime.TryParse(raw, out var parsed)) return parsed;
        return null;
    }

    public static string ExtractIconPath(string? displayIcon)
    {
        if (string.IsNullOrWhiteSpace(displayIcon)) return string.Empty;
        // DisplayIcon may be "path,index" or just a path.
        var comma = displayIcon.LastIndexOf(',');
        var path = comma > 0 ? displayIcon[..comma] : displayIcon;
        return path.Trim().Trim('"');
    }

    /// <summary>
    /// Extracts a display icon to a cached PNG. Prefers the registry DisplayIcon entry
    /// (with its icon index when present); falls back to the verified uninstall executable.
    /// Returns an empty string when no icon can be resolved.
    /// </summary>
    private static string ResolveIconPath(string? displayIcon, string? uninstallExe)
    {
        IconSource? display = null;
        if (!string.IsNullOrWhiteSpace(displayIcon))
        {
            var raw = displayIcon.Trim();
            // "path,index" form: keep the index for icon extraction.
            var comma = raw.LastIndexOf(',');
            if (comma > 0 && int.TryParse(raw[(comma + 1)..].Trim(), out var index) && index >= 0)
            {
                var path = raw[..comma].Trim().Trim('"');
                if (path.Length > 0) display = new IconSource(path, index);
            }
            else
            {
                var path = raw.Trim('"');
                if (path.Length > 0) display = new IconSource(path);
            }
        }

        IconSource? exe = uninstallExe is { Length: > 0 } ? new IconSource(uninstallExe) : null;
        return IconExtractService.GetIconPath(display, exe) ?? string.Empty;
    }
}
