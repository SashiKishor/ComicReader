using System.Threading;
using System.Threading.Tasks;
using ComicReader.Models;

namespace ComicReader.Services.Interfaces;

/// <summary>
/// Service contract for saving, debouncing, and retrieving reading progress and cached page counts per comic.
/// </summary>
public interface IProgressTrackingService
{
    /// <summary>
    /// Retrieves the last recorded 0-based page index for the given comic file.
    /// Returns 0 if no progress has been recorded.
    /// </summary>
    Task<int> GetLastReadPageAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Synchronously retrieves the progress entry from the in-memory cache.
    /// </summary>
    ReadingProgressEntry? GetProgress(string filePath);

    /// <summary>
    /// Schedules a debounced save of the comic's reading progress.
    /// </summary>
    void UpdateProgress(string filePath, int pageIndex, int totalPages);

    /// <summary>
    /// Returns the cached total page count, or extracts it asynchronously via the document service and saves it to cache.
    /// </summary>
    Task<int> GetOrExtractTotalPagesAsync(string filePath, IComicDocumentService documentService, CancellationToken cancellationToken = default);

    /// <summary>
    /// Immediately writes any pending debounced progress updates to disk.
    /// </summary>
    Task FlushAsync(CancellationToken cancellationToken = default);
}
