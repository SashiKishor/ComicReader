using ComicReader.Services.Interfaces;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ComicReader.Services;

/// <summary>
/// Manages disk-based thumbnail caching with deterministic SHA-256 keys based on file metadata.
/// </summary>
public sealed class CacheService : ICacheService
{
    private readonly IConfigurationService _configurationService;
    private readonly string _cacheDirectory;

    public CacheService(IConfigurationService configurationService)
    {
        _configurationService = configurationService;
        string resolvedPath;

        try
        {
            var configuredPath = _configurationService.Settings.Cache.GetResolvedCachePath();
            resolvedPath = Path.GetFullPath(configuredPath);
            if (!Directory.Exists(resolvedPath))
            {
                Directory.CreateDirectory(resolvedPath);
            }
        }
        catch
        {
            // Fallback to local application .cache directory if target path has restricted permissions
            resolvedPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".cache", "covers");
            if (!Directory.Exists(resolvedPath))
            {
                Directory.CreateDirectory(resolvedPath);
            }
        }

        _cacheDirectory = resolvedPath;
    }

    public string ComputeCacheKey(string filePath, DateTime lastModifiedUtc, long fileSizeBytes)
    {
        var rawKey = $"{filePath.ToUpperInvariant()}|{lastModifiedUtc.Ticks}|{fileSizeBytes}";
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawKey));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    public bool HasCachedCover(string cacheKey)
    {
        var targetFile = GetCachedCoverPath(cacheKey);
        return File.Exists(targetFile);
    }

    public string GetCachedCoverPath(string cacheKey)
    {
        return Path.Combine(_cacheDirectory, $"{cacheKey}.jpg");
    }

    public async Task<string> SaveCoverToCacheAsync(
        string cacheKey,
        byte[] imageData,
        CancellationToken cancellationToken = default)
    {
        var targetFile = GetCachedCoverPath(cacheKey);
        var tempFile = targetFile + ".tmp";

        // Write to temporary file first then atomic move to prevent partial reads
        await File.WriteAllBytesAsync(tempFile, imageData, cancellationToken);

        if (File.Exists(targetFile))
        {
            File.Delete(targetFile);
        }

        File.Move(tempFile, targetFile);
        return targetFile;
    }

    public Task ClearCacheAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            if (Directory.Exists(_cacheDirectory))
            {
                var directoryInfo = new DirectoryInfo(_cacheDirectory);
                foreach (var file in directoryInfo.EnumerateFiles())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        file.Delete();
                    }
                    catch (IOException)
                    {
                        // File may be locked by a viewer; ignore and continue
                    }
                }
            }
        }, cancellationToken);
    }
}

