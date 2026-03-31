using System.IO;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
    [NotifyCanExecuteChangedFor(nameof(PlayPauseCommand))]
    [NotifyCanExecuteChangedFor(nameof(PreviousCommand))]
    [NotifyCanExecuteChangedFor(nameof(NextCommand))]
    private PlaybackStatus _status = PlaybackStatus.Idle;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PlayPauseCommand))]
    [NotifyCanExecuteChangedFor(nameof(PreviousCommand))]
    [NotifyCanExecuteChangedFor(nameof(NextCommand))]
    private MediaControlCapabilities _capabilities = new();

    /// <summary>
    /// Returns the appropriate glyph for the play/pause button based on current status.
    /// </summary>
    public string PlayPauseGlyph => Status == PlaybackStatus.Playing
        ? "\uE769"  // Pause
        : "\uE768"; // Play

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

    [RelayCommand(CanExecute = nameof(CanPlayPause))]
    private async Task PlayPauseAsync()
    {
        if (Status == PlaybackStatus.Playing)
        {
            await _session.TryPauseAsync();
        }
        else
        {
            await _session.TryPlayAsync();
        }
    }

    private bool CanPlayPause() =>
        Status == PlaybackStatus.Playing ? Capabilities.CanPause : Capabilities.CanPlay;

    [RelayCommand(CanExecute = nameof(CanPrevious))]
    private async Task PreviousAsync()
    {
        await _session.TrySkipPreviousAsync();
    }

    private bool CanPrevious() => Capabilities.CanPrevious;

    [RelayCommand(CanExecute = nameof(CanNext))]
    private async Task NextAsync()
    {
        await _session.TrySkipNextAsync();
    }

    private bool CanNext() => Capabilities.CanNext;

    private void UpdatePlaybackInfo()
    {
        var info = _session.GetPlaybackInfo();
        if (info is null)
        {
            return;
        }

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

        OnPropertyChanged(nameof(PlayPauseGlyph));
    }

    private async Task UpdateMediaPropertiesAsync()
    {
        try
        {
            var properties = await _session.TryGetMediaPropertiesAsync();
            if (properties is null)
            {
                return;
            }

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
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _session.MediaPropertiesChanged -= OnMediaPropertiesChanged;
        _session.PlaybackInfoChanged -= OnPlaybackInfoChanged;
        GC.SuppressFinalize(this);
    }
}
