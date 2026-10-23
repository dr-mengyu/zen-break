using Microsoft.Win32;

namespace ZenBreak.Services;

/// <summary>
/// Monitors Windows session events (lock/unlock) and power events (sleep/hibernate/resume).
/// </summary>
public class SessionMonitor : IDisposable
{
    /// <summary>
    /// Raised when the user locks their screen or the system enters sleep/hibernate.
    /// </summary>
    public event EventHandler? SessionLocked;
    
    /// <summary>
    /// Raised when the user unlocks their screen and the system is awake.
    /// </summary>
    public event EventHandler? SessionUnlocked;
    
    private bool _isSessionLocked;
    private bool _isSuspended;
    private bool _wasAway;
    
    public SessionMonitor()
    {
        SystemEvents.SessionSwitch += OnSessionSwitch;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
    }
    
    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        switch (e.Reason)
        {
            case SessionSwitchReason.SessionLock:
                _isSessionLocked = true;
                UpdateAwayState();
                break;
                
            case SessionSwitchReason.SessionUnlock:
                _isSessionLocked = false;
                UpdateAwayState();
                break;
        }
    }
    
    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        switch (e.Mode)
        {
            case PowerModes.Suspend:
                _isSuspended = true;
                UpdateAwayState();
                break;
                
            case PowerModes.Resume:
                _isSuspended = false;
                UpdateAwayState();
                break;
        }
    }
    
    private void UpdateAwayState()
    {
        bool isAwayNow = _isSessionLocked || _isSuspended;
        if (isAwayNow == _wasAway)
            return;
            
        _wasAway = isAwayNow;
        if (isAwayNow)
        {
            SessionLocked?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            SessionUnlocked?.Invoke(this, EventArgs.Empty);
        }
    }
    
    public void Dispose()
    {
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
    }
}
