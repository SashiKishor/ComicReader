using ComicReader.Config;

namespace ComicReader.Services.Interfaces;

/// <summary>
/// Service contract for loading, retrieving, and persisting application configurations.
/// </summary>
public interface IConfigurationService
{
    AppSettings Settings { get; }
    string? GetRootDirectory();
    void SaveRootDirectory(string path);
    void SaveSettings();
    void Reload();
}
