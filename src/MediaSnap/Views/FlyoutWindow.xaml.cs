using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using MediaSnap.Helpers;

namespace MediaSnap.Views;

public partial class FlyoutWindow : Window
{
    private bool _isClosingByDeactivate;

    public FlyoutWindow()
    {
        InitializeComponent();

        Deactivated += OnDeactivated;
        PreviewKeyDown += OnPreviewKeyDown;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Hide from Alt+Tab
        var handle = new WindowInteropHelper(this).Handle;
        WindowHelper.SetToolWindow(handle);
    }

    /// <summary>
    /// Shows the flyout positioned near the system tray.
    /// </summary>
    public void ShowFlyout()
    {
        if (IsVisible)
        {
            HideFlyout();
            return;
        }

        PositionNearTray();
        Show();
        Activate();
    }

    public void HideFlyout()
    {
        Hide();
    }

    private void PositionNearTray()
    {
        var trayPosition = TrayPositionHelper.GetFlyoutPosition(ActualWidth, ActualHeight);
        Left = trayPosition.X;
        Top = trayPosition.Y;
    }

    private void OnDeactivated(object? sender, EventArgs e)
    {
        if (_isClosingByDeactivate)
        {
            return;
        }

        _isClosingByDeactivate = true;
        try
        {
            HideFlyout();
        }
        finally
        {
            _isClosingByDeactivate = false;
        }
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            HideFlyout();
            e.Handled = true;
        }
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        // Prevent actual close — just hide. App lifecycle manages shutdown.
        e.Cancel = true;
        HideFlyout();
    }
}
