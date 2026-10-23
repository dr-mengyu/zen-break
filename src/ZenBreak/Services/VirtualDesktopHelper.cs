using System.Runtime.InteropServices;
using Avalonia.Controls;

namespace ZenBreak.Services;

/// <summary>
/// Win32 & COM interop helper to make overlay windows topmost, non-activating,
/// and visible across all Windows Virtual Desktops.
/// </summary>
public static class VirtualDesktopHelper
{
    private static readonly IntPtr HWND_TOPMOST = new(-1);

    private const int GWL_EXSTYLE = -20;
    private const int GWLP_HWNDPARENT = -8;

    private const int WS_EX_TOPMOST = 0x00000008;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_APPWINDOW = 0x00040000;

    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_FRAMECHANGED = 0x0020;
    private const uint SWP_SHOWWINDOW = 0x0040;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int X,
        int Y,
        int cx,
        int cy,
        uint uFlags);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("a5cd92ff-29be-454c-8d04-d82879fb3f1b")]
    private interface IVirtualDesktopManager
    {
        [PreserveSig]
        int IsWindowOnCurrentVirtualDesktop(IntPtr topLevelWindow, [MarshalAs(UnmanagedType.Bool)] out bool onCurrentDesktop);

        [PreserveSig]
        int GetWindowDesktopId(IntPtr topLevelWindow, out Guid desktopId);

        [PreserveSig]
        int MoveWindowToDesktop(IntPtr topLevelWindow, [In] ref Guid desktopId);
    }

    private static readonly Guid CLSID_VirtualDesktopManager = new("aa509086-5ca9-4c25-8f95-589d3c07b48a");
    private static IVirtualDesktopManager? _virtualDesktopManager;
    private static bool _comInitializedFailed;

    private static IVirtualDesktopManager? GetManager()
    {
        if (_comInitializedFailed) return null;
        if (_virtualDesktopManager != null) return _virtualDesktopManager;

        try
        {
            var type = Type.GetTypeFromCLSID(CLSID_VirtualDesktopManager);
            if (type != null)
            {
                _virtualDesktopManager = (IVirtualDesktopManager?)Activator.CreateInstance(type);
            }
        }
        catch
        {
            _comInitializedFailed = true;
        }

        return _virtualDesktopManager;
    }

    /// <summary>
    /// Configures a window as an unowned Win32 tool window so Windows displays it across all Virtual Desktops,
    /// and reinforces HWND_TOPMOST without stealing focus if <paramref name="noActivate"/> is true.
    /// </summary>
    public static void ConfigureFloatingOverlay(Window window, bool noActivate)
    {
        try
        {
            var handle = window.TryGetPlatformHandle()?.Handle;
            if (!handle.HasValue || handle.Value == IntPtr.Zero)
                return;

            IntPtr hwnd = handle.Value;

            // Detach Avalonia's hidden owner window if present so the window isn't anchored to the owner's virtual desktop
            SetWindowLongPtr(hwnd, GWLP_HWNDPARENT, IntPtr.Zero);

            // Apply WS_EX_TOOLWINDOW | WS_EX_TOPMOST and remove WS_EX_APPWINDOW
            long exStyle = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
            exStyle |= (WS_EX_TOOLWINDOW | WS_EX_TOPMOST);
            exStyle &= ~WS_EX_APPWINDOW;
            SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(exStyle));

            uint flags = SWP_NOMOVE | SWP_NOSIZE | SWP_FRAMECHANGED | SWP_SHOWWINDOW;
            if (noActivate)
            {
                flags |= SWP_NOACTIVATE;
            }

            SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, flags);
        }
        catch
        {
            // Ignore Win32 interop errors
        }
    }

    /// <summary>
    /// Checks if the window is currently visible on the user's active Virtual Desktop.
    /// If not, attempts to move it to the active desktop, returning false if the window needs to be re-shown.
    /// </summary>
    public static bool EnsureOnCurrentVirtualDesktop(Window window, bool noActivate)
    {
        try
        {
            var handle = window.TryGetPlatformHandle()?.Handle;
            if (!handle.HasValue || handle.Value == IntPtr.Zero)
                return true;

            IntPtr hwnd = handle.Value;
            var manager = GetManager();
            if (manager == null)
                return true;

            int hr = manager.IsWindowOnCurrentVirtualDesktop(hwnd, out bool onCurrent);
            if (hr != 0 || onCurrent)
                return true;

            // Try to move to the current foreground window's desktop first
            IntPtr fgHwnd = GetForegroundWindow();
            if (fgHwnd != IntPtr.Zero && fgHwnd != hwnd)
            {
                if (manager.GetWindowDesktopId(fgHwnd, out Guid currentDesktopId) == 0 &&
                    currentDesktopId != Guid.Empty)
                {
                    if (manager.MoveWindowToDesktop(hwnd, ref currentDesktopId) == 0)
                    {
                        ConfigureFloatingOverlay(window, noActivate);
                        if (manager.IsWindowOnCurrentVirtualDesktop(hwnd, out bool nowOnCurrent) == 0 && nowOnCurrent)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }
        catch
        {
            return true;
        }
    }
}
