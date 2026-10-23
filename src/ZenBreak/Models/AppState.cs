namespace ZenBreak.Models;

/// <summary>
/// Represents the application state machine states.
/// </summary>
public enum AppState
{
    /// <summary>
    /// User is actively working, timer is counting up.
    /// </summary>
    Working,
    
    /// <summary>
    /// Timer elapsed, reminder is being shown to user.
    /// </summary>
    Reminding,
    
    /// <summary>
    /// User delayed the reminder, waiting for delay period to elapse.
    /// </summary>
    Snoozed,
    
    /// <summary>
    /// User is on a break (either manually or from screen lock >= 1 min).
    /// </summary>
    OnBreak,
    
    /// <summary>
    /// Screen is locked, waiting to see if it's a short pause or a real break.
    /// If locked < 1 min, will resume Working/Snoozed. If >= 1 min, will become OnBreak.
    /// </summary>
    Paused
}
