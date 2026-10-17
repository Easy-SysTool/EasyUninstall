using EasyUninstall.Contracts;
using EasyUninstall.Services;
using EasyUninstall.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace EasyUninstall;

public sealed partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private readonly ISettingsService _settings;
    private CancellationTokenSource _cts = new();

    public MainWindow(MainViewModel vm, ISettingsService settings)
    {
        _settings = settings;
        _vm = vm;
        InitializeComponent();
        Title = "EasyUninstall";

        // Custom title bar: system caption buttons remain, but we draw our own
        // title row (app icon + title text) and let content extend into the bar.
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.TitleBar.PreferredHeightOption = Microsoft.UI.Windowing.TitleBarHeightOption.Standard;
        ApplyTitleBarThemeColors();
        TryEnableMica();

        Nav.SelectionChanged += Nav_SelectionChanged;
        SearchBox.TextChanged += (_, _) => _vm.SearchText = SearchBox.Text;
        SortCombo.SelectionChanged += (_, _) => _vm.SortField = SortCombo.SelectedIndex switch
        {
            1 => AppCatalogExtensions.SortField.InstallDate,
            2 => AppCatalogExtensions.SortField.Publisher,
            _ => AppCatalogExtensions.SortField.Name
        };
        SortDescToggle.Click += (_, _) => _vm.SortDescending = SortDescToggle.IsChecked == true;
        RefreshButton.Click += async (_, _) => await RunAsync(_vm.RefreshCommand.ExecuteAsync(null));
        AppList.ItemClick += async (_, e) =>
        {
            _vm.SelectedApp = e.ClickedItem as Models.InstalledApp;
            await ConfirmUninstallAsync();
        };

        UninstallButton.Click += async (_, _) => await ConfirmUninstallAsync();

        AppList.ItemsSource = _vm.ViewApps;
        _vm.PropertyChanged += async (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.SelectedApp))
            {
                AppList.SelectedItem = _vm.SelectedApp;
                UpdateDetailPane();
            }
            if (e.PropertyName == nameof(MainViewModel.StatusMessage)) StatusText.Text = _vm.StatusMessage;
            if (e.PropertyName == nameof(MainViewModel.IsBusy) || e.PropertyName == nameof(MainViewModel.IsEmpty))
            {
                LoadingRing.IsActive = _vm.IsBusy;
                EmptyText.Visibility = _vm.IsEmpty ? Visibility.Visible : Visibility.Collapsed;
            }
            if (e.PropertyName == nameof(MainViewModel.HasError))
            {
                ErrorBar.IsOpen = _vm.HasError;
                ErrorBar.Message = _vm.StatusMessage;
            }
        };

        _ = LoadThemeAsync();
        _ = LoadLanguageAsync();
        _ = RunAsync(_vm.LoadAsync(_cts.Token));
    }

    /// <summary>Loads the persisted language and applies all static UI strings.</summary>
    private async Task LoadLanguageAsync()
    {
        var lang = await _settings.GetLanguageAsync();
        LocalizationService.SelectedLanguage = lang switch
        {
            "zh-CN" => AppLanguage.SimplifiedChinese,
            "en-US" => AppLanguage.English,
            "ja-JP" => AppLanguage.Japanese,
            _ => AppLanguage.System
        };
        ApplyLocalizedStrings();
        LocalizationService.LanguageChanged += (_, _) =>
            DispatcherQueue.TryEnqueue(ApplyLocalizedStrings);
    }

    /// <summary>Pushes the current language's strings into all static controls.</summary>
    private void ApplyLocalizedStrings()
    {
        Title = LocalizationService.Get("AppTitle");
        NavItemAll.Content = LocalizationService.Get("NavAll");
        NavItemDesktop.Content = LocalizationService.Get("NavDesktop");
        NavItemStore.Content = LocalizationService.Get("NavStore");
        NavItemSettings.Content = LocalizationService.Get("NavSettings");
        SearchBox.PlaceholderText = LocalizationService.Get("SearchPlaceholder");
        SortItemName.Content = LocalizationService.Get("SortName");
        SortItemDate.Content = LocalizationService.Get("SortInstallDate");
        SortItemPublisher.Content = LocalizationService.Get("SortPublisher");
        SortDescToggle.Content = LocalizationService.Get("SortDescending");
        RefreshButton.Content = LocalizationService.Get("Refresh");
        EmptyText.Text = LocalizationService.Get("EmptyList");
        UninstallButton.Content = LocalizationService.Get("Uninstall");
        UpdateDetailPane(); // re-render detail labels with the active language
    }

    private void UpdateDetailPane()
    {
        if (_vm.SelectedApp is not { } app)
        {
            DetailPane.Visibility = Visibility.Collapsed;
            return;
        }

        DetailPane.Visibility = Visibility.Visible;
        DetailName.Text = app.DisplayName;
        DetailVersion.Text = LocalizationService.Format("DetailVersion", app.DisplayVersion ?? LocalizationService.Get("Unknown"));
        DetailPublisher.Text = LocalizationService.Format("DetailPublisher", app.Publisher ?? LocalizationService.Get("Unknown"));
        DetailInstallDate.Text = LocalizationService.Format("DetailInstallDate",
            app.InstallDate?.ToString("yyyy-MM-dd") ?? LocalizationService.Get("Unknown"));
        DetailLocation.Text = LocalizationService.Format("DetailLocation",
            string.IsNullOrWhiteSpace(app.InstallLocation) ? LocalizationService.Get("Unknown") : app.InstallLocation);

        if (app.CanUninstall)
        {
            DetailWarn.IsOpen = false;
            UninstallButton.IsEnabled = true;
        }
        else
        {
            DetailWarn.Message = app.UnavailableReason ?? LocalizationService.Get("WarnUnverifiable");
            DetailWarn.IsOpen = true;
            UninstallButton.IsEnabled = false;
        }
    }

    private async Task ConfirmUninstallAsync()
    {
        if (_vm.SelectedApp is not { } app) return;
        if (!app.CanUninstall)
        {
            ShowError(app.UnavailableReason ?? LocalizationService.Get("CannotUninstall"));
            return;
        }

        var dialog = new ContentDialog
        {
            XamlRoot = Content.XamlRoot,
            Title = LocalizationService.Format("ConfirmTitle", app.DisplayName),
            Content = LocalizationService.Format("ConfirmContent", app.DisplayName,
                app.DisplayVersion ?? LocalizationService.Get("Unknown"),
                app.Publisher ?? LocalizationService.Get("Unknown")),
            PrimaryButtonText = LocalizationService.Get("Uninstall"),
            CloseButtonText = LocalizationService.Get("Cancel"),
            DefaultButton = ContentDialogButton.Close
        };
        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;

        await RunAsync(_vm.UninstallCommand.ExecuteAsync(null));
    }

    private void Nav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem item)
        {
            if (item.Tag?.ToString() == "Settings")
            {
                _ = ShowSettingsAsync();
                // Restore selection to a category item so the pane doesn't stick on settings.
                sender.SelectedItem = sender.MenuItems[0];
                return;
            }
            _vm.SelectedCategory = item.Tag?.ToString() switch
            {
                "Desktop" => MainViewModel.CategoryFilter.Desktop,
                "Store" => MainViewModel.CategoryFilter.Store,
                _ => MainViewModel.CategoryFilter.All
            };
        }
    }

    private async Task LoadThemeAsync()
    {
        var theme = await _settings.GetThemeAsync();
        RootTheme = theme switch
        {
            "Light" => ElementTheme.Light,
            "Dark" => ElementTheme.Dark,
            _ => ElementTheme.Default
        };
    }

    public ElementTheme RootTheme
    {
        get => Content is FrameworkElement fe ? fe.RequestedTheme : ElementTheme.Default;
        set
        {
            if (Content is FrameworkElement fe)
            {
                fe.RequestedTheme = value;
                ApplyTitleBarThemeColors();
            }
        }
    }

    /// <summary>
    /// Enables the Mica system backdrop (Windows 11 22H2+) for the whole window,
    /// so the title bar and NavigationView pane pick up the desktop wallpaper tint.
    /// Falls back to Desktop Acrylic on older Win11, silently keeps the plain
    /// theme background pre-Win11.
    /// </summary>
    private void TryEnableMica()
    {
        if (Microsoft.UI.Composition.SystemBackdrops.MicaController.IsSupported())
        {
            SystemBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop
            {
                Kind = Microsoft.UI.Composition.SystemBackdrops.MicaKind.Base
            };
        }
        else if (Microsoft.UI.Composition.SystemBackdrops.DesktopAcrylicController.IsSupported())
        {
            SystemBackdrop = new Microsoft.UI.Xaml.Media.DesktopAcrylicBackdrop();
        }
        // else pre-Win11: keep default opaque background

        // The title bar row must be transparent so the backdrop shows through;
        // NavigationView content/pane are made transparent in XAML resources.
        AppTitleBar.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
    }

    /// <summary>
    /// Keeps caption buttons and app-title text in sync with the effective theme
    /// so the custom title bar matches native Windows styling in light and dark.
    /// </summary>
    private void ApplyTitleBarThemeColors()
    {
        var tb = AppWindow.TitleBar;
        var root = Content as FrameworkElement;
        var isDark = (root?.ActualTheme == ElementTheme.Dark) ||
                     (root?.ActualTheme != ElementTheme.Light &&
                      Application.Current.RequestedTheme == ApplicationTheme.Dark);

        // Matches system colors: near-black text on light, near-white on dark,
        // transparent button backgrounds exactly like the default title bar.
        var fg = isDark ? Microsoft.UI.Colors.White : Microsoft.UI.Colors.Black;
        // Subtle hover overlays matching system caption button hover (~7% / 4% alpha).
        var hoverBg = isDark
            ? Microsoft.UI.ColorHelper.FromArgb(0x12, 0xFF, 0xFF, 0xFF)
            : Microsoft.UI.ColorHelper.FromArgb(0x0A, 0x00, 0x00, 0x00);
        tb.ButtonForegroundColor = fg;
        tb.ButtonHoverBackgroundColor = hoverBg;
        tb.ButtonHoverForegroundColor = fg;
        tb.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;
        tb.ButtonInactiveBackgroundColor = Microsoft.UI.Colors.Transparent;
        tb.ButtonPressedForegroundColor = fg;
    }

    public async Task ShowSettingsAsync()
    {
        var currentTheme = await _settings.GetThemeAsync();
        var themeCombo = new ComboBox { SelectedIndex = currentTheme switch { "Light" => 1, "Dark" => 2, _ => 0 }, MinWidth = 160 };
        themeCombo.Items.Add(LocalizationService.Get("ThemeSystem"));
        themeCombo.Items.Add(LocalizationService.Get("ThemeLight"));
        themeCombo.Items.Add(LocalizationService.Get("ThemeDark"));

        var currentLang = await _settings.GetLanguageAsync();
        var langCombo = new ComboBox { SelectedIndex = currentLang switch { "zh-CN" => 1, "en-US" => 2, "ja-JP" => 3, _ => 0 }, MinWidth = 160 };
        langCombo.Items.Add(LocalizationService.Get("LanguageSystem"));
        langCombo.Items.Add(LocalizationService.Get("LanguageZh"));
        langCombo.Items.Add(LocalizationService.Get("LanguageEn"));
        langCombo.Items.Add(LocalizationService.Get("LanguageJa"));

        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(new TextBlock { Text = LocalizationService.Get("SettingsTheme") });
        panel.Children.Add(themeCombo);
        panel.Children.Add(new TextBlock { Text = LocalizationService.Get("SettingsLanguage") });
        panel.Children.Add(langCombo);
        panel.Children.Add(new TextBlock
        {
            Text = LocalizationService.Get("LanguageChangedNote"),
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
            TextWrapping = TextWrapping.Wrap
        });

        var dialog = new ContentDialog
        {
            XamlRoot = Content.XamlRoot,
            Title = LocalizationService.Get("SettingsTitle"),
            Content = panel,
            PrimaryButtonText = LocalizationService.Get("Save"),
            CloseButtonText = LocalizationService.Get("Cancel"),
            DefaultButton = ContentDialogButton.Primary
        };
        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;

        var themeChoice = themeCombo.SelectedIndex switch { 1 => "Light", 2 => "Dark", _ => "Default" };
        await _settings.SetThemeAsync(themeChoice);
        await LoadThemeAsync();

        var langChoice = langCombo.SelectedIndex switch { 1 => "zh-CN", 2 => "en-US", 3 => "ja-JP", _ => "System" };
        await _settings.SetLanguageAsync(langChoice);
        LocalizationService.SelectedLanguage = langChoice switch
        {
            "zh-CN" => AppLanguage.SimplifiedChinese,
            "en-US" => AppLanguage.English,
            "ja-JP" => AppLanguage.Japanese,
            _ => AppLanguage.System
        };
    }

    public void ShowError(string message)
    {
        ErrorBar.IsOpen = true;
        ErrorBar.Message = message;
    }

    private async Task RunAsync(Task task)
    {
        try { await task; }
        catch { /* VM reports via StatusMessage */ }
    }
}
