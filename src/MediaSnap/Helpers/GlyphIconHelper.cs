using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PixelFormat = System.Drawing.Imaging.PixelFormat;

namespace MediaSnap.Helpers;

/// <summary>
/// Renders a font glyph into a System.Drawing.Icon, properly centered and DPI-aware.
/// </summary>
public static class GlyphIconHelper
{
    private const int IconSize = 16;

    public static Icon CreateIcon(string glyph, Typeface typeface, System.Windows.Media.Color color, uint dpi)
    {
        var dpiScaling = dpi / 96.0;
        var scaledSize = (int)Math.Ceiling(IconSize * dpiScaling);

        var visual = new DrawingVisual();
        using (var ctx = visual.RenderOpen())
        {
            var formattedText = new FormattedText(
                glyph,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                typeface,
                IconSize,
                new SolidColorBrush(color),
                dpiScaling);

            // Center the glyph within the icon canvas
            var x = (IconSize - formattedText.Width) / 2;
            var y = (IconSize - formattedText.Height) / 2;
            ctx.DrawText(formattedText, new System.Windows.Point(x, y));
        }

        var renderBitmap = new RenderTargetBitmap(scaledSize, scaledSize, dpi, dpi, PixelFormats.Pbgra32);
        renderBitmap.Render(visual);

        using var bitmap = new Bitmap(renderBitmap.PixelWidth, renderBitmap.PixelHeight, PixelFormat.Format32bppPArgb);
        var bitmapData = bitmap.LockBits(
            new Rectangle(0, 0, bitmap.Width, bitmap.Height),
            ImageLockMode.WriteOnly,
            PixelFormat.Format32bppPArgb);
        renderBitmap.CopyPixels(Int32Rect.Empty, bitmapData.Scan0, bitmapData.Height * bitmapData.Stride, bitmapData.Stride);
        bitmap.UnlockBits(bitmapData);

        return AsDisposableIcon(Icon.FromHandle(bitmap.GetHicon()));
    }

    /// <summary>
    /// Marks the icon as owning its handle so it gets properly disposed.
    /// </summary>
    private static Icon AsDisposableIcon(Icon icon)
    {
        icon.GetType()
            .GetField("ownHandle", BindingFlags.Instance | BindingFlags.NonPublic)?
            .SetValue(icon, true);
        return icon;
    }
}
