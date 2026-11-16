namespace ComicReader.Config;

/// <summary>
/// Configuration settings for comic viewer experience.
/// </summary>
public sealed class ReaderSettings
{
    public int PageAnimationDurationMs { get; set; } = 250;
    public string DefaultFitMode { get; set; } = "FitToScreen";
    public bool SmoothScrolling { get; set; } = true;
    public int PreloadPageCount { get; set; } = 2;
}

