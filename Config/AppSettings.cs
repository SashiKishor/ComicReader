namespace ComicReader.Config;

/// <summary>
/// Root application settings aggregating library, cache, reader, and theme configurations.
/// </summary>
public sealed class AppSettings
{
    public LibrarySettings Library { get; set; } = new();
    public CacheSettings Cache { get; set; } = new();
    public ReaderSettings Reader { get; set; } = new();
    public ThemeSettings Theme { get; set; } = new();
}

