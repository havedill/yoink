using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace Yoink.Services;

public static class ClipboardService
{
    private static readonly ImageCodecInfo? PngEncoder =
        ImageCodecInfo.GetImageEncoders().FirstOrDefault(e => e.MimeType == "image/png");

    /// <summary>
    /// Encodes <paramref name="bitmap"/> to PNG on a background thread and marshals the
    /// final <see cref="System.Windows.Forms.Clipboard.SetDataObject"/> call back to the
    /// WPF dispatcher (which is STA). Keeping PNG encoding off the UI thread removes the
    /// visible hitch after every screenshot on high-resolution displays.
    ///
    /// The caller owns <paramref name="bitmap"/>'s lifetime: it must remain alive until
    /// the returned task completes.
    /// </summary>
    public static Task CopyToClipboardAsync(Bitmap bitmap)
    {
        if (bitmap is null) throw new ArgumentNullException(nameof(bitmap));

        // Task.Run(Func<Task>) already returns a flattened Task — no Unwrap needed.
        return Task.Run(async () =>
        {
            // Encode PNG off the UI thread. Rewind the stream and hand it directly to
            // the DataObject — no intermediate byte[] + new MemoryStream copy.
            var pngStream = new MemoryStream(256 * 1024);
            if (PngEncoder is not null)
            {
                using var enc = new EncoderParameters(1);
                enc.Param[0] = new EncoderParameter(Encoder.Compression, 6L);
                bitmap.Save(pngStream, PngEncoder, enc);
            }
            else
            {
                bitmap.Save(pngStream, ImageFormat.Png);
            }
            pngStream.Position = 0;

            var dataObject = new System.Windows.Forms.DataObject();
            dataObject.SetData(System.Windows.Forms.DataFormats.Bitmap, bitmap);
            dataObject.SetData("PNG", false, pngStream);

            // SetDataObject must run on an STA thread; the WPF dispatcher satisfies this.
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    System.Windows.Forms.Clipboard.SetDataObject(dataObject, copy: true);
                }
                catch (Exception)
                {
                    // Clipboard may be locked by another application - retry once
                    Thread.Sleep(50);
                    try { System.Windows.Forms.Clipboard.SetDataObject(dataObject, copy: true); }
                    catch { }
                }
            });
        });
    }

    public static void CopyTextToClipboard(string text)
    {
        var dataObject = new System.Windows.Forms.DataObject();
        dataObject.SetData(System.Windows.Forms.DataFormats.UnicodeText, false, text);
        dataObject.SetData(System.Windows.Forms.DataFormats.Text, false, text);

        try
        {
            System.Windows.Forms.Clipboard.SetDataObject(dataObject, true);
        }
        catch (Exception)
        {
            Thread.Sleep(50);
            try { System.Windows.Forms.Clipboard.SetDataObject(dataObject, true); }
            catch { }
        }
    }
}
