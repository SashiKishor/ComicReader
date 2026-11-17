using ComicReader.Models;
using System.Text.Json.Serialization;

namespace ComicReader.Config;

/// <summary>
/// Configuration settings for comic viewer experience.
/// </summary>
public sealed class ReaderSettings
{
    public int PageAnimationDurationMs { get; set; } = 250;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PageFitMode DefaultFitMode { get; set; } = PageFitMode.FitToScreen;

    public double DefaultZoomFactor { get; set; } = 1.0;

    public bool SmoothScrolling { get; set; } = true;
    public int PreloadPageCount { get; set; } = 2;
}
