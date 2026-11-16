using ComicReader.Config;
using ComicReader.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using System.IO;
using System.Text.Json;

namespace ComicReader.Services;

/// <summary>
/// Service responsible for loading, binding, and persisting application configuration.
/// Layers user-specific settings in LocalAppData over base appsettings.json.
/// </summary>
public sealed class ConfigurationService : IConfigurationService
{
    private readonly string _baseConfigFilePath;
    private readonly string _userConfigFilePath;
    private AppSettings _settings = new();

    public AppSettings Settings => _settings;

    public ConfigurationService(string? configFilePath = null)
    {
        _baseConfigFilePath = configFilePath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
        _userConfigFilePath = ResolveUserConfigPath();
        Reload();
    }

    private static string ResolveUserConfigPath()
    {
        try
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var dir = Path.Combine(appData, "ComicReader");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "usersettings.json");
        }
        catch
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "usersettings.json");
        }
    }

    public void Reload()
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory);

        if (File.Exists(_baseConfigFilePath))
        {
            builder.AddJsonFile(_baseConfigFilePath, optional: true, reloadOnChange: true);
        }

        if (File.Exists(_userConfigFilePath))
        {
            builder.AddJsonFile(_userConfigFilePath, optional: true, reloadOnChange: true);
        }

        var configuration = builder.Build();
        var boundSettings = new AppSettings();
        configuration.Bind(boundSettings);

        _settings = boundSettings;
    }

    public string? GetRootDirectory()
    {
        var configuredPath = _settings.Library.DefaultLibraryPath;
        if (!string.IsNullOrWhiteSpace(configuredPath) && Directory.Exists(configuredPath))
        {
            return configuredPath;
        }

        return null;
    }

    public void SaveRootDirectory(string path)
    {
        _settings.Library.DefaultLibraryPath = path;

        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(_settings, options);
            File.WriteAllText(_userConfigFilePath, json);
        }
        catch
        {
            // Gracefully ignore write failures in restricted environments
        }
    }
}
