using EasyUninstall.Models;
using EasyUninstall.Services;
using Xunit;

namespace EasyUninstall.Tests;

public class UninstallCommandParserTests
{
    private readonly UninstallCommandParser _parser = new();

    [Theory]
    [InlineData("MsiExec.exe /I{GUID}")]
    [InlineData("MsiExec.exe /X{BAD}")]
    [InlineData("MsiExec.exe /X{00000000-0000-0000-0000-00000000000Z}")]
    public void Parse_MsiInvalidGuid_ReturnsNull(string raw)
    {
        Assert.Null(_parser.Parse(raw, null));
    }

    [Fact]
    public void Parse_MsiValidGuid_ReturnsMsiCommand()
    {
        var cmd = _parser.Parse("MsiExec.exe /X{12345678-1234-1234-1234-123456789012}", null);
        Assert.NotNull(cmd);
        Assert.True(cmd.IsMsi);
        Assert.EndsWith("msiexec.exe", cmd.ExecutablePath, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("/x{12345678-1234-1234-1234-123456789012}", cmd.Arguments);
    }

    [Fact]
    public void Parse_CmdHost_Rejects()
    {
        Assert.Null(_parser.Parse("cmd.exe /c del /q C:\\app", null));
    }

    [Fact]
    public void Parse_PowerShellHost_Rejects()
    {
        Assert.Null(_parser.Parse("\"C:\\Windows\\System32\\WindowsPowerShell\\v1.0\\powershell.exe\" -Command Remove-Item", null));
    }

    [Fact]
    public void Parse_NonexistentExecutable_ReturnsNull()
    {
        Assert.Null(_parser.Parse("\"C:\\definitely\\not\\here\\unins000.exe\" /S", null));
    }

    [Fact]
    public void Parse_QuotedExistingExecutable_ExtractsPathAndArgs()
    {
        // Use a file that always exists.
        var target = Path.Combine(Environment.SystemDirectory, "notepad.exe");
        var cmd = _parser.Parse($"\"{target}\" /S", null);
        Assert.NotNull(cmd);
        Assert.Equal(target, cmd.ExecutablePath, ignoreCase: true);
        Assert.Equal("/S", cmd.Arguments);
    }

    [Fact]
    public void Parse_Empty_ReturnsNull()
    {
        Assert.Null(_parser.Parse(null, null));
        Assert.Null(_parser.Parse("", null));
        Assert.Null(_parser.Parse("   ", null));
    }

    [Theory]
    [InlineData("rundll32.exe something.dll,Entry")]
    [InlineData("mshta.exe \"C:\\evil\\x.hta\"")]
    public void Parse_UnsafeHosts_Reject(string raw)
    {
        Assert.Null(_parser.Parse(raw, null));
    }
}

public class RegistryAppSourceTests
{
    [Theory]
    [InlineData("20240315", "2024-03-15")]
    [InlineData("", null)]
    [InlineData(null, null)]
    [InlineData("not-a-date", null)]
    public void ParseInstallDate_HandlesFormats(string? raw, string? expected)
    {
        var result = EasyUninstall.Services.RegistryAppSource.ParseInstallDate(raw);
        if (expected is null)
            Assert.Null(result);
        else
            Assert.Equal(DateTime.Parse(expected), result);
    }

    [Theory]
    [InlineData("C:\\app\\icon.ico,0", "C:\\app\\icon.ico")]
    [InlineData("\"C:\\app dir\\icon.exe\"", "C:\\app dir\\icon.exe")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void ExtractIconPath_HandlesFormats(string? raw, string expected)
    {
        Assert.Equal(expected, EasyUninstall.Services.RegistryAppSource.ExtractIconPath(raw));
    }
}

public class AppCatalogExtensionsTests
{
    private static InstalledApp Make(string name, string publisher = "", DateTime? date = null,
        AppCategory cat = AppCategory.Desktop, string id = "") => new()
    {
        Id = string.IsNullOrEmpty(id) ? name : id,
        Category = cat,
        DisplayName = name,
        Publisher = publisher,
        InstallDate = date
    };

    [Fact]
    public void Search_FiltersByName()
    {
        var apps = new[] { Make("Firefox"), Make("Chrome"), Make("Firebird") };
        var result = apps.Search("fire").ToList();
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void FilterByCategory_Works()
    {
        var apps = new[] { Make("A"), Make("B", cat: AppCategory.Store) };
        Assert.Single(apps.FilterByCategory(AppCategory.Store));
        Assert.Equal(2, apps.FilterByCategory(null).Count());
    }

    [Fact]
    public void Sort_ByDate_Descending()
    {
        var apps = new[]
        {
            Make("Old", date: new DateTime(2020, 1, 1)),
            Make("New", date: new DateTime(2024, 1, 1)),
            Make("None")
        };
        var sorted = apps.Sort(AppCatalogExtensions.SortField.InstallDate, descending: true).ToList();
        Assert.Equal("New", sorted[0].DisplayName);
        Assert.Equal("None", sorted[2].DisplayName);
    }

    [Fact]
    public void Sort_ByPublisher()
    {
        var apps = new[] { Make("B", publisher: "Zeta"), Make("A", publisher: "Alpha") };
        var sorted = apps.Sort(AppCatalogExtensions.SortField.Publisher).ToList();
        Assert.Equal("A", sorted[0].DisplayName);
    }
}

public class ForceUninstallGuardTests
{
    [Theory]
    [InlineData(null, "empty")]
    [InlineData("", "empty")]
    [InlineData("C:\\", "drive-root")]
    [InlineData("C:\\Windows", "system-root")]
    [InlineData("C:\\Windows\\System32\\Fakes", "system-root")]
    [InlineData("C:\\Users", "system-root")]
    [InlineData("C:\\Users\\someone\\App", "system-root")]
    [InlineData("C:\\Foo", "too-shallow")]
    [InlineData("F:\\EasyUninstall\\cache", "self")]
    [InlineData("C:\\Program Files\\WindowsApps\\Pkg", "msix-store")]
    public void GuardPath_DangerousPaths_Refused(string? path, string expectedReason)
    {
        Assert.Equal(expectedReason, ForceUninstallGuard.GuardPath(path));
    }

    [Theory]
    [InlineData("C:\\Apps\\SomeVendor\\SomeApp")]
    [InlineData("D:\\Tools\\Editor\\bin")]
    [InlineData("C:\\Program Files\\Vendor\\SomeApp")]
    [InlineData("C:\\Program Files (x86)\\Vendor\\SomeApp")]
    public void GuardPath_PlausibleAppDirectories_Allowed(string path)
    {
        Assert.Null(ForceUninstallGuard.GuardPath(path));
    }

    [Fact]
    public void Serialize_RoundTrip_PreservesInstruction()
    {
        var original = new ForceUninstallGuard.ForceInstruction(
            [123, 456], "C:\\Apps\\Test", ["HKLM|SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\Test"]);
        var json = ForceUninstallGuard.Serialize(original);
        var restored = ForceUninstallGuard.Deserialize(json);
        Assert.NotNull(restored);
        Assert.Equal(original.ProcessIds, restored.ProcessIds);
        Assert.Equal(original.InstallDirectory, restored.InstallDirectory);
        Assert.Equal(original.UninstallKeyPaths, restored.UninstallKeyPaths);
    }

    [Fact]
    public void Deserialize_Garbage_ReturnsNull()
    {
        Assert.Null(ForceUninstallGuard.Deserialize("not json{{{"));
    }
}
