using System.Collections.Concurrent;
using System.Globalization;

namespace EasyUninstall.Services;

/// <summary>Supported UI languages.</summary>
public enum AppLanguage
{
    /// <summary>Follow the OS display language.</summary>
    System,
    SimplifiedChinese,
    English,
    Japanese
}

/// <summary>
/// Centralized UI-string localization. Strings live in per-language dictionaries;
/// <see cref="SetLanguage"/> swaps the active table at runtime. Falls back to
/// SimplifiedChinese for any missing key (the app's origin language).
/// </summary>
public static class LocalizationService
{
    private static ConcurrentDictionary<AppLanguage, IReadOnlyDictionary<string, string>> _tables = new();
    private static volatile IReadOnlyDictionary<string, string> _current;
    private static AppLanguage _language = AppLanguage.System;

    public static event EventHandler? LanguageChanged;

    static LocalizationService()
    {
        _tables[AppLanguage.SimplifiedChinese] = StringsZh.Table;
        _tables[AppLanguage.English] = StringsEn.Table;
        _tables[AppLanguage.Japanese] = StringsJa.Table;
        _current = ResolveTable(AppLanguage.System);
    }

    /// <summary>The currently active language (System resolves to a concrete table).</summary>
    public static AppLanguage SelectedLanguage
    {
        get => _language;
        set
        {
            if (_language == value) return;
            _language = value;
            _current = ResolveTable(value);
            LanguageChanged?.Invoke(null, EventArgs.Empty);
        }
    }

    /// <summary>Concrete language backing the current table (for display/debug).</summary>
    public static AppLanguage EffectiveLanguage { get; private set; }

    /// <summary>Gets the localized string for <paramref name="key"/>.</summary>
    public static string Get(string key) =>
        _current.TryGetValue(key, out var value) ? value : key;

    /// <summary>Formats a localized string with arguments.</summary>
    public static string Format(string key, params object[] args) =>
        string.Format(Get(key), args);

    private static IReadOnlyDictionary<string, string> ResolveTable(AppLanguage language)
    {
        if (language != AppLanguage.System)
        {
            EffectiveLanguage = language;
            return _tables[language];
        }

        // Follow the OS UI language: zh → Chinese, ja → Japanese, otherwise English.
        var os = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        var resolved = os switch
        {
            "zh" => AppLanguage.SimplifiedChinese,
            "ja" => AppLanguage.Japanese,
            _ => AppLanguage.English
        };
        EffectiveLanguage = resolved;
        return _tables[resolved];
    }
}
