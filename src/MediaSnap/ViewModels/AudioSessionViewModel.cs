using System.Diagnostics;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MediaSnap.ViewModels;

/// <summary>
/// View model for WASAPI-only audio sessions. Displays app name and icon
/// but has no media controls (play/pause/next/prev).
/// </summary>
public partial class AudioSessionViewModel : ObservableObject, ISessionViewModel
{
    public string SessionId { get; }
    public bool HasMediaControls => false;

    [ObservableProperty]
    private string _appName;

    [ObservableProperty]
    private BitmapSource? _appIcon;

    public AudioSessionViewModel(string processName, int processId)
    {
        SessionId = processName;
        _appName = FormatProcessName(processName);
        _appIcon = ExtractProcessIcon(processId);
    }

    private static string FormatProcessName(string processName)
    {
        // Capitalize first letter and add spaces before capitals for readability
        if (string.IsNullOrEmpty(processName))
        {
            return "Unknown App";
        }

        return char.ToUpperInvariant(processName[0]) + processName[1..];
    }

    private static BitmapSource? ExtractProcessIcon(int processId)
    {
        try
        {
            var process = Process.GetProcessById(processId);
            var mainModule = process.MainModule;
            if (mainModule?.FileName is null)
            {
                return null;
            }

            var icon = System.Drawing.Icon.ExtractAssociatedIcon(mainModule.FileName);
            if (icon is null)
            {
                return null;
            }

            using (icon)
            {
                var bitmap = icon.ToBitmap();
                using (bitmap)
                {
                    var bitmapSource = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                        bitmap.GetHbitmap(),
                        IntPtr.Zero,
                        System.Windows.Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
                    bitmapSource.Freeze();
                    return bitmapSource;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MediaSnap] Failed to extract icon for PID {processId}: {ex.Message}");
            return null;
        }
    }

    public void Dispose()
    {
        // No resources to dispose
        GC.SuppressFinalize(this);
    }
}
