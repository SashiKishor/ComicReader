namespace ComicReader.Services.Interfaces;

/// <summary>
/// Service contract for managing persistent local thumbnail and asset cache.
/// </summary>
public interface ICacheService
{
    string ComputeCacheKey(string filePath, DateTime lastModifiedUtc, long fileSizeBytes);
    bool HasCachedCover(string cacheKey);
    string GetCachedCoverPath(string cacheKey);
    Task<string> SaveCoverToCacheAsync(string cacheKey, byte[] imageData, CancellationToken cancellationToken = default);
    Task ClearCacheAsync(CancellationToken cancellationToken = default);
}

