using System.Threading;

namespace ZenBreak.Services;

/// <summary>
/// Ensures only one instance of the application runs at a time.
/// </summary>
public class SingleInstanceGuard : IDisposable
{
    private const string MutexName = "ZenBreak_SingleInstance_Mutex";
    private Mutex? _mutex;
    private bool _hasOwnership;
    
    /// <summary>
    /// Attempts to acquire single-instance lock.
    /// </summary>
    /// <returns>True if this is the only instance, false if another is already running.</returns>
    public bool TryAcquire()
    {
        _mutex = new Mutex(true, MutexName, out _hasOwnership);
        
        if (!_hasOwnership)
        {
            // Another instance is already running
            _mutex.Dispose();
            _mutex = null;
            return false;
        }
        
        return true;
    }
    
    public void Dispose()
    {
        if (_mutex != null && _hasOwnership)
        {
            _mutex.ReleaseMutex();
            _mutex.Dispose();
        }
    }
}
