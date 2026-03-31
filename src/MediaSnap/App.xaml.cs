using System.Windows;
using H.NotifyIcon;
using MediaSnap.Models;
using MediaSnap.Services;
using MediaSnap.ViewModels;
using MediaSnap.Views;

namespace MediaSnap;

public partial class App : Application
{
    private static Mutex? _mutex;
    private MediaSessionService? _mediaService;
    private MainViewModel? _mainViewModel;
    private FlyoutWindow? _flyoutWindow;
    private TaskbarIcon? _trayIcon;

    protected override async void OnStartup(StartupEventArgs e)
    {
        const string mutexName = "MediaSnap_SingleInstance_Mutex";
        _mutex = new Mutex(true, mutexName, out bool isNewInstance);

        if (!isNewInstance)
        {
            MessageBox.Show("MediaSnap is already running.", "MediaSnap", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        base.OnStartup(e);

        // Initialize media service
        _mediaService = new MediaSessionService();
        await _mediaService.InitializeAsync();

        // Create ViewModel
        _mainViewModel = new MainViewModel(_mediaService, Dispatcher);
        _mainViewModel.RefreshSessions();

        // Create flyout window
        _flyoutWindow = new FlyoutWindow { DataContext = _mainViewModel };

        // Set up tray icon
        _trayIcon = (TaskbarIcon)FindResource("TrayIcon");
        _trayIcon.TrayLeftMouseUp += OnTrayLeftClick;
        _trayIcon.ForceCreate();

        // React to session changes for tray icon visibility and glyph
        _mainViewModel.PropertyChanged += OnMainViewModelPropertyChanged;
        UpdateTrayIconState();
    }

    private void OnTrayLeftClick(object sender, RoutedEventArgs e)
    {
        _flyoutWindow?.ShowFlyout();
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
        if (_trayIcon is null || _mainViewModel is null) return;

        // Show/hide tray icon based on sessions
        _trayIcon.Visibility = _mainViewModel.HasAnySessions
            ? Visibility.Visible
            : Visibility.Collapsed;

        // Update glyph: playing = pause icon, paused = play icon
        var glyph = _mainViewModel.AggregateStatus == PlaybackStatus.Playing
            ? "\uE769"  // Pause
            : "\uE768"; // Play

        _trayIcon.IconSource = new GeneratedIconSource
        {
            Text = glyph,
            FontFamily = new System.Windows.Media.FontFamily("Segoe Fluent Icons"),
            Foreground = System.Windows.Media.Brushes.White,
            FontSize = 28
        };
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _mainViewModel?.Dispose();
        _mediaService?.Dispose();
        _trayIcon?.Dispose();

        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
