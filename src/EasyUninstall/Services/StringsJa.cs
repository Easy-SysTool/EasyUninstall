namespace EasyUninstall.Services;

/// <summary>Japanese string table.</summary>
internal static class StringsJa
{
    public static readonly IReadOnlyDictionary<string, string> Table = new Dictionary<string, string>
    {
        // Main window
        ["AppTitle"] = "EasyUninstall",
        ["NavAll"] = "すべてのアプリ",
        ["NavDesktop"] = "デスクトップアプリ",
        ["NavStore"] = "ストアアプリ",
        ["NavSettings"] = "設定",
        ["SearchPlaceholder"] = "名前で検索…",
        ["SortName"] = "名前",
        ["SortInstallDate"] = "インストール日",
        ["SortPublisher"] = "発行元",
        ["SortDescending"] = "降順",
        ["Refresh"] = "更新",
        ["EmptyList"] = "アプリが見つかりません",
        ["Uninstall"] = "アンインストール",
        ["Unknown"] = "不明",

        // Detail pane
        ["DetailVersion"] = "バージョン：{0}",
        ["DetailPublisher"] = "発行元：{0}",
        ["DetailInstallDate"] = "インストール日：{0}",
        ["DetailLocation"] = "インストール先：{0}",

        // Uninstall confirm dialog
        ["ConfirmTitle"] = "{0} をアンインストール",
        ["ConfirmContent"] = "公式のアンインストーラーで {0} をアンインストールしますか？\n\nバージョン：{1}\n発行元：{2}",
        ["Cancel"] = "キャンセル",

        // Status messages
        ["StatusLoading"] = "アプリ一覧を読み込み中…",
        ["StatusLoaded"] = "全 {0} 件。",
        ["StatusLoadCancelled"] = "読み込みがキャンセルされました。",
        ["StatusLoadFailed"] = "アプリ一覧の読み込みに失敗しました：{0}",
        ["StatusRefreshing"] = "更新中…",
        ["StatusRefreshed"] = "更新しました。全 {0} 件。",
        ["StatusRefreshCancelled"] = "更新がキャンセルされました。",
        ["StatusRefreshFailed"] = "更新に失敗しました：{0}",
        ["StatusUninstalling"] = "{0} をアンインストール中…",
        ["StatusCancelled"] = "キャンセルされました。",
        ["StatusUninstallFailed"] = "アンインストールに失敗しました：{0}",
        ["CannotUninstall"] = "アンインストールできません。",

        // Uninstall results
        ["ResultSuccess"] = "✔ {0}：{1}",
        ["ResultCancelled"] = "{0} のアンインストールをキャンセルしました。",
        ["ResultUnknown"] = "? {0}：{1}",
        ["ResultFailed"] = "✘ {0}：{1}",

        // Uninstall service
        ["UninstallNoVerifiedMethod"] = "このアプリには検証可能なアンインストール方法がないため、アンインストールは無効です。",
        ["UninstallCancelled"] = "キャンセルされました。",
        ["UninstallError"] = "アンインストール中にエラーが発生しました：{0}",
        ["UninstallCommandUnverifiable"] = "アンインストール コマンドを検証できません（曖昧または存在しないプログラムを指しています）。実行をブロックしました：{0}",
        ["UninstallLaunchFailed"] = "アンインストーラーを起動できません。",
        ["UninstallStillPresent"] = "アンインストーラーは起動しましたが、アプリが一覧に残っています。未完了またはキャンセルされた可能性があります。更新して確認してください。",
        ["UninstallStillPresentPackage"] = "アンインストールを要求しましたが、アプリが一覧に残っています。更新して確認してください。",
        ["UninstallComplete"] = "アンインストールが完了しました。",
        ["UninstallMissingPackageId"] = "パッケージ ID がないためアンインストールできません。",

        // Settings dialog
        ["SettingsTitle"] = "設定",
        ["SettingsTheme"] = "テーマ",
        ["ThemeSystem"] = "システムに従う",
        ["ThemeLight"] = "ライト",
        ["ThemeDark"] = "ダーク",
        ["SettingsLanguage"] = "言語",
        ["LanguageSystem"] = "システムに従う",
        ["LanguageZh"] = "简体中文",
        ["LanguageEn"] = "English",
        ["LanguageJa"] = "日本語",
        ["Save"] = "保存",
        ["LanguageChangedNote"] = "言語は保存後すぐに反映されます。",

        // Warnings
        ["WarnUnverifiable"] = "このアプリのアンインストール コマンドを検証できないため、アンインストールは無効です。",
    };
}
