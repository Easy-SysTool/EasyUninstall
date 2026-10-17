using EasyUninstall.Contracts;
using EasyUninstall.Models;

namespace EasyUninstall.Services;

/// <summary>
/// Parses raw UninstallString values into verified executable + argument pairs.
/// Uses rule-based parsing (MSI, quoted/unquoted exe paths with known quiet switches).
/// Never passes anything to cmd.exe or PowerShell; ambiguous strings are rejected.
/// </summary>
public sealed class UninstallCommandParser : IUninstallCommandParser
{
    // Well-known quiet switches used by common uninstallers (NSIS, Inno Setup, InstallShield, WiX/burn).
    private static readonly string[] KnownQuietSwitches =
    [
        "/S", "/s", "/silent", "/SILENT", "/verysilent", "/VERYSILENT",
        "/quiet", "/Q", "/q", "/qn", "/passive", "/PASSIVE", "-q", "-quiet", "--quiet",
        "/x", "/uninstall", "/uninstall ", "/U", "/u"
    ];

    public UninstallCommand? Parse(string? uninstallString, string? quietSwitches)
    {
        if (string.IsNullOrWhiteSpace(uninstallString))
            return null;

        var trimmed = uninstallString.Trim();

        // Standard MSI pattern: MsiExec.exe /X{GUID} or MsiExec.exe /I{GUID} /qn etc.
        if (TryParseMsi(trimmed, out var msi))
            return msi;

        // Explicit msiexec path: "C:\Windows\System32\msiexec.exe" /x{GUID}
        if (Path.GetFileName(trimmed.Trim('"')).Equals("msiexec.exe", StringComparison.OrdinalIgnoreCase))
            return ParseMsiExecPath(trimmed);

        // Reject obviously unsafe hosts — never route through shells or interpreters.
        var exeCandidate = ExtractExecutableToken(trimmed);
        if (exeCandidate is null)
            return null;

        var exeName = Path.GetFileName(exeCandidate);
        if (exeName.Equals("cmd.exe", StringComparison.OrdinalIgnoreCase) ||
            exeName.Equals("powershell.exe", StringComparison.OrdinalIgnoreCase) ||
            exeName.Equals("pwsh.exe", StringComparison.OrdinalIgnoreCase) ||
            exeName.Equals("mshta.exe", StringComparison.OrdinalIgnoreCase) ||
            exeName.Equals("rundll32.exe", StringComparison.OrdinalIgnoreCase) ||
            exeName.Equals("regsvr32.exe", StringComparison.OrdinalIgnoreCase))
            return null;

        // Uninstaller executables must exist on disk to be verified.
        if (!File.Exists(exeCandidate))
            return null;

        // Arguments = everything after the executable token (quoted or unquoted form).
        string rest;
        if (trimmed.StartsWith('"'))
        {
            var end = trimmed.IndexOf('"', 1);
            rest = end + 1 < trimmed.Length ? trimmed[(end + 1)..].Trim() : string.Empty;
        }
        else
        {
            rest = trimmed.Length > exeCandidate.Length
                ? trimmed[trimmed.IndexOf(exeCandidate, StringComparison.OrdinalIgnoreCase)..][exeCandidate.Length..].Trim()
                : string.Empty;
        }

        return new UninstallCommand(exeCandidate, rest, IsMsi: false);
    }

    private static bool TryParseMsi(string trimmed, out UninstallCommand? command)
    {
        command = null;

        // MsiExec.exe /X{GUID} — case-insensitive, optional .exe
        if (trimmed.StartsWith("MsiExec.exe", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("MsiExec", StringComparison.OrdinalIgnoreCase))
        {
            var args = trimmed.Substring("MsiExec.exe".Length).Trim();
            var parsed = ParseMsiExecArgs(args);
            if (parsed is null) return false;
            command = parsed;
            return true;
        }
        return false;
    }

    private static UninstallCommand? ParseMsiExecPath(string trimmed)
    {
        // "C:\Windows\System32\msiexec.exe" /x{GUID}
        var quoted = trimmed.StartsWith('"');
        int exeEnd;
        if (quoted)
        {
            exeEnd = trimmed.IndexOf('"', 1);
            if (exeEnd < 0) return null;
            exeEnd++;
        }
        else
        {
            exeEnd = trimmed.IndexOf(' ');
            if (exeEnd < 0) return null;
        }
        var args = trimmed.Substring(exeEnd).Trim();
        return ParseMsiExecArgs(args);
    }

    private static UninstallCommand? ParseMsiExecArgs(string args)
    {
        if (string.IsNullOrWhiteSpace(args)) return null;

        // Must start with /X or /uninstall (with or without GUID). /I is "modify" — only /X uninstalls.
        if (args.StartsWith("/X", StringComparison.OrdinalIgnoreCase) ||
            args.StartsWith("/uninstall", StringComparison.OrdinalIgnoreCase))
        {
            var guid = args.StartsWith("/uninstall", StringComparison.OrdinalIgnoreCase)
                ? args.Substring("/uninstall".Length).Trim()
                : args.Substring(2).Trim();

            guid = guid.Trim('"');

            if (!IsValidMsiGuid(guid))
                return null;

            // Standard, documented MSI uninstall invocation. We keep the interactive flags off
            // so the MSI UI decides (never silent by default).
            return new UninstallCommand(
                Path.Combine(Environment.SystemDirectory, "msiexec.exe"),
                $"/x{guid}",
                IsMsi: true);
        }
        return null;
    }

    private static bool IsValidMsiGuid(string guid)
    {
        // ProductCode GUID form: 8-4-4-4-12 hex digits.
        if (guid.Length != 38 || guid[0] != '{' || guid[37] != '}')
            return false;
        var body = guid[1..37];
        return body.Count(c => c == '-') == 4
            && body.Replace("-", "").All(char.IsAsciiHexDigit);
    }

    /// <summary>Extracts the executable token: quoted path, else token before first space.</summary>
    private static string? ExtractExecutableToken(string command)
    {
        if (command.StartsWith('"'))
        {
            var end = command.IndexOf('"', 1);
            if (end < 0) return null;
            var path = command[1..end];
            return path.Length > 0 ? path : null;
        }

        // Unquoted: probe progressively longer prefixes so paths with spaces still resolve.
        // e.g. "C:\Program Files\App\unins000.exe" /S — walk space-by-space.
        int idx = command.IndexOf(' ');
        while (idx >= 0)
        {
            var candidate = command[..idx];
            if (File.Exists(candidate))
                return candidate;
            idx = command.IndexOf(' ', idx + 1);
        }

        // Single token without spaces (or no suffix at all).
        var firstSpace = command.IndexOf(' ');
        return firstSpace < 0 ? command : null;
    }
}
