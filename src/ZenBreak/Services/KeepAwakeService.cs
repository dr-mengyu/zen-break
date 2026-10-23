using System.Runtime.InteropServices;

namespace ZenBreak.Services;

/// <summary>
/// Prevents Windows from turning off the display or sleeping/locking due to idle timeout.
/// </summary>
public class KeepAwakeService : IDisposable
{
    [Flags]
    private enum EXECUTION_STATE : uint
    {
        ES_SYSTEM_REQUIRED = 0x00000001,
        ES_DISPLAY_REQUIRED = 0x00000002,
        ES_CONTINUOUS = 0x80000000
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern EXECUTION_STATE SetThreadExecutionState(EXECUTION_STATE esFlags);

    private bool _isEnabled;

    /// <summary>
    /// Enables or disables keeping Windows awake and unlocked.
    /// </summary>
    public void SetEnabled(bool enabled)
    {
        _isEnabled = enabled;
        ApplyExecutionState();
    }

    /// <summary>
    /// Reinforces the current execution state if enabled.
    /// </summary>
    public void Refresh()
    {
        if (_isEnabled)
        {
            ApplyExecutionState();
        }
    }

    private void ApplyExecutionState()
    {
        try
        {
            if (_isEnabled)
            {
                SetThreadExecutionState(
                    EXECUTION_STATE.ES_CONTINUOUS |
                    EXECUTION_STATE.ES_SYSTEM_REQUIRED |
                    EXECUTION_STATE.ES_DISPLAY_REQUIRED);
            }
            else
            {
                SetThreadExecutionState(EXECUTION_STATE.ES_CONTINUOUS);
            }
        }
        catch
        {
            // Ignore P/Invoke errors
        }
    }

    public void Dispose()
    {
        if (_isEnabled)
        {
            _isEnabled = false;
            ApplyExecutionState();
        }
    }
}
