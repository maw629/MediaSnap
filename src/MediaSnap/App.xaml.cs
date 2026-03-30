using System.Windows;

namespace MediaSnap;

public partial class App : Application
{
    private static Mutex? _mutex;

    protected override void OnStartup(StartupEventArgs e)
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
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
