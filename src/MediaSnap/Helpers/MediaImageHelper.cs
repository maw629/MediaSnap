using System.IO;
using System.Windows.Media.Imaging;
using Windows.Media.Control;

namespace MediaSnap.Helpers;

public static class MediaImageHelper
{
    /// <summary>
    /// Loads album art from a media session's properties as a frozen BitmapImage.
    /// </summary>
    public static async Task<BitmapSource?> LoadThumbnailAsync(
        GlobalSystemMediaTransportControlsSessionMediaProperties properties)
    {
        if (properties.Thumbnail is null)
        {
            return null;
        }

        try
        {
            using var streamRef = await properties.Thumbnail.OpenReadAsync();
            if (streamRef is null)
            {
                return null;
            }

            using var stream = streamRef.AsStream();
            return CreateBitmapFromStream(stream);
        }
        catch (Exception ex) when (ex is FileNotFoundException or UnauthorizedAccessException)
        {
            // Some media sources provide invalid thumbnail references
            return null;
        }
    }

    /// <summary>
    /// Creates a frozen BitmapImage from a stream. Returns null on failure.
    /// </summary>
    public static BitmapSource? CreateBitmapFromStream(Stream stream)
    {
        if (stream is null || stream.Length == 0)
        {
            return null;
        }

        try
        {
            stream.Seek(0, SeekOrigin.Begin);
            var bitmap = BitmapFrame.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            bitmap.Freeze();
            return bitmap;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
