using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using ZenBreak.Models;
using ZenBreak.Services;
using ZenBreak.ViewModels;
using ZenBreak.Views;

namespace ZenBreak;

public partial class App : Avalonia.Application
{
    private MainViewModel? _viewModel;
    private ReminderWindow? _reminderWindow;
    private BreakWindow? _breakWindow;
    private TrayIcon? _trayIcon;
    
    // Menu items that need dynamic updates
    private NativeMenuItem? _workingTimeItem;
    private NativeMenuItem? _statusItem;
    private NativeMenuItem? _autoStartItem;
    private NativeMenuItem? _keepUnlockedItem;
    private readonly Dictionary<int, NativeMenuItem> _intervalItems = new();
    
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Background tray application without a main window
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            
            _viewModel = new MainViewModel();
            _viewModel.ShowReminderRequested += OnShowReminderRequested;
            _viewModel.HideReminderRequested += OnHideReminderRequested;
            _viewModel.ShowBreakWindowRequested += OnShowBreakWindowRequested;
            _viewModel.HideBreakWindowRequested += OnHideBreakWindowRequested;
            _viewModel.StatusUpdated += (_, _) => UpdateMenuStatus();
            
            SetupTrayIcon(desktop);
        }

        base.OnFrameworkInitializationCompleted();
    }
    
    private void SetupTrayIcon(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var menu = new NativeMenu();
        
        // Continuous working duration item (non-clickable)
        _workingTimeItem = new NativeMenuItem { Header = "🪑 Working: 0 seconds", IsEnabled = false };
        menu.Items.Add(_workingTimeItem);
        
        // Status item (non-clickable, shows current countdown/state)
        _statusItem = new NativeMenuItem { Header = "Starting...", IsEnabled = false };
        menu.Items.Add(_statusItem);
        menu.Items.Add(new NativeMenuItemSeparator());
        
        // Interval submenu with checkmarks
        var intervalMenu = new NativeMenu();
        foreach (var interval in AppSettings.IntervalOptions)
        {
            var isSelected = _viewModel?.Settings.ReminderIntervalMinutes == interval;
            var item = new NativeMenuItem 
            { 
                Header = isSelected ? $"✓ {interval} minutes" : $"    {interval} minutes",
                Command = new RelayCommand(() => SetInterval(interval))
            };
            _intervalItems[interval] = item;
            intervalMenu.Items.Add(item);
        }
        var intervalMenuItem = new NativeMenuItem { Header = "Reminder Interval", Menu = intervalMenu };
        menu.Items.Add(intervalMenuItem);
        
        // Auto-start toggle with checkmark
        _autoStartItem = new NativeMenuItem 
        { 
            Header = _viewModel?.Settings.AutoStart == true ? "Start with Windows ✓" : "Start with Windows",
            Command = new RelayCommand(ToggleAutoStart)
        };
        menu.Items.Add(_autoStartItem);
        
        // Keep Windows Unlocked toggle with checkmark
        _keepUnlockedItem = new NativeMenuItem
        {
            Header = _viewModel?.Settings.KeepUnlocked == true ? "Keep Windows Unlocked ✓" : "Keep Windows Unlocked",
            Command = new RelayCommand(ToggleKeepUnlocked)
        };
        menu.Items.Add(_keepUnlockedItem);
        
        menu.Items.Add(new NativeMenuItemSeparator());
        
        // Take Break Now
        var breakNowItem = new NativeMenuItem 
        { 
            Header = "Take Break Now",
            Command = new RelayCommand(() => _viewModel?.TakeBreak())
        };
        menu.Items.Add(breakNowItem);
        
        menu.Items.Add(new NativeMenuItemSeparator());
        
        // Exit
        var exitItem = new NativeMenuItem 
        { 
            Header = "Exit",
            Command = new RelayCommand(() => 
            {
                _trayIcon?.Dispose();
                _viewModel?.Dispose();
                desktop.Shutdown();
            })
        };
        menu.Items.Add(exitItem);
        
        // Load tray icon from embedded resources
        WindowIcon? trayIconImage = null;
        try
        {
            var iconUri = new Uri("avares://ZenBreak/Assets/icon.png");
            using var stream = AssetLoader.Open(iconUri);
            trayIconImage = new WindowIcon(stream);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load tray icon: {ex.Message}");
        }
        
        _trayIcon = new TrayIcon
        {
            ToolTipText = "Zen Break",
            Menu = menu,
            IsVisible = true
        };
        
        if (trayIconImage != null)
        {
            try { _trayIcon.Icon = trayIconImage; }
            catch { /* Ignore icon errors */ }
        }
        
        UpdateMenuStatus();
    }
    
    private void UpdateMenuStatus()
    {
        if (_viewModel == null || _statusItem == null || _workingTimeItem == null) return;
        
        _workingTimeItem.Header = _viewModel.TrayWorkingTimeText;
        
        var state = _viewModel.CurrentState;
        var statusText = state switch
        {
            AppState.Working => $"💻 {_viewModel.RemainingTimeText}",
            AppState.Snoozed => $"⏳ {_viewModel.RemainingTimeText}",
            AppState.OnBreak => $"🥛 {_viewModel.RemainingTimeText}",
            AppState.Paused => $"⏸️ {_viewModel.RemainingTimeText}",
            AppState.Reminding => "⏰ Time for a break!",
            _ => "Zen Break"
        };
        _statusItem.Header = statusText;
        
        // Update tooltip with both continuous sitting time and next event status
        if (_trayIcon != null)
        {
            _trayIcon.ToolTipText = state == AppState.OnBreak
                ? statusText
                : $"{_viewModel.TrayWorkingTimeText}\n{statusText}";
        }
        
        // Ensure active popup windows follow the user across Virtual Desktops
        EnsureWindowsOnCurrentVirtualDesktop();
    }
    
    private void EnsureWindowsOnCurrentVirtualDesktop()
    {
        if (_reminderWindow is { IsVisible: true })
        {
            if (!VirtualDesktopHelper.EnsureOnCurrentVirtualDesktop(_reminderWindow, noActivate: true))
            {
                var oldWindow = _reminderWindow;
                oldWindow.IsSuppressingCloseAction = true;
                _reminderWindow = new ReminderWindow { DataContext = _viewModel };
                _reminderWindow.Show();
                oldWindow.Close();
            }
        }
        
        if (_breakWindow is { IsVisible: true })
        {
            if (!VirtualDesktopHelper.EnsureOnCurrentVirtualDesktop(_breakWindow, noActivate: false))
            {
                var oldWindow = _breakWindow;
                oldWindow.IsSuppressingCloseAction = true;
                _breakWindow = new BreakWindow { DataContext = _viewModel };
                _breakWindow.Show();
                oldWindow.Close();
            }
        }
    }
    
    private void SetInterval(int minutes)
    {
        _viewModel?.SetReminderInterval(minutes);
        
        foreach (var (interval, item) in _intervalItems)
        {
            item.Header = interval == minutes ? $"✓ {interval} minutes" : $"    {interval} minutes";
        }
    }
    
    private void ToggleAutoStart()
    {
        if (_viewModel != null && _autoStartItem != null)
        {
            _viewModel.SetAutoStart(!_viewModel.Settings.AutoStart);
            _autoStartItem.Header = _viewModel.Settings.AutoStart ? "Start with Windows ✓" : "Start with Windows";
        }
    }
    
    private void ToggleKeepUnlocked()
    {
        if (_viewModel != null && _keepUnlockedItem != null)
        {
            _viewModel.SetKeepUnlocked(!_viewModel.Settings.KeepUnlocked);
            _keepUnlockedItem.Header = _viewModel.Settings.KeepUnlocked ? "Keep Windows Unlocked ✓" : "Keep Windows Unlocked";
        }
    }
    
    private void OnShowReminderRequested(object? sender, EventArgs e)
    {
        if (_reminderWindow == null || !_reminderWindow.IsVisible)
        {
            _reminderWindow = new ReminderWindow
            {
                DataContext = _viewModel
            };
            // Do NOT call Activate() so we don't steal keyboard focus from the user's active work
            _reminderWindow.Show();
        }
        else
        {
            _reminderWindow.EnsureTopmostNoActivate();
        }
    }
    
    private void OnHideReminderRequested(object? sender, EventArgs e)
    {
        var window = _reminderWindow;
        _reminderWindow = null;
        window?.Close();
    }
    
    private void OnShowBreakWindowRequested(object? sender, EventArgs e)
    {
        if (_breakWindow == null || !_breakWindow.IsVisible)
        {
            _breakWindow = new BreakWindow
            {
                DataContext = _viewModel
            };
            _breakWindow.Show();
            _breakWindow.Activate();
        }
    }
    
    private void OnHideBreakWindowRequested(object? sender, EventArgs e)
    {
        var window = _breakWindow;
        _breakWindow = null;
        window?.Close();
    }
}

/// <summary>
/// Simple relay command for menu actions.
/// </summary>
public class RelayCommand : System.Windows.Input.ICommand
{
    private readonly Action _execute;
    
    public RelayCommand(Action execute) => _execute = execute;
    
    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }
    
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => _execute();
}
