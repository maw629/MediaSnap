using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Threading;
using H.NotifyIcon;
using MediaSnap.Helpers;
using MediaSnap.Models;
using MediaSnap.Services;
using MediaSnap.ViewModels;
using MediaSnap.Views;

namespace MediaSnap;

// App owns disposable fields but cannot implement IDisposable (WPF Application lifecycle).
// Disposal is handled in OnExit.
[SuppressMessage("Reliability", "CA1001:Types that own disposable fields should be disposable")]
public partial class App
{
    private static Mutex? _mutex;
    private MediaSessionService? _mediaService;
    private ThemeService? _themeService;
    private MainViewModel? _mainViewModel;
    private FlyoutWindow? _flyoutWindow;
    private TaskbarIcon? _trayIcon;

    private static readonly System.Windows.Media.Typeface GlyphTypeface =
        new("Segoe Fluent Icons");

    private const string GlyphPause = "\uE769";
    private const string GlyphPlay = "\uE768";

    private const string DarkThemeUri = "Styles/DarkTheme.xaml";
    private const string LightThemeUri = "Styles/LightTheme.xaml";

    protected override async void OnStartup(StartupEventArgs e)
    {
        // Wire up global exception handlers before anything else
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        const string mutexName = "MediaSnap_SingleInstance_Mutex";
        _mutex = new Mutex(true, mutexName, out bool isNewInstance);

        if (!isNewInstance)
        {
            _ = MessageBox.Show(
                "MediaSnap is already running.",
                "MediaSnap",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
            Shutdown();
            return;
        }

        base.OnStartup(e);

        try
        {
            // Initialize theme service and apply system theme
            _themeService = new ThemeService();
            _themeService.ThemeChanged += OnThemeChanged;
            ApplyTheme(_themeService.IsDarkTheme);

            // Initialize media service
            _mediaService = new MediaSessionService();
            await _mediaService.InitializeAsync();

            // Create ViewModel
            _mainViewModel = new MainViewModel(_mediaService, Dispatcher);
            _mainViewModel.RefreshSessions();

            // Create flyout window
            _flyoutWindow = new FlyoutWindow { DataContext = _mainViewModel };

            // Set up tray icon
            _trayIcon = (TaskbarIcon)FindResource("TrayIcon")!;
            _trayIcon.TrayLeftMouseUp += OnTrayLeftClick;
            _trayIcon.TrayMiddleMouseUp += OnTrayMiddleClick;
            _trayIcon.ForceCreate();

            // React to session changes for tray icon visibility and glyph
            _mainViewModel.PropertyChanged += OnMainViewModelPropertyChanged;
            UpdateTrayIconState();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MediaSnap] Startup failed: {ex}");
            _ = MessageBox.Show(
                $"MediaSnap failed to start:\n{ex.Message}",
                "MediaSnap",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown();
        }
    }

    private void ApplyTheme(bool isDark)
    {
        var themeUri = isDark ? DarkThemeUri : LightThemeUri;
        var newTheme = new ResourceDictionary { Source = new Uri(themeUri, UriKind.Relative) };

        // Replace the theme dictionary (first in MergedDictionaries)
        var mergedDicts = Resources.MergedDictionaries;
        if (mergedDicts.Count > 0 && mergedDicts[0].Source?.OriginalString is DarkThemeUri or LightThemeUri)
        {
            mergedDicts[0] = newTheme;
        }
        else
        {
            mergedDicts.Insert(0, newTheme);
        }
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            try
            {
                ApplyTheme(_themeService!.IsDarkTheme);
                UpdateTrayIconState();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MediaSnap] Theme change failed: {ex}");
            }
        });
    }

    private void OnTrayLeftClick(object sender, RoutedEventArgs e) => _flyoutWindow?.ShowFlyout();

    private async void OnTrayMiddleClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_mediaService is not null)
            {
                await _mediaService.TogglePlaybackAsync();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MediaSnap] Toggle playback failed: {ex}");
        }
    }

    private void OnMainViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.HasAnySessions) or nameof(MainViewModel.AggregateStatus))
        {
            UpdateTrayIconState();
        }
    }

    private void UpdateTrayIconState()
    {
        if (_trayIcon is null || _mainViewModel is null)
        {
            return;
        }

        // Show/hide tray icon based on sessions
        _trayIcon.Visibility = _mainViewModel.HasAnySessions
            ? Visibility.Visible
            : Visibility.Collapsed;

        try
        {
            // Show current state: play icon when playing, pause icon when paused
            var glyph = _mainViewModel.AggregateStatus == PlaybackStatus.Playing
                ? GlyphPlay
                : GlyphPause;

            var iconColor = _themeService is { IsDarkTheme: true }
                ? System.Windows.Media.Colors.White
                : System.Windows.Media.Colors.Black;

            var dpi = (uint)(System.Windows.Media.VisualTreeHelper.GetDpi(
                _flyoutWindow ?? (System.Windows.Media.Visual)MainWindow!).PixelsPerInchX);

            var oldIcon = _trayIcon.Icon;
            _trayIcon.Icon = GlyphIconHelper.CreateIcon(glyph, GlyphTypeface, iconColor, dpi);
            oldIcon?.Dispose();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MediaSnap] Tray icon update failed: {ex}");
        }
    }

    #region Global Exception Handlers

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Debug.WriteLine($"[MediaSnap] Unhandled dispatcher exception: {e.Exception}");
        e.Handled = true;
    }

    private static void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        Debug.WriteLine($"[MediaSnap] Unhandled AppDomain exception: {e.ExceptionObject}");
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Debug.WriteLine($"[MediaSnap] Unobserved task exception: {e.Exception}");
        e.SetObserved();
    }

    #endregion

    protected override void OnExit(ExitEventArgs e)
    {
        _themeService?.Dispose();
        _mainViewModel?.Dispose();
        _mediaService?.Dispose();
        _trayIcon?.Dispose();

        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
