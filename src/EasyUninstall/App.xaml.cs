using EasyUninstall.Contracts;
using EasyUninstall.Services;
using EasyUninstall.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using System.Threading;

namespace EasyUninstall;

/// <summary>Application entry: provides DI container, theme management, and global error handling.</summary>
public partial class App : Application
{
    public static Window? MainWindow { get; private set; }

    public static ServiceProvider Services { get; private set; } = default!;

    public App()
    {
        InitializeComponent();
        Services = ConfigureServices();
        UnhandledException += OnUnhandledException;
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        MainWindow = Services.GetRequiredService<MainWindow>();
        MainWindow.Activate();
    }

    private static ServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        services.AddSingleton<IUninstallCommandParser, UninstallCommandParser>();
        services.AddSingleton<IAppSource, RegistryAppSource>();
        services.AddSingleton<IAppSource, PackageAppSource>();
        services.AddSingleton<IAppCatalogService, AppCatalogService>();
        services.AddSingleton<Win32Uninstaller>();
        services.AddSingleton<PackageUninstaller>();
        services.AddSingleton<IUninstallService, UninstallService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();

        return services.BuildServiceProvider();
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        // Convert unexpected exceptions to a visible error state; never crash silently.
        if (MainWindow is MainWindow w)
        {
            w.ShowError($"发生未处理的错误：{e.Message}");
        }
        e.Handled = true;
    }
}
