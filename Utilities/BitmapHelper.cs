using System.IO;
using System.Windows.Media.Imaging;

namespace ComicReader.Utilities;

/// <summary>
/// Thread-safe helper for creating frozen WPF ImageSource instances from streams or byte arrays.
/// </summary>
public static class BitmapHelper
{
    /// <summary>
    /// Safely decodes an image stream on a background thread into a frozen WPF BitmapImage.
    /// Isolates the input into a seekable MemoryStream, rewinds to position 0,
    /// sets BitmapCacheOption.OnLoad to read all bytes immediately, and calls .Freeze().
    /// </summary>
    public static BitmapImage CreateImageSourceFromStream(Stream stream)
    {
        using var memoryStream = new MemoryStream();
        if (stream.CanSeek && stream.Position != 0)
        {
            stream.Position = 0;
        }
        stream.CopyTo(memoryStream);
        memoryStream.Position = 0; // Rewind to start of stream

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad; // Crucial: Loads all bytes into memory immediately
        bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
        bitmap.StreamSource = memoryStream;
        bitmap.EndInit();
        bitmap.Freeze(); // Crucial: Makes the bitmap thread-safe for cross-thread UI consumption
        return bitmap;
    }

    /// <summary>
    /// Decodes a byte array into a frozen, thread-safe BitmapImage on any thread.
    /// </summary>
    public static BitmapImage CreateImageSourceFromBytes(byte[] bytes)
    {
        using var memoryStream = new MemoryStream(bytes);
        return CreateImageSourceFromStream(memoryStream);
    }
}
