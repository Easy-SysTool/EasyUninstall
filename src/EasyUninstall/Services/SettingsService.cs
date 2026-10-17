using EasyUninstall.Contracts;
using Windows.Storage;

namespace EasyUninstall.Services;

/// <summary>Persists settings in local ApplicationData (works in MSIX container).</summary>
public sealed class SettingsService : ISettingsService
{
    private const string ThemeKey = "AppTheme";
    private const string LanguageKey = "AppLanguage";

    private static ApplicationDataContainer Local => ApplicationData.Current.LocalSettings;

    public Task<string> GetThemeAsync()
        => Task.FromResult(Local.Values[ThemeKey] as string ?? "Default");

    public Task SetThemeAsync(string theme)
    {
        Local.Values[ThemeKey] = theme;
        return Task.CompletedTask;
    }

    public Task<string> GetLanguageAsync()
        => Task.FromResult(Local.Values[LanguageKey] as string ?? "System");

    public Task SetLanguageAsync(string language)
    {
        Local.Values[LanguageKey] = language;
        return Task.CompletedTask;
    }
}
