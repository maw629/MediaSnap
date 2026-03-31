using MediaSnap.Models;
using Windows.Media.Control;

namespace MediaSnap.Services;

public sealed class MediaSessionService : IMediaSessionService
{
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private readonly object _lock = new();
    private bool _disposed;

    public event EventHandler? SessionsChanged;
    public event EventHandler? PlaybackStatusChanged;

    public async Task InitializeAsync()
    {
        if (_manager is not null)
        {
            _manager.SessionsChanged -= OnSessionsChanged;
        }

        _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        _manager.SessionsChanged += OnSessionsChanged;

        SubscribeToCurrentSessions();
    }

    public IReadOnlyList<GlobalSystemMediaTransportControlsSession> GetSessions()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_manager is null)
        {
            return [];
        }

        return _manager.GetSessions().ToList().AsReadOnly();
    }

    public PlaybackStatus GetAggregateStatus()
    {
        var sessions = GetSessions();

        if (sessions.Count == 0)
        {
            return PlaybackStatus.Idle;
        }

        var hasPlaying = false;
        var hasPaused = false;

        foreach (var session in sessions)
        {
            var status = session.GetPlaybackInfo()?.PlaybackStatus;
            switch (status)
            {
                case GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing:
                    hasPlaying = true;
                    break;
                case GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused:
                    hasPaused = true;
                    break;
            }
        }

        if (hasPlaying)
        {
            return PlaybackStatus.Playing;
        }

        if (hasPaused)
        {
            return PlaybackStatus.Paused;
        }

        return PlaybackStatus.Idle;
    }

    public async Task TogglePlaybackAsync()
    {
        var sessions = GetSessions();

        if (sessions.Count == 0)
        {
            return;
        }

        var hasPlaying = sessions.Any(s =>
            s.GetPlaybackInfo()?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing);

        if (hasPlaying)
        {
            // Pause all playing sessions
            var tasks = sessions
                .Where(s => s.GetPlaybackInfo()?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                .Select(s => s.TryPauseAsync().AsTask());
            await Task.WhenAll(tasks);
        }
        else
        {
            // Play the current/last session
            var current = _manager?.GetCurrentSession();
            if (current is not null)
            {
                await current.TryPlayAsync();
            }
        }
    }

    private void SubscribeToCurrentSessions()
    {
        if (_manager is null)
        {
            return;
        }

        foreach (var session in _manager.GetSessions())
        {
            session.PlaybackInfoChanged += OnPlaybackInfoChanged;
        }
    }

    private void UnsubscribeFromAllSessions()
    {
        if (_manager is null)
        {
            return;
        }

        foreach (var session in _manager.GetSessions())
        {
            session.PlaybackInfoChanged -= OnPlaybackInfoChanged;
        }
    }

    private void OnSessionsChanged(
        GlobalSystemMediaTransportControlsSessionManager sender,
        SessionsChangedEventArgs args)
    {
        // Re-subscribe to the new set of sessions
        UnsubscribeFromAllSessions();
        SubscribeToCurrentSessions();

        SessionsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnPlaybackInfoChanged(
        GlobalSystemMediaTransportControlsSession sender,
        PlaybackInfoChangedEventArgs args)
    {
        PlaybackStatusChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_manager is not null)
        {
            UnsubscribeFromAllSessions();
            _manager.SessionsChanged -= OnSessionsChanged;
            _manager = null;
        }
    }
}
