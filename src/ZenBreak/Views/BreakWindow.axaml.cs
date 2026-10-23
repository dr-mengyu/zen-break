using Avalonia.Controls;
using Avalonia.Interactivity;
using ZenBreak.Models;
using ZenBreak.Services;
using ZenBreak.ViewModels;

namespace ZenBreak.Views;

public partial class BreakWindow : Window
{
    private MainViewModel? ViewModel => DataContext as MainViewModel;
    
    /// <summary>
    /// True when the window is being closed programmatically to respawn on a new Virtual Desktop.
    /// </summary>
    public bool IsSuppressingCloseAction { get; set; }
    
    public BreakWindow()
    {
        InitializeComponent();
    }
    
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        VirtualDesktopHelper.ConfigureFloatingOverlay(this, noActivate: false);
    }
    
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        
        // If closed externally (e.g. Alt+F4) while still in OnBreak state, treat as "I'm Back!"
        if (!IsSuppressingCloseAction &&
            e.CloseReason != WindowCloseReason.ApplicationShutdown &&
            e.CloseReason != WindowCloseReason.OSShutdown &&
            ViewModel?.CurrentState == AppState.OnBreak)
        {
            ViewModel.EndBreak();
        }
    }
    
    private void OnImBackClick(object? sender, RoutedEventArgs e)
    {
        ViewModel?.EndBreak();
    }
}
