using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ZenBreak.Models;
using ZenBreak.Services;
using ZenBreak.ViewModels;

namespace ZenBreak.Views;

public partial class ReminderWindow : Window
{
    private MainViewModel? ViewModel => DataContext as MainViewModel;
    
    /// <summary>
    /// True when the window is being closed programmatically to respawn on a new Virtual Desktop.
    /// </summary>
    public bool IsSuppressingCloseAction { get; set; }
    
    public ReminderWindow()
    {
        InitializeComponent();
    }
    
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        PositionBottomRight();
        EnsureTopmostNoActivate();
    }
    
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        
        // If closed externally (e.g. Alt+F4) while still in Reminding state, treat as Skip
        if (!IsSuppressingCloseAction &&
            e.CloseReason != WindowCloseReason.ApplicationShutdown &&
            e.CloseReason != WindowCloseReason.OSShutdown &&
            ViewModel?.CurrentState == AppState.Reminding)
        {
            ViewModel.Skip();
        }
    }
    
    /// <summary>
    /// Positions the reminder window at the bottom-right corner of the primary screen's work area.
    /// </summary>
    private void PositionBottomRight()
    {
        var screen = Screens.Primary ?? Screens.All.FirstOrDefault();
        if (screen == null) return;
        
        var workArea = screen.WorkingArea;
        var scaling = DesktopScaling > 0 ? DesktopScaling : screen.Scaling;
        
        var logicalWidth = Bounds.Width > 0 ? Bounds.Width : Width;
        var logicalHeight = Bounds.Height > 0 ? Bounds.Height : Height;
        
        int pixelWidth = (int)Math.Ceiling(logicalWidth * scaling);
        int pixelHeight = (int)Math.Ceiling(logicalHeight * scaling);
        int margin = (int)Math.Ceiling(16 * scaling);
        
        int x = workArea.Right - pixelWidth - margin;
        int y = workArea.Bottom - pixelHeight - margin;
        
        Position = new PixelPoint(x, y);
    }
    
    /// <summary>
    /// Configures Win32 tool window style (visible across all virtual desktops)
    /// and reinforces HWND_TOPMOST without stealing keyboard focus.
    /// </summary>
    public void EnsureTopmostNoActivate()
    {
        VirtualDesktopHelper.ConfigureFloatingOverlay(this, noActivate: true);
    }
    
    private void OnTakeBreakClick(object? sender, RoutedEventArgs e)
    {
        ViewModel?.TakeBreak();
    }
    
    private void OnSnoozeClick(object? sender, RoutedEventArgs e)
    {
        ViewModel?.Snooze();
    }
    
    private void OnSkipClick(object? sender, RoutedEventArgs e)
    {
        ViewModel?.Skip();
    }
}
