using System.Diagnostics;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace MediaSnap.Services;

/// <summary>
/// Detects audio sessions via Windows Core Audio API (WASAPI).
/// Provides process-level audio presence for apps that don't register with SMTC.
/// </summary>
public sealed class AudioSessionService : IDisposable, IAudioSessionNotification
{
    private MMDeviceEnumerator? _deviceEnumerator;
    private MMDevice? _device;
    private AudioSessionManager? _sessionManager;
    private bool _disposed;

    public event EventHandler? SessionsChanged;

    public void Initialize()
    {
        try
        {
            _deviceEnumerator = new MMDeviceEnumerator();
            _device = _deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            _sessionManager = _device.AudioSessionManager;
            _sessionManager.OnSessionCreated += OnSessionCreated;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MediaSnap] WASAPI initialization failed: {ex}");
        }
    }

    /// <summary>
    /// Returns info about active audio sessions (process ID and name).
    /// Filters out system sounds (PID 0) and expired sessions.
    /// </summary>
    public IReadOnlyList<AudioSessionInfo> GetActiveSessions()
    {
        if (_sessionManager is null)
        {
            return [];
        }

        var results = new List<AudioSessionInfo>();

        try
        {
            _sessionManager.RefreshSessions();
            var sessions = _sessionManager.Sessions;

            for (var i = 0; i < sessions.Count; i++)
            {
                try
                {
                    var session = sessions[i];

                    // Skip expired or inactive sessions
                    if (session.State == AudioSessionState.AudioSessionStateExpired)
                    {
                        continue;
                    }

                    var pid = (int)session.GetProcessID;

                    // Skip system sounds (PID 0)
                    if (pid == 0)
                    {
                        continue;
                    }

                    var processName = GetProcessName(pid);
                    if (processName is null)
                    {
                        continue;
                    }

                    results.Add(new AudioSessionInfo
                    {
                        ProcessId = pid,
                        ProcessName = processName
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[MediaSnap] Error reading WASAPI session: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MediaSnap] Error enumerating WASAPI sessions: {ex}");
        }

        return results;
    }

    private static string? GetProcessName(int pid)
    {
        try
        {
            var process = Process.GetProcessById(pid);
            return process.ProcessName;
        }
        catch (ArgumentException)
        {
            // Process no longer exists
            return null;
        }
    }

    // IAudioSessionNotification implementation
    int IAudioSessionNotification.OnSessionCreated(IAudioSessionControl newSession)
    {
        SessionsChanged?.Invoke(this, EventArgs.Empty);
        return 0;
    }

    private void OnSessionCreated(object sender, IAudioSessionControl newSession)
    {
        SessionsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_sessionManager is not null)
        {
            _sessionManager.OnSessionCreated -= OnSessionCreated;
        }

        _device?.Dispose();
        _deviceEnumerator?.Dispose();
    }
}

/// <summary>
/// Basic info about an audio session detected via WASAPI.
/// </summary>
public sealed class AudioSessionInfo
{
    public required int ProcessId { get; init; }
    public required string ProcessName { get; init; }
}
