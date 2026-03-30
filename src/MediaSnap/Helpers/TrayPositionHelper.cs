using System.Runtime.InteropServices;
using System.Windows;

namespace MediaSnap.Helpers;

/// <summary>
/// Detects the system tray position and calculates flyout placement.
/// </summary>
public static class TrayPositionHelper
{
    public static Point GetFlyoutPosition(double flyoutWidth, double flyoutHeight)
    {
        var workArea = SystemParameters.WorkArea;

        // Default: position at bottom-right, just above the taskbar
        var x = workArea.Right - flyoutWidth;
        var y = workArea.Bottom - flyoutHeight;

        return new Point(Math.Max(workArea.Left, x), Math.Max(workArea.Top, y));
    }
}
