using System.Diagnostics;
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

        try
        {
            return _manager.GetSessions().ToList().AsReadOnly();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MediaSnap] Failed to get sessions: {ex}");
            return [];
        }
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
            try
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
            catch (Exception ex)
            {
                Debug.WriteLine($"[MediaSnap] Failed to get playback info: {ex}");
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
            // Pause all playing sessions, tolerating individual failures
            var tasks = sessions
                .Where(s => s.GetPlaybackInfo()?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                .Select(async s =>
                {
                    try
                    {
                        await s.TryPauseAsync();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[MediaSnap] Failed to pause session: {ex}");
                    }
                });
            await Task.WhenAll(tasks);
        }
        else
        {
            var current = _manager?.GetCurrentSession();
            if (current is not null)
            {
                try
                {
                    await current.TryPlayAsync();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[MediaSnap] Failed to play session: {ex}");
                }
            }
        }
    }

    private void SubscribeToCurrentSessions()
    {
        if (_manager is null)
        {
            return;
        }

        try
        {
            foreach (var session in _manager.GetSessions())
            {
                session.PlaybackInfoChanged += OnPlaybackInfoChanged;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MediaSnap] Failed to subscribe to sessions: {ex}");
        }
    }

    private void UnsubscribeFromAllSessions()
    {
        if (_manager is null)
        {
            return;
        }

        try
        {
            foreach (var session in _manager.GetSessions())
            {
                session.PlaybackInfoChanged -= OnPlaybackInfoChanged;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MediaSnap] Failed to unsubscribe from sessions: {ex}");
        }
    }

    private void OnSessionsChanged(
        GlobalSystemMediaTransportControlsSessionManager sender,
        SessionsChangedEventArgs args)
    {
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
