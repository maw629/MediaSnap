using Microsoft.Win32;

namespace MediaSnap.Services;

/// <summary>
/// Detects the current Windows theme and notifies when it changes.
/// </summary>
public sealed class ThemeService : IDisposable
{
    private const string PersonalizationKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string AppsUseLightThemeValue = "AppsUseLightTheme";

    private bool _disposed;

    public bool IsDarkTheme { get; private set; }

    public event EventHandler? ThemeChanged;

    public ThemeService()
    {
        IsDarkTheme = DetectDarkTheme();
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    private static bool DetectDarkTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizationKey);
            var value = key?.GetValue(AppsUseLightThemeValue);
            // 0 = dark, 1 = light; default to dark if missing
            return value is not int intVal || intVal == 0;
        }
        catch
        {
            return true; // Default to dark theme
        }
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category != UserPreferenceCategory.General)
        {
            return;
        }

        var isDark = DetectDarkTheme();
        if (isDark == IsDarkTheme)
        {
            return;
        }

        IsDarkTheme = isDark;
        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
    }
}
