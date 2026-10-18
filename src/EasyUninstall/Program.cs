// Program.cs — custom Main for WinUI 3 packaged app (DISABLE_XAML_GENERATED_MAIN).
// Also serves as the elevated force-uninstall helper entry: when launched with a
// single base64 JSON argument (from ForceUninstallService), it performs the elevated
// work and exits without touching the WinUI UI.
using EasyUninstall.Services;
using Microsoft.UI.Dispatching;
using System.Threading;

namespace EasyUninstall;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        // Elevated helper mode: "--force-helper <base64-json>"
        if (args.Length == 2 && args[0] == "--force-helper")
        {
            return ForceUninstallHelper.Run([args[1]]);
        }

        global::WinRT.ComWrappersSupport.InitializeComWrappers();

        global::Microsoft.UI.Xaml.Application.Start(_ => CreateApp());
        return 0;
    }

    private static void CreateApp()
    {
        SynchronizationContext.SetSynchronizationContext(
            new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread()));
        _ = new App();
    }
}
