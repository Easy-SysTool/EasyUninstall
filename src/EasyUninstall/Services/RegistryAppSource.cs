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
            IconPath = ExtractIconPath(iconPath),
            UninstallCommandRaw = uninstallString,
            CanUninstall = canUninstall,
            UnavailableReason = canUninstall
                ? null
                : "卸载命令缺失或无法验证，已禁用卸载。"
        };
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
}
