using System.IO;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using MediaSnap.Helpers;
using MediaSnap.Models;
using Windows.Media.Control;

namespace MediaSnap.ViewModels;

public partial class MediaSessionViewModel : ObservableObject, IDisposable
{
    private readonly GlobalSystemMediaTransportControlsSession _session;
    private readonly Dispatcher _dispatcher;
    private bool _disposed;

    public string SessionId => _session.SourceAppUserModelId;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _artist = string.Empty;

    [ObservableProperty]
    private BitmapSource? _thumbnail;

    [ObservableProperty]
    private string _appName = string.Empty;

    [ObservableProperty]
    private BitmapSource? _appIcon;

    [ObservableProperty]
    private PlaybackStatus _status = PlaybackStatus.Idle;

    [ObservableProperty]
    private MediaControlCapabilities _capabilities = new();

    public MediaSessionViewModel(
        GlobalSystemMediaTransportControlsSession session,
        Dispatcher dispatcher)
    {
        _session = session;
        _dispatcher = dispatcher;

        _session.MediaPropertiesChanged += OnMediaPropertiesChanged;
        _session.PlaybackInfoChanged += OnPlaybackInfoChanged;

        // Initial load
        UpdatePlaybackInfo();
        _ = UpdateMediaPropertiesAsync();
    }

    private void UpdatePlaybackInfo()
    {
        var info = _session.GetPlaybackInfo();
        if (info is null) return;

        Status = info.PlaybackStatus switch
        {
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing => PlaybackStatus.Playing,
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused => PlaybackStatus.Paused,
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Stopped => PlaybackStatus.Stopped,
            _ => PlaybackStatus.Idle
        };

        var controls = info.Controls;
        Capabilities = new MediaControlCapabilities
        {
            CanPlay = controls.IsPlayEnabled,
            CanPause = controls.IsPauseEnabled,
            CanNext = controls.IsNextEnabled,
            CanPrevious = controls.IsPreviousEnabled
        };
    }

    private async Task UpdateMediaPropertiesAsync()
    {
        try
        {
            var properties = await _session.TryGetMediaPropertiesAsync();
            if (properties is null) return;

            Title = properties.Title ?? string.Empty;
            Artist = properties.Artist ?? string.Empty;

            var thumb = await MediaImageHelper.LoadThumbnailAsync(properties);
            Thumbnail = thumb;
        }
        catch (Exception ex) when (ex is FileNotFoundException or UnauthorizedAccessException)
        {
            // Some sessions have transient property access issues
        }
    }

    private void OnMediaPropertiesChanged(
        GlobalSystemMediaTransportControlsSession sender,
        MediaPropertiesChangedEventArgs args)
    {
        _dispatcher.BeginInvoke(async () => await UpdateMediaPropertiesAsync());
    }

    private void OnPlaybackInfoChanged(
        GlobalSystemMediaTransportControlsSession sender,
        PlaybackInfoChangedEventArgs args)
    {
        _dispatcher.BeginInvoke(UpdatePlaybackInfo);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _session.MediaPropertiesChanged -= OnMediaPropertiesChanged;
        _session.PlaybackInfoChanged -= OnPlaybackInfoChanged;
    }
}
