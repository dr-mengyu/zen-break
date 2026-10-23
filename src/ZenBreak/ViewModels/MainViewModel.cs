using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Threading;
using ZenBreak.Models;
using ZenBreak.Services;

namespace ZenBreak.ViewModels;

/// <summary>
/// Main ViewModel that orchestrates the application logic and exposes state to the UI.
/// </summary>
public class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly SettingsService _settingsService;
    private readonly TimerService _timerService;
    private readonly BreakTracker _breakTracker;
    private readonly SessionMonitor _sessionMonitor;
    private readonly KeepAwakeService _keepAwakeService;
    private readonly DispatcherTimer _uiTimer;
    
    private string _remainingTimeText = "";
    private string _workingDurationText = "";
    private string _trayWorkingTimeText = "";
    private string _breakTimeText = "";
    private DateTime? _lockStartTime = null;
    private DispatcherTimer? _lockThresholdTimer = null;
    private const int LockThresholdMinutes = 1;
    
    public event PropertyChangedEventHandler? PropertyChanged;
    
    /// <summary>
    /// Raised on every unified timer tick or state change so the tray menu can update.
    /// </summary>
    public event EventHandler? StatusUpdated;
    
    /// <summary>
    /// Raised when a reminder should be shown to the user.
    /// </summary>
    public event EventHandler? ShowReminderRequested;
    
    /// <summary>
    /// Raised when the reminder window should be hidden.
    /// </summary>
    public event EventHandler? HideReminderRequested;
    
    /// <summary>
    /// Raised when the break window should be shown.
    /// </summary>
    public event EventHandler? ShowBreakWindowRequested;
    
    /// <summary>
    /// Raised when the break window should be hidden.
    /// </summary>
    public event EventHandler? HideBreakWindowRequested;
    
    public string RemainingTimeText
    {
        get => _remainingTimeText;
        set => SetField(ref _remainingTimeText, value);
    }
    
    /// <summary>
    /// Continuous sitting/working duration text displayed on the ReminderWindow.
    /// </summary>
    public string WorkingDurationText
    {
        get => _workingDurationText;
        set => SetField(ref _workingDurationText, value);
    }
    
    /// <summary>
    /// Continuous sitting/working duration text displayed in the System Tray menu.
    /// </summary>
    public string TrayWorkingTimeText
    {
        get => _trayWorkingTimeText;
        set => SetField(ref _trayWorkingTimeText, value);
    }
    
    /// <summary>
    /// Text shown on the BreakWindow - always shows current break duration.
    /// </summary>
    public string BreakTimeText
    {
        get => _breakTimeText;
        set => SetField(ref _breakTimeText, value);
    }
    
    public AppSettings Settings => _settingsService.Settings;
    public AppState CurrentState => _timerService.CurrentState;
    
    public MainViewModel()
    {
        _settingsService = new SettingsService();
        _timerService = new TimerService(_settingsService);
        _breakTracker = new BreakTracker();
        _sessionMonitor = new SessionMonitor();
        _keepAwakeService = new KeepAwakeService();
        
        // Apply initial KeepUnlocked state
        _keepAwakeService.SetEnabled(_settingsService.Settings.KeepUnlocked);
        
        // Wire up events
        _timerService.ReminderTriggered += OnReminderTriggered;
        _timerService.StateChanged += OnStateChanged;
        _sessionMonitor.SessionLocked += OnSessionLocked;
        _sessionMonitor.SessionUnlocked += OnSessionUnlocked;
        
        // Single unified 1-second timer driving both TimerService and UI updates
        _uiTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _uiTimer.Tick += OnUiTimerTick;
        _uiTimer.Start();
        
        RefreshStateDisplays();
    }
    
    private void OnReminderTriggered(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            RefreshStateDisplays();
            ShowReminderRequested?.Invoke(this, EventArgs.Empty);
        });
    }
    
    private void OnStateChanged(object? sender, AppState state)
    {
        Dispatcher.UIThread.Post(RefreshStateDisplays);
    }
    
    private void OnSessionLocked(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var currentState = _timerService.CurrentState;
            
            // If already on break or paused, lock changes nothing
            if (currentState == AppState.OnBreak || currentState == AppState.Paused)
                return;
            
            _lockStartTime = DateTime.Now;
            
            // If reminder is showing, treat lock as "Take Break" - immediately go to break
            if (currentState == AppState.Reminding)
            {
                TransitionToBreakFromLock();
                return;
            }
            
            // For Working/Snoozed states: pause and start threshold timer
            _timerService.Pause();
            HideReminderRequested?.Invoke(this, EventArgs.Empty);
            
            _lockThresholdTimer?.Stop();
            _lockThresholdTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(LockThresholdMinutes) };
            _lockThresholdTimer.Tick += OnLockThresholdReached;
            _lockThresholdTimer.Start();
            
            RefreshStateDisplays();
        });
    }
    
    /// <summary>
    /// Called when lock duration exceeds threshold - transition to break.
    /// </summary>
    private void OnLockThresholdReached(object? sender, EventArgs e)
    {
        _lockThresholdTimer?.Stop();
        _lockThresholdTimer = null;
        
        if (_timerService.CurrentState == AppState.Paused)
        {
            TransitionToBreakFromLock();
        }
    }
    
    private void TransitionToBreakFromLock()
    {
        _timerService.StartBreak();
        
        if (_lockStartTime.HasValue)
            _breakTracker.StartBreakAt(_lockStartTime.Value);
        else
            _breakTracker.StartBreak();
            
        HideReminderRequested?.Invoke(this, EventArgs.Empty);
        ShowBreakWindowRequested?.Invoke(this, EventArgs.Empty);
        RefreshStateDisplays();
    }
    
    private void OnSessionUnlocked(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            _lockThresholdTimer?.Stop();
            _lockThresholdTimer = null;
            
            var currentState = _timerService.CurrentState;
            
            if (currentState == AppState.Paused)
            {
                // If system was asleep/locked >= 1 min (e.g. timers didn't tick during S3/S4 sleep),
                // transition to OnBreak now instead of resuming
                if (_lockStartTime.HasValue &&
                    (DateTime.Now - _lockStartTime.Value) >= TimeSpan.FromMinutes(LockThresholdMinutes))
                {
                    TransitionToBreakFromLock();
                    return;
                }
                
                // Lock was < 1 min: resume previous state (Working or Snoozed)
                _timerService.Resume();
                _lockStartTime = null;
                RefreshStateDisplays();
                return;
            }
            
            // If on break - do nothing, wait for "I'm Back" button
            RefreshStateDisplays();
        });
    }
    
    private void OnUiTimerTick(object? sender, EventArgs e)
    {
        _timerService.Tick();
        _keepAwakeService.Refresh();
        RefreshStateDisplays();
    }
    
    private void RefreshStateDisplays()
    {
        var remaining = _timerService.RemainingTime;
        var continuousWork = _timerService.ContinuousWorkTime;
        var formattedSitting = BreakTracker.FormatDuration(continuousWork);
        
        WorkingDurationText = $"🪑 Working continuously for {formattedSitting}";
        TrayWorkingTimeText = _timerService.CurrentState == AppState.OnBreak
            ? "🪑 Working: Reset on break"
            : $"🪑 Working: {formattedSitting}";
        
        RemainingTimeText = _timerService.CurrentState switch
        {
            AppState.Working => $"Next reminder in {(int)remaining.TotalMinutes}:{remaining.Seconds:D2}",
            AppState.Snoozed => $"Remind in {(int)remaining.TotalMinutes}:{remaining.Seconds:D2}",
            AppState.OnBreak => $"On Break ({BreakTracker.FormatDuration(_breakTracker.CurrentBreakDuration)})",
            AppState.Paused => $"Paused at {(int)remaining.TotalMinutes}:{remaining.Seconds:D2}",
            AppState.Reminding => "Time for a break!",
            _ => ""
        };
        
        BreakTimeText = $"Break: {BreakTracker.FormatDuration(_breakTracker.CurrentBreakDuration)}";
        StatusUpdated?.Invoke(this, EventArgs.Empty);
    }
    
    /// <summary>
    /// User clicked "Take Break" - dismiss reminder and start break mode.
    /// </summary>
    public void TakeBreak()
    {
        _timerService.StartBreak();
        _breakTracker.StartBreak();
        HideReminderRequested?.Invoke(this, EventArgs.Empty);
        ShowBreakWindowRequested?.Invoke(this, EventArgs.Empty);
        RefreshStateDisplays();
    }
    
    /// <summary>
    /// User clicked "Later (5m)" - delay reminder. Continuous work time keeps counting.
    /// </summary>
    public void Snooze()
    {
        _timerService.Snooze();
        HideReminderRequested?.Invoke(this, EventArgs.Empty);
        RefreshStateDisplays();
    }
    
    /// <summary>
    /// User clicked "Skip" (or closed reminder window) - dismiss and reset interval timer only.
    /// </summary>
    public void Skip()
    {
        _timerService.Dismiss();
        HideReminderRequested?.Invoke(this, EventArgs.Empty);
        RefreshStateDisplays();
    }
    
    /// <summary>
    /// Ends break manually (user clicked "I'm Back" or closed break window).
    /// Resets both continuous work timer and interval timer.
    /// </summary>
    public void EndBreak()
    {
        _breakTracker.EndBreak();
        _timerService.EndBreak();
        _lockStartTime = null;
        HideBreakWindowRequested?.Invoke(this, EventArgs.Empty);
        RefreshStateDisplays();
    }
    
    /// <summary>
    /// Sets a new reminder interval. Resets interval countdown while keeping continuous work time.
    /// </summary>
    public void SetReminderInterval(int minutes)
    {
        _settingsService.SetReminderInterval(minutes);
        _timerService.Dismiss();
        OnPropertyChanged(nameof(Settings));
        RefreshStateDisplays();
    }
    
    /// <summary>
    /// Sets auto-start with Windows.
    /// </summary>
    public void SetAutoStart(bool enabled)
    {
        _settingsService.SetAutoStart(enabled);
        OnPropertyChanged(nameof(Settings));
    }
    
    /// <summary>
    /// Sets whether Windows should be kept unlocked/awake.
    /// </summary>
    public void SetKeepUnlocked(bool enabled)
    {
        _settingsService.SetKeepUnlocked(enabled);
        _keepAwakeService.SetEnabled(enabled);
        OnPropertyChanged(nameof(Settings));
    }
    
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
    
    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
    
    public void Dispose()
    {
        _uiTimer.Stop();
        _lockThresholdTimer?.Stop();
        _keepAwakeService.Dispose();
        _sessionMonitor.Dispose();
    }
}
