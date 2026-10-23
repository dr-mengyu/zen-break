using Microsoft.Win32;
using ZenBreak.Models;

namespace ZenBreak.Services;

/// <summary>
/// Manages application settings persistence using Windows Registry.
/// </summary>
public class SettingsService
{
    private const string RegistryKeyPath = @"Software\ZenBreak";
    private const string IntervalValueName = "ReminderInterval";
    private const string SnoozeValueName = "SnoozeMinutes";
    private const string AutoStartValueName = "AutoStart";
    private const string KeepUnlockedValueName = "KeepUnlocked";
    
    private const string StartupKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "ZenBreak";
    
    private readonly string _executablePath;
    
    public AppSettings Settings { get; private set; }
    
    public SettingsService()
    {
        _executablePath = Environment.ProcessPath ?? "";
        Settings = Load();
    }
    
    /// <summary>
    /// Loads settings from the registry. Returns defaults if not found.
    /// </summary>
    public AppSettings Load()
    {
        var settings = new AppSettings();
        bool hasAutoStartSetting = false;
        
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath);
            if (key != null)
            {
                if (key.GetValue(IntervalValueName) is int interval)
                    settings.ReminderIntervalMinutes = interval;
                    
                if (key.GetValue(SnoozeValueName) is int snooze)
                    settings.SnoozeMinutes = snooze;
                    
                if (key.GetValue(AutoStartValueName) is int autoStart)
                {
                    settings.AutoStart = autoStart == 1;
                    hasAutoStartSetting = true;
                }
                
                if (key.GetValue(KeepUnlockedValueName) is int keepUnlocked)
                    settings.KeepUnlocked = keepUnlocked == 1;
            }
            
            // Check actual Windows Run registry key to avoid desync on first run or moved executable
            using var runKey = Registry.CurrentUser.OpenSubKey(StartupKeyPath, writable: false);
            var existingRunValue = runKey?.GetValue(AppName) as string;
            if (!hasAutoStartSetting && !string.IsNullOrEmpty(existingRunValue))
            {
                settings.AutoStart = true;
            }
        }
        catch
        {
            // Ignore registry errors, use defaults
        }
        
        Settings = settings;
        
        // Ensure Windows Run key is in sync with loaded AutoStart state & current executable path
        UpdateAutoStart();
        
        return settings;
    }
    
    /// <summary>
    /// Saves current settings to the registry.
    /// </summary>
    public void Save()
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RegistryKeyPath);
            key.SetValue(IntervalValueName, Settings.ReminderIntervalMinutes, RegistryValueKind.DWord);
            key.SetValue(SnoozeValueName, Settings.SnoozeMinutes, RegistryValueKind.DWord);
            key.SetValue(AutoStartValueName, Settings.AutoStart ? 1 : 0, RegistryValueKind.DWord);
            key.SetValue(KeepUnlockedValueName, Settings.KeepUnlocked ? 1 : 0, RegistryValueKind.DWord);
            
            // Update Windows startup registry
            UpdateAutoStart();
        }
        catch
        {
            // Ignore registry errors
        }
    }
    
    /// <summary>
    /// Updates the Windows startup registry entry based on AutoStart setting.
    /// </summary>
    private void UpdateAutoStart()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(StartupKeyPath, writable: true);
            if (key == null) return;
            
            if (Settings.AutoStart && !string.IsNullOrEmpty(_executablePath))
            {
                key.SetValue(AppName, $"\"{_executablePath}\"");
            }
            else
            {
                key.DeleteValue(AppName, throwOnMissingValue: false);
            }
        }
        catch
        {
            // Ignore registry errors
        }
    }
    
    /// <summary>
    /// Updates the reminder interval and saves to registry.
    /// </summary>
    public void SetReminderInterval(int minutes)
    {
        Settings.ReminderIntervalMinutes = minutes;
        Save();
    }
    
    /// <summary>
    /// Toggles auto-start setting and saves to registry.
    /// </summary>
    public void SetAutoStart(bool enabled)
    {
        Settings.AutoStart = enabled;
        Save();
    }
    
    /// <summary>
    /// Toggles keep-unlocked setting and saves to registry.
    /// </summary>
    public void SetKeepUnlocked(bool enabled)
    {
        Settings.KeepUnlocked = enabled;
        Save();
    }
}
