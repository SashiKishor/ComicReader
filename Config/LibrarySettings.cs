namespace ComicReader.Config;

/// <summary>
/// Configuration settings for library discovery and thumbnail generation.
/// </summary>
public sealed class LibrarySettings
{
    public string[] SupportedExtensions { get; set; } = [".cbr", ".cbz", ".pdf"];
    public string DefaultLibraryPath { get; set; } = string.Empty;
    public int MaxDegreeOfParallelism { get; set; } = 4;
    public int ThumbnailQuality { get; set; } = 85;
    public int ThumbnailMaxWidth { get; set; } = 360;
    public int ThumbnailMaxHeight { get; set; } = 540;
}

