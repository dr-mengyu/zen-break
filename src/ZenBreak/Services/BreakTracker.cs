namespace ZenBreak.Services;

/// <summary>
/// Tracks break duration when user is away from computer.
/// </summary>
public class BreakTracker
{
    private DateTime? _breakStartTime;
    
    /// <summary>
    /// Gets the current break duration (if on break).
    /// </summary>
    public TimeSpan CurrentBreakDuration => _breakStartTime.HasValue 
        ? DateTime.Now - _breakStartTime.Value 
        : TimeSpan.Zero;
    
    /// <summary>
    /// Starts tracking a break from current time.
    /// </summary>
    public void StartBreak()
    {
        _breakStartTime = DateTime.Now;
    }
    
    /// <summary>
    /// Starts tracking a break from a specific time (for backdating screen lock).
    /// </summary>
    public void StartBreakAt(DateTime startTime)
    {
        _breakStartTime = startTime;
    }
    
    /// <summary>
    /// Ends the current break.
    /// </summary>
    public void EndBreak()
    {
        _breakStartTime = null;
    }
    
    /// <summary>
    /// Formats a duration for human-friendly display.
    /// </summary>
    public static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalHours >= 1)
        {
            return $"{(int)duration.TotalHours}h {duration.Minutes}m";
        }
        if (duration.TotalMinutes >= 1)
        {
            int mins = (int)duration.TotalMinutes;
            return mins == 1 ? "1 minute" : $"{mins} minutes";
        }
        int secs = Math.Max(0, (int)duration.TotalSeconds);
        return secs == 1 ? "1 second" : $"{secs} seconds";
    }
}
