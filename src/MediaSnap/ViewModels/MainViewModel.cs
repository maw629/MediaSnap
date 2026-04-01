using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using MediaSnap.Models;
using MediaSnap.Services;

namespace MediaSnap.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly IMediaSessionService _mediaService;
    private readonly Dispatcher _dispatcher;
    private bool _disposed;

    public ObservableCollection<MediaSessionViewModel> Sessions { get; } = [];

    [ObservableProperty]
    private PlaybackStatus _aggregateStatus = PlaybackStatus.Idle;

    [ObservableProperty]
    private bool _hasAnySessions;

    public MainViewModel(IMediaSessionService mediaService, Dispatcher dispatcher)
    {
        _mediaService = mediaService;
        _dispatcher = dispatcher;

        _mediaService.SessionsChanged += OnSessionsChanged;
        _mediaService.PlaybackStatusChanged += OnPlaybackStatusChanged;
    }

    /// <summary>
    /// Syncs the ViewModel session list with the current SMTC sessions.
    /// Must be called after MediaSessionService.InitializeAsync().
    /// </summary>
    public void RefreshSessions()
    {
        var currentSessions = _mediaService.GetSessions();

        // Remove sessions that no longer exist
        for (var i = Sessions.Count - 1; i >= 0; i--)
        {
            var existing = Sessions[i];
            if (currentSessions.Any(s => s.SourceAppUserModelId == existing.SessionId))
            {
                continue;
            }

            Sessions[i].Dispose();
            Sessions.RemoveAt(i);
        }

        // Add new sessions
        foreach (var session in currentSessions)
        {
            if (Sessions.Any(vm => vm.SessionId == session.SourceAppUserModelId))
            {
                continue;
            }

            var vm = new MediaSessionViewModel(session, _dispatcher);
            Sessions.Add(vm);
        }

        UpdateAggregateStatus();
    }

    private void UpdateAggregateStatus()
    {
        AggregateStatus = _mediaService.GetAggregateStatus();
        HasAnySessions = Sessions.Count > 0;
    }

    private void OnSessionsChanged(object? sender, EventArgs e) => _dispatcher.BeginInvoke(RefreshSessions);

    private void OnPlaybackStatusChanged(object? sender, EventArgs e) => _dispatcher.BeginInvoke(UpdateAggregateStatus);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _mediaService.SessionsChanged -= OnSessionsChanged;
        _mediaService.PlaybackStatusChanged -= OnPlaybackStatusChanged;

        foreach (var session in Sessions)
        {
            session.Dispose();
        }

        Sessions.Clear();
        GC.SuppressFinalize(this);
    }
}
