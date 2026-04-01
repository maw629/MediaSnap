namespace MediaSnap.ViewModels;

/// <summary>
/// Common interface for session view models (SMTC and WASAPI).
/// </summary>
public interface ISessionViewModel : IDisposable
{
    /// <summary>
    /// Unique identifier for deduplication (typically the process name or app model ID).
    /// </summary>
    string SessionId { get; }

    string AppName { get; }

    /// <summary>
    /// Whether this session supports media controls (play/pause/next/prev).
    /// </summary>
    bool HasMediaControls { get; }
}
