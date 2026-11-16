using System.Windows.Media.Imaging;

namespace ComicReader.Models;

/// <summary>
/// Domain model representing a single readable page within a comic or document.
/// </summary>
public sealed class ComicPage
{
    public int PageIndex { get; set; }
    public int PageNumber => PageIndex + 1;
    public string? EntryName { get; set; }
    public BitmapSource? PageImage { get; set; }
    public byte[]? ImageData { get; set; }
    public int PixelWidth { get; set; }
    public int PixelHeight { get; set; }
}
