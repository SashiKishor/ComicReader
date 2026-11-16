using System.IO;

namespace ComicReader.Config;

/// <summary>
/// Configuration settings for local cover cache.
/// </summary>
public sealed class CacheSettings
{
    public string CacheDirectory { get; set; } = @".cache\covers";
    public int MaxCacheSizeMegaBytes { get; set; } = 1024;
    public bool EnableDiskCache { get; set; } = true;

    /// <summary>
    /// Gets the resolved absolute path for the cache directory.
    /// Defaults to LocalApplicationData so user has guaranteed write permissions.
    /// </summary>
    public string GetResolvedCachePath()
    {
        var expanded = Environment.ExpandEnvironmentVariables(CacheDirectory);
        if (Path.IsPathRooted(expanded))
        {
            return expanded;
        }

        try
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(appData, "ComicReader", expanded.TrimStart('\\', '/'));
        }
        catch
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, expanded);
        }
    }
}

