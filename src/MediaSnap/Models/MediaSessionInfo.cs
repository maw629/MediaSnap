using System.Windows.Media.Imaging;

namespace MediaSnap.Models;

public sealed class MediaSessionInfo
{
    public required string SessionId { get; init; }
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public BitmapSource? Thumbnail { get; set; }
    public string AppName { get; set; } = string.Empty;
    public BitmapSource? AppIcon { get; set; }
    public PlaybackStatus Status { get; set; } = PlaybackStatus.Idle;
    public MediaControlCapabilities Capabilities { get; set; } = new();
}
