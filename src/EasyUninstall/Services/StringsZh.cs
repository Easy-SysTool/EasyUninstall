namespace EasyUninstall.Services;

/// <summary>Simplified Chinese string table (default / fallback language).</summary>
internal static class StringsZh
{
    public static readonly IReadOnlyDictionary<string, string> Table = new Dictionary<string, string>
    {
        // Main window
        ["AppTitle"] = "EasyUninstall",
        ["NavAll"] = "全部软件",
        ["NavDesktop"] = "桌面应用",
        ["NavStore"] = "商店应用",
        ["NavSettings"] = "设置",
        ["SearchPlaceholder"] = "按名称搜索…",
        ["SortName"] = "名称",
        ["SortInstallDate"] = "安装日期",
        ["SortPublisher"] = "发布者",
        ["SortDescending"] = "降序",
        ["Refresh"] = "刷新",
        ["EmptyList"] = "未发现软件",
        ["Uninstall"] = "卸载",
        ["Unknown"] = "未知",

        // Detail pane
        ["DetailVersion"] = "版本：{0}",
        ["DetailPublisher"] = "发布者：{0}",
        ["DetailInstallDate"] = "安装日期：{0}",
        ["DetailLocation"] = "安装路径：{0}",

        // Uninstall confirm dialog
        ["ConfirmTitle"] = "卸载 {0}",
        ["ConfirmContent"] = "确定要通过官方卸载程序卸载 {0} 吗？\n\n版本：{1}\n发布者：{2}",
        ["Cancel"] = "取消",

        // Status messages
        ["StatusLoading"] = "正在加载软件列表…",
        ["StatusLoaded"] = "共 {0} 项。",
        ["StatusLoadCancelled"] = "加载已取消。",
        ["StatusLoadFailed"] = "加载软件列表失败：{0}",
        ["StatusRefreshing"] = "正在刷新…",
        ["StatusRefreshed"] = "已刷新，共 {0} 项。",
        ["StatusRefreshCancelled"] = "刷新已取消。",
        ["StatusRefreshFailed"] = "刷新失败：{0}",
        ["StatusUninstalling"] = "正在卸载 {0}…",
        ["StatusCancelled"] = "已取消。",
        ["StatusUninstallFailed"] = "卸载失败：{0}",
        ["CannotUninstall"] = "无法卸载。",

        // Uninstall results
        ["ResultSuccess"] = "✔ {0}：{1}",
        ["ResultCancelled"] = "已取消卸载 {0}。",
        ["ResultUnknown"] = "? {0}：{1}",
        ["ResultFailed"] = "✘ {0}：{1}",

        // Uninstall service
        ["UninstallNoVerifiedMethod"] = "此软件没有可验证的卸载方式，已禁用卸载。",
        ["UninstallCancelled"] = "已取消。",
        ["UninstallError"] = "卸载过程中出现错误：{0}",
        ["UninstallCommandUnverifiable"] = "无法验证卸载命令（原始命令有歧义或指向不存在的程序），已阻止执行：{0}",
        ["UninstallLaunchFailed"] = "无法启动卸载程序。",
        ["UninstallStillPresent"] = "卸载程序已启动，但软件仍出现在列表中。可能卸载尚未完成或被用户取消，请刷新后确认。",
        ["UninstallStillPresentPackage"] = "已请求卸载，但软件仍出现在列表中，请刷新确认。",
        ["UninstallComplete"] = "卸载完成。",
        ["UninstallMissingPackageId"] = "缺少包标识，无法卸载。",

        // Settings dialog
        ["SettingsTitle"] = "设置",
        ["SettingsTheme"] = "主题",
        ["ThemeSystem"] = "跟随系统",
        ["ThemeLight"] = "浅色",
        ["ThemeDark"] = "深色",
        ["SettingsLanguage"] = "语言",
        ["LanguageSystem"] = "跟随系统",
        ["LanguageZh"] = "简体中文",
        ["LanguageEn"] = "English",
        ["LanguageJa"] = "日本語",
        ["Save"] = "保存",
        ["LanguageChangedNote"] = "语言将在保存后立即生效。",

        // Warnings
        ["WarnUnverifiable"] = "该软件的卸载命令无法验证，已禁用卸载。",
    };
}
