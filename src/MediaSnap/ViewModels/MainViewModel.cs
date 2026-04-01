using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using MediaSnap.Models;
using MediaSnap.Services;

namespace MediaSnap.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly IMediaSessionService _mediaService;
    private readonly AudioSessionService? _audioSessionService;
    private readonly Dispatcher _dispatcher;
    private bool _disposed;

    public ObservableCollection<ISessionViewModel> Sessions { get; } = [];

    [ObservableProperty]
    private PlaybackStatus _aggregateStatus = PlaybackStatus.Idle;

    [ObservableProperty]
    private bool _hasAnySessions;

    public MainViewModel(
        IMediaSessionService mediaService,
        Dispatcher dispatcher,
        AudioSessionService? audioSessionService = null)
    {
        _mediaService = mediaService;
        _dispatcher = dispatcher;
        _audioSessionService = audioSessionService;

        _mediaService.SessionsChanged += OnSessionsChanged;
        _mediaService.PlaybackStatusChanged += OnPlaybackStatusChanged;

        if (_audioSessionService is not null)
        {
            _audioSessionService.SessionsChanged += OnAudioSessionsChanged;
        }
    }

    /// <summary>
    /// Syncs the ViewModel session list with current SMTC and WASAPI sessions.
    /// </summary>
    public void RefreshSessions()
    {
        RefreshSmtcSessions();
        RefreshAudioSessions();
        UpdateAggregateStatus();
    }

    private void RefreshSmtcSessions()
    {
        var currentSessions = _mediaService.GetSessions();

        // Remove SMTC sessions that no longer exist
        for (var i = Sessions.Count - 1; i >= 0; i--)
        {
            if (Sessions[i] is not MediaSessionViewModel existing)
            {
                continue;
            }

            if (currentSessions.Any(s => s.SourceAppUserModelId == existing.SessionId))
            {
                continue;
            }

            Sessions[i].Dispose();
            Sessions.RemoveAt(i);
        }

        // Add new SMTC sessions
        foreach (var session in currentSessions)
        {
            if (Sessions.OfType<MediaSessionViewModel>().Any(vm => vm.SessionId == session.SourceAppUserModelId))
            {
                continue;
            }

            var vm = new MediaSessionViewModel(session, _dispatcher);
            Sessions.Add(vm);
        }
    }

    private void RefreshAudioSessions()
    {
        if (_audioSessionService is null)
        {
            return;
        }

        try
        {
            var audioSessions = _audioSessionService.GetActiveSessions();
            var smtcSessionIds = Sessions.OfType<MediaSessionViewModel>()
                .Select(s => s.SessionId.ToLowerInvariant())
                .ToHashSet();

            // Remove WASAPI sessions that no longer exist or now have SMTC coverage
            for (var i = Sessions.Count - 1; i >= 0; i--)
            {
                if (Sessions[i] is not AudioSessionViewModel existing)
                {
                    continue;
                }

                var stillActive = audioSessions.Any(a =>
                    string.Equals(a.ProcessName, existing.SessionId, StringComparison.OrdinalIgnoreCase));
                var nowCoveredBySmtc = smtcSessionIds.Any(id =>
                    id.Contains(existing.SessionId, StringComparison.OrdinalIgnoreCase));

                if (stillActive && !nowCoveredBySmtc)
                {
                    continue;
                }

                Sessions[i].Dispose();
                Sessions.RemoveAt(i);
            }

            // Add new WASAPI-only sessions (not already covered by SMTC)
            foreach (var audioSession in audioSessions)
            {
                // Skip if already shown as WASAPI session
                if (Sessions.OfType<AudioSessionViewModel>().Any(vm =>
                    string.Equals(vm.SessionId, audioSession.ProcessName, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                // Skip if covered by an SMTC session
                if (smtcSessionIds.Any(id =>
                    id.Contains(audioSession.ProcessName, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                var vm = new AudioSessionViewModel(audioSession.ProcessName, audioSession.ProcessId);
                Sessions.Add(vm);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MediaSnap] Error refreshing audio sessions: {ex}");
        }
    }

    private void UpdateAggregateStatus()
    {
        AggregateStatus = _mediaService.GetAggregateStatus();
        HasAnySessions = Sessions.Count > 0;
    }

    private void OnSessionsChanged(object? sender, EventArgs e) => _dispatcher.BeginInvoke(RefreshSessions);

    private void OnPlaybackStatusChanged(object? sender, EventArgs e) => _dispatcher.BeginInvoke(UpdateAggregateStatus);

    private void OnAudioSessionsChanged(object? sender, EventArgs e) => _dispatcher.BeginInvoke(RefreshSessions);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _mediaService.SessionsChanged -= OnSessionsChanged;
        _mediaService.PlaybackStatusChanged -= OnPlaybackStatusChanged;

        if (_audioSessionService is not null)
        {
            _audioSessionService.SessionsChanged -= OnAudioSessionsChanged;
        }

        foreach (var session in Sessions)
        {
            session.Dispose();
        }

        Sessions.Clear();
        GC.SuppressFinalize(this);
    }
}
