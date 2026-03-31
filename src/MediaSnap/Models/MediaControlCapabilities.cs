namespace MediaSnap.Models;

public sealed class MediaControlCapabilities
{
    public bool CanPlay { get; set; }
    public bool CanPause { get; set; }
    public bool CanNext { get; set; }
    public bool CanPrevious { get; set; }
}
