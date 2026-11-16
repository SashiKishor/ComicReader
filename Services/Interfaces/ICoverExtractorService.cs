namespace ComicReader.Services.Interfaces;

/// <summary>
/// Service contract for extracting cover art thumbnails from comic archives and documents.
/// </summary>
public interface ICoverExtractorService
{
    Task<string?> GetOrCreateCoverAsync(string filePath, CancellationToken cancellationToken = default);
}

