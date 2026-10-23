using ZenBreak.Models;

namespace ZenBreak.Services;

/// <summary>
/// Manages the continuous work timer, interval countdown, and delayed reminder timer.
/// Driven by the unified UI tick in MainViewModel.
/// </summary>
public class TimerService
{
    private readonly SettingsService _settingsService;
    
    private DateTime _continuousWorkStartTime;
    private DateTime _intervalStartTime;
    private DateTime _snoozeEndTime;
    private DateTime? _pauseStartTime;
    
    private AppState _currentState = AppState.Working;
    private AppState _stateBeforePause = AppState.Working;
    
    /// <summary>
    /// Raised when it's time to show a reminder to the user.
    /// </summary>
    public event EventHandler? ReminderTriggered;
    
    /// <summary>
    /// Raised when the state changes.
    /// </summary>
    public event EventHandler<AppState>? StateChanged;
    
    public AppState CurrentState => _currentState;
    
    /// <summary>
    /// Gets the total continuous work time since app startup or the last completed break.
    /// Does not reset on Later (Snooze), Skip, or interval changes.
    /// </summary>
    public TimeSpan ContinuousWorkTime
    {
        get
        {
            if (_currentState == AppState.OnBreak)
                return TimeSpan.Zero;
                
            if (_currentState == AppState.Paused && _pauseStartTime.HasValue)
            {
                var frozen = _pauseStartTime.Value - _continuousWorkStartTime;
                return frozen > TimeSpan.Zero ? frozen : TimeSpan.Zero;
            }
            
            var elapsed = DateTime.Now - _continuousWorkStartTime;
            return elapsed > TimeSpan.Zero ? elapsed : TimeSpan.Zero;
        }
    }
    
    /// <summary>
    /// Gets the elapsed work time in the current reminder interval.
    /// </summary>
    public TimeSpan ElapsedIntervalTime
    {
        get
        {
            if (_currentState == AppState.Working)
            {
                var elapsed = DateTime.Now - _intervalStartTime;
                return elapsed > TimeSpan.Zero ? elapsed : TimeSpan.Zero;
            }
            if (_currentState == AppState.Paused && _pauseStartTime.HasValue)
            {
                var frozen = _pauseStartTime.Value - _intervalStartTime;
                return frozen > TimeSpan.Zero ? frozen : TimeSpan.Zero;
            }
            return TimeSpan.Zero;
        }
    }
    
    /// <summary>
    /// Gets the remaining time until the next reminder (or delayed reminder end).
    /// </summary>
    public TimeSpan RemainingTime
    {
        get
        {
            if (_currentState == AppState.Working)
            {
                var target = TimeSpan.FromMinutes(_settingsService.Settings.ReminderIntervalMinutes);
                var remaining = target - ElapsedIntervalTime;
                return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
            }
            if (_currentState == AppState.Paused && _pauseStartTime.HasValue)
            {
                if (_stateBeforePause == AppState.Snoozed)
                {
                    var snoozeRemaining = _snoozeEndTime - _pauseStartTime.Value;
                    return snoozeRemaining > TimeSpan.Zero ? snoozeRemaining : TimeSpan.Zero;
                }
                var target = TimeSpan.FromMinutes(_settingsService.Settings.ReminderIntervalMinutes);
                var remaining = target - ElapsedIntervalTime;
                return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
            }
            if (_currentState == AppState.Snoozed)
            {
                var remaining = _snoozeEndTime - DateTime.Now;
                return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
            }
            return TimeSpan.Zero;
        }
    }
    
    public TimerService(SettingsService settingsService)
    {
        _settingsService = settingsService;
        var now = DateTime.Now;
        _continuousWorkStartTime = now;
        _intervalStartTime = now;
    }
    
    /// <summary>
    /// Called every second by the unified timer to evaluate state transitions.
    /// </summary>
    public void Tick()
    {
        switch (_currentState)
        {
            case AppState.Working:
                CheckWorkTimer();
                break;
            case AppState.Snoozed:
                CheckSnoozeTimer();
                break;
        }
    }
    
    private void CheckWorkTimer()
    {
        var elapsed = DateTime.Now - _intervalStartTime;
        var threshold = TimeSpan.FromMinutes(_settingsService.Settings.ReminderIntervalMinutes);
        
        if (elapsed >= threshold)
        {
            SetState(AppState.Reminding);
            ReminderTriggered?.Invoke(this, EventArgs.Empty);
        }
    }
    
    private void CheckSnoozeTimer()
    {
        if (DateTime.Now >= _snoozeEndTime)
        {
            SetState(AppState.Reminding);
            ReminderTriggered?.Invoke(this, EventArgs.Empty);
        }
    }
    
    /// <summary>
    /// User dismissed/skipped the reminder (or changed interval).
    /// Resets the interval timer while preserving continuous sitting time.
    /// </summary>
    public void Dismiss()
    {
        _intervalStartTime = DateTime.Now;
        _pauseStartTime = null;
        SetState(AppState.Working);
    }
    
    /// <summary>
    /// User chose "Later". Remind again after delay period while preserving continuous sitting time.
    /// </summary>
    public void Snooze()
    {
        _snoozeEndTime = DateTime.Now.AddMinutes(_settingsService.Settings.SnoozeMinutes);
        _pauseStartTime = null;
        SetState(AppState.Snoozed);
    }
    
    /// <summary>
    /// Pause the current timer (screen locked or system suspended).
    /// Freezes continuous work time, interval time, and delayed reminder time.
    /// </summary>
    public void Pause()
    {
        if (_currentState == AppState.OnBreak || _currentState == AppState.Paused)
            return;
            
        _stateBeforePause = _currentState;
        _pauseStartTime = DateTime.Now;
        SetState(AppState.Paused);
    }
    
    /// <summary>
    /// Resume the timer after a short pause (< 1 min).
    /// Shifts start/end timestamps forward by the pause duration and restores previous state.
    /// </summary>
    public void Resume()
    {
        if (_currentState != AppState.Paused)
            return;
            
        if (_pauseStartTime.HasValue)
        {
            var pauseDuration = DateTime.Now - _pauseStartTime.Value;
            if (pauseDuration > TimeSpan.Zero)
            {
                _continuousWorkStartTime = _continuousWorkStartTime.Add(pauseDuration);
                _intervalStartTime = _intervalStartTime.Add(pauseDuration);
                _snoozeEndTime = _snoozeEndTime.Add(pauseDuration);
            }
            _pauseStartTime = null;
        }
        
        SetState(_stateBeforePause);
    }
    
    /// <summary>
    /// User is taking a break (manually or screen locked/asleep >= 1 min).
    /// </summary>
    public void StartBreak()
    {
        _pauseStartTime = null;
        SetState(AppState.OnBreak);
    }
    
    /// <summary>
    /// User returned from break (clicked "I'm Back").
    /// Resets BOTH the continuous sitting timer and the interval timer.
    /// </summary>
    public void EndBreak()
    {
        var now = DateTime.Now;
        _continuousWorkStartTime = now;
        _intervalStartTime = now;
        _pauseStartTime = null;
        SetState(AppState.Working);
    }
    
    private void SetState(AppState newState)
    {
        if (_currentState != newState)
        {
            _currentState = newState;
            StateChanged?.Invoke(this, newState);
        }
    }
}
