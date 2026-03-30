using System.Runtime.InteropServices;

namespace MediaSnap.Helpers;

/// <summary>
/// Minimal Win32 interop for window management.
/// </summary>
public static partial class WindowHelper
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOOLWINDOW = 0x00000080;

    /// <summary>
    /// Makes a window a tool window (hidden from Alt+Tab).
    /// </summary>
    public static void SetToolWindow(IntPtr handle)
    {
        var style = GetWindowLong(handle, GWL_EXSTYLE);
        SetWindowLong(handle, GWL_EXSTYLE, style | WS_EX_TOOLWINDOW);
    }

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static partial int GetWindowLong(IntPtr hWnd, int nIndex);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static partial int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
}
