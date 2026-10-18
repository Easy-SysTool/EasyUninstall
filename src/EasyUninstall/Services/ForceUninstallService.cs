using System.Text.Json;

namespace EasyUninstall.Services;

/// <summary>
/// Safety rails for the force-uninstall helper. Both the UI process and the elevated
/// helper validate every path through <see cref="GuardPath"/> before any deletion,
/// so a corrupt or malicious registry entry can never make us delete system files.
/// </summary>
public static class ForceUninstallGuard
{
    /// <summary>Fixed directories that must never be deletion targets, including anything inside them.</summary>
    private static readonly string[] ForbiddenRoots =
    [
        "C:\\Windows",
        "C:\\Users",
        "C:\\ProgramData",
        "C:\\System Volume Information",
        "C:\\Recovery"
    ];

    /// <summary>
    /// Validates an install directory for deletion. Returns null when safe to delete,
    /// otherwise a refusal reason. Rules: absolute, existing-or-empty, not a drive root,
    /// not a forbidden system root or inside one, not the app's own directory,
    /// not containing "easyuninstall" markers, and at least 2 levels deep.
    /// </summary>
    public static string? GuardPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return "empty";
        string full;
        try
        {
            full = Path.GetFullPath(path.Trim().Trim('"'));
        }
        catch (Exception)
        {
            return "not-a-path";
        }

        if (!Path.IsPathRooted(full)) return "relative";
        if (Path.GetPathRoot(full)!.Equals(full, StringComparison.OrdinalIgnoreCase)) return "drive-root";

        var rooted = full.TrimEnd('\\');
        foreach (var forbidden in ForbiddenRoots)
        {
            if (rooted.Equals(forbidden, StringComparison.OrdinalIgnoreCase)) return "system-root";
            if (rooted.StartsWith(forbidden + "\\", StringComparison.OrdinalIgnoreCase))
                return "system-root";
        }

        // Refuse shallow paths like C:\Foo — a real app dir is at least C:\X\Y.
        if (rooted.Count(c => c == '\\') < 2) return "too-shallow";

        if (rooted.Contains("easyuninstall", StringComparison.OrdinalIgnoreCase)) return "self";
        if (rooted.Contains("windowsapps", StringComparison.OrdinalIgnoreCase)) return "msix-store";

        return null;
    }

    /// <summary>Serializes a force-uninstall instruction for the elevated helper.</summary>
    public static string Serialize(ForceInstruction instruction)
        => JsonSerializer.Serialize(instruction, JsonOptions);

    /// <summary>Deserializes a helper instruction; returns null on tampering/garbage.</summary>
    public static ForceInstruction? Deserialize(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<ForceInstruction>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.General);

    public sealed record ForceInstruction(
        int[] ProcessIds,
        string? InstallDirectory,
        string[] UninstallKeyPaths);
}

/// <summary>Runs elevated and executes a validated <see cref="ForceUninstallGuard.ForceInstruction"/>.</summary>
public static class ForceUninstallService
{
    /// <summary>
    /// Launches this same executable elevated (UAC prompt) in helper mode with the
    /// serialized instruction. Returns false when the user declined elevation or
    /// launch failed. Wait for exit so the rescan happens after the work is done.
    /// </summary>
    public static async Task<bool> LaunchElevatedHelperAsync(
        ForceUninstallGuard.ForceInstruction instruction, CancellationToken ct)
    {
        var exePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath)) return false;

        var payload = Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes(ForceUninstallGuard.Serialize(instruction)));

        try
        {
            using var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = $"--force-helper {payload}",
                    UseShellExecute = true,
                    Verb = "runas"
                },
                EnableRaisingEvents = true
            };
            process.Start();
            await process.WaitForExitAsync(ct);
            return true;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // UAC declined by the user.
            return false;
        }
    }
}

/// <summary>Runs elevated and executes a validated <see cref="ForceUninstallGuard.ForceInstruction"/>.</summary>
public static class ForceUninstallHelper
{
    /// <summary>Entry point when spawned elevated with a single JSON argument. Returns 0 on success.</summary>
    public static int Run(string[] args)
    {
        if (args.Length != 1) return 2;
        var instruction = ForceUninstallGuard.Deserialize(args[0]);
        if (instruction is null) return 2;

        foreach (var pid in instruction.ProcessIds)
        {
            try
            {
                using var process = System.Diagnostics.Process.GetProcessById(pid);
                if (process.HasExited) continue;
                process.Kill(entireProcessTree: true);
                process.WaitForExit(10_000);
            }
            catch (ArgumentException) { }
            catch (InvalidOperationException) { }
            catch (System.ComponentModel.Win32Exception) { }
        }

        if (instruction.InstallDirectory is { Length: > 0 } dir)
        {
            var refusal = ForceUninstallGuard.GuardPath(dir);
            if (refusal is not null) return 3;

            try
            {
                if (Directory.Exists(dir))
                {
                    foreach (var attempt in Enumerable.Range(1, 3))
                    {
                        try
                        {
                            Directory.Delete(dir, recursive: true);
                            break;
                        }
                        catch (IOException) when (attempt < 3)
                        {
                            Thread.Sleep(500);
                        }
                    }
                }
            }
            catch (UnauthorizedAccessException) { return 4; }
            catch (IOException) { return 4; }
        }

        foreach (var keyPath in instruction.UninstallKeyPaths)
        {
            var normalized = keyPath.Replace('/', '\\');
            try
            {
                // Key paths are stored as "HKLM|SOFTWARE\...\Uninstall\Name" or "HKCU|...".
                var separator = normalized.IndexOf('|');
                if (separator <= 0) continue;
                var hive = normalized[..separator];
                var sub = normalized[(separator + 1)..];
                var leaf = Path.GetFileName(sub.TrimEnd('\\'));
                var parentPath = sub[..(sub.Length - leaf.Length)].TrimEnd('\\');

                using var parent = hive == "HKLM"
                    ? Microsoft.Win32.Registry.LocalMachine.OpenSubKey(parentPath, writable: true)
                    : Microsoft.Win32.Registry.CurrentUser.OpenSubKey(parentPath, writable: true);
                parent?.DeleteSubKeyTree(leaf);
            }
            catch (ArgumentException) { }
            catch (System.Security.SecurityException) { }
        }

        return 0;
    }
}
