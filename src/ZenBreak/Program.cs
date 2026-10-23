using Avalonia;
using ZenBreak.Services;
using System;
using System.Windows.Forms;

namespace ZenBreak;

class Program
{
    private static SingleInstanceGuard? _singleInstanceGuard;
    
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // Ensure only one instance runs
        _singleInstanceGuard = new SingleInstanceGuard();
        if (!_singleInstanceGuard.TryAcquire())
        {
            // Another instance is already running
            MessageBox.Show(
                "Zen Break is already running.\nCheck your system tray.",
                "Zen Break",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }
        
        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            _singleInstanceGuard.Dispose();
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
