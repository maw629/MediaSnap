using MediaSnap.Models;
using Windows.Media.Control;

namespace MediaSnap.Services;

/// <summary>
/// Provides access to system media sessions via SMTC (System Media Transport Controls).
/// </summary>
public interface IMediaSessionService : IDisposable
{
    /// <summary>
    /// Initializes the session manager. Must be called before accessing sessions.
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// Gets the currently active media sessions.
    /// </summary>
    IReadOnlyList<GlobalSystemMediaTransportControlsSession> GetSessions();

    /// <summary>
    /// Computes the aggregate playback status across all sessions.
    /// </summary>
    PlaybackStatus GetAggregateStatus();

    /// <summary>
    /// Toggles playback: pauses all playing sessions, or plays the last paused session.
    /// </summary>
    Task TogglePlaybackAsync();

    /// <summary>
    /// Raised when the list of active sessions changes (added or removed).
    /// </summary>
    event EventHandler? SessionsChanged;

    /// <summary>
    /// Raised when any session's playback info changes (play/pause/stop).
    /// </summary>
    event EventHandler? PlaybackStatusChanged;
}
