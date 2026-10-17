// Program.cs — custom Main for WinUI 3 packaged app (DISABLE_XAML_GENERATED_MAIN)
using Microsoft.UI.Dispatching;
using System.Threading;

namespace EasyUninstall;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        global::WinRT.ComWrappersSupport.InitializeComWrappers();

        global::Microsoft.UI.Xaml.Application.Start(_ => CreateApp());
    }

    private static void CreateApp()
    {
        SynchronizationContext.SetSynchronizationContext(
            new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread()));
        _ = new App();
    }
}
