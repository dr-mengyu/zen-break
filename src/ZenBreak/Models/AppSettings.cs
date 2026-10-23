namespace ZenBreak.Models;

/// <summary>
/// Application settings that are persisted to the registry.
/// </summary>
public class AppSettings
{
    /// <summary>
    /// How long before showing a break reminder (in minutes).
    /// </summary>
    public int ReminderIntervalMinutes { get; set; } = 45;
    
    /// <summary>
    /// How long to delay when user clicks Later (in minutes).
    /// </summary>
    public int SnoozeMinutes { get; set; } = 5;
    
    /// <summary>
    /// Whether the app should start automatically with Windows.
    /// </summary>
    public bool AutoStart { get; set; } = false;
    
    /// <summary>
    /// Whether to prevent Windows from sleeping or locking the display due to idle timeout.
    /// </summary>
    public bool KeepUnlocked { get; set; } = false;
    
    /// <summary>
    /// Available reminder interval options (in minutes).
    /// </summary>
    public static readonly int[] IntervalOptions = { 30, 45, 60 };
}
