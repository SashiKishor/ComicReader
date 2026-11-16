using System;

namespace ComicReader.Models;

/// <summary>
/// Data model representing the recorded reading position and total page count of a comic file.
/// Persisted in reading_progress.json for fast progress resumption and library progress bar visualization.
/// </summary>
public sealed class ReadingProgressEntry
{
    public string FilePath { get; set; } = string.Empty;
    public int LastReadPageIndex { get; set; }
    public int TotalPages { get; set; }
    public DateTime LastReadUtc { get; set; } = DateTime.UtcNow;

    public bool HasStartedReading => LastReadPageIndex > 0 || (LastReadPageIndex == 0 && TotalPages > 0);

    public double ProgressPercentage =>
        TotalPages > 0
            ? Math.Min(100.0, Math.Max(0.0, ((double)(LastReadPageIndex + 1) / TotalPages) * 100.0))
            : 0.0;
}
