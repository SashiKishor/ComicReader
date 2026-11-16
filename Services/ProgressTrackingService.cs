using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ComicReader.Models;
using ComicReader.Services.Interfaces;

namespace ComicReader.Services;

/// <summary>
/// Database-free progress tracking service that records and persists comic reading positions and total page counts.
/// Implements debounced disk writes to avoid I/O bottlenecks during rapid page turns.
/// </summary>
public sealed class ProgressTrackingService : IProgressTrackingService, IDisposable
{
    private readonly string _storageFilePath;
    private readonly Dictionary<string, ReadingProgressEntry> _progressMap = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _syncLock = new();
    private readonly SemaphoreSlim _diskLock = new(1, 1);
    private CancellationTokenSource? _debounceCts;
    private bool _isLoaded;

    public ProgressTrackingService()
    {
        _storageFilePath = ResolveStorageFilePath();
        // Warm up cache loading on background thread immediately
        _ = Task.Run(() => EnsureLoadedAsync(CancellationToken.None));
    }

    /// <summary>
    /// Synchronously retrieves the progress entry from in-memory cache if available.
    /// </summary>
    public ReadingProgressEntry? GetProgress(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return null;

        var normalizedPath = Path.GetFullPath(filePath);
        lock (_syncLock)
        {
            if (_progressMap.TryGetValue(normalizedPath, out var entry))
            {
                return entry;
            }
        }

        return null;
    }

    /// <summary>
    /// Retrieves the last recorded 0-based page index for the given comic file.
    /// </summary>
    public async Task<int> GetLastReadPageAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return 0;

        await EnsureLoadedAsync(cancellationToken);

        var normalizedPath = Path.GetFullPath(filePath);
        lock (_syncLock)
        {
            if (_progressMap.TryGetValue(normalizedPath, out var entry))
            {
                return Math.Max(0, entry.LastReadPageIndex);
            }
        }

        return 0;
    }

    /// <summary>
    /// Returns the cached total page count, or extracts it asynchronously on a background thread
    /// and persists it to reading_progress.json so archives are not re-opened on subsequent runs.
    /// </summary>
    public async Task<int> GetOrExtractTotalPagesAsync(
        string filePath,
        IComicDocumentService documentService,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return 0;

        var normalizedPath = Path.GetFullPath(filePath);

        // 1. Fast in-memory lookup
        lock (_syncLock)
        {
            if (_progressMap.TryGetValue(normalizedPath, out var entry) && entry.TotalPages > 0)
            {
                return entry.TotalPages;
            }
        }

        await EnsureLoadedAsync(cancellationToken);

        lock (_syncLock)
        {
            if (_progressMap.TryGetValue(normalizedPath, out var entry) && entry.TotalPages > 0)
            {
                return entry.TotalPages;
            }
        }

        // 2. Background extraction via document service
        try
        {
            int pageCount = await documentService.OpenDocumentAsync(normalizedPath, cancellationToken);
            documentService.CloseDocument();

            if (pageCount > 0)
            {
                lock (_syncLock)
                {
                    if (_progressMap.TryGetValue(normalizedPath, out var existing))
                    {
                        existing.TotalPages = pageCount;
                    }
                    else
                    {
                        _progressMap[normalizedPath] = new ReadingProgressEntry
                        {
                            FilePath = normalizedPath,
                            LastReadPageIndex = 0,
                            TotalPages = pageCount,
                            LastReadUtc = DateTime.UtcNow
                        };
                    }
                }

                // Debounce persist to disk
                TriggerDebouncedSave();
                return pageCount;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to extract total pages for {filePath}: {ex.Message}");
        }

        return 0;
    }

    /// <summary>
    /// Schedules a debounced disk write (500ms after the last page turn).
    /// </summary>
    public void UpdateProgress(string filePath, int pageIndex, int totalPages)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return;

        var normalizedPath = Path.GetFullPath(filePath);

        lock (_syncLock)
        {
            _progressMap[normalizedPath] = new ReadingProgressEntry
            {
                FilePath = normalizedPath,
                LastReadPageIndex = pageIndex,
                TotalPages = totalPages,
                LastReadUtc = DateTime.UtcNow
            };

            TriggerDebouncedSave();
        }
    }

    private void TriggerDebouncedSave()
    {
        try
        {
            _debounceCts?.Cancel();
            _debounceCts?.Dispose();
        }
        catch { }

        _debounceCts = new CancellationTokenSource();
        var token = _debounceCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                // Debounce delay: wait 500ms for user to finish rapid page turning
                await Task.Delay(500, token);

                if (!token.IsCancellationRequested)
                {
                    await SaveToDiskAsync(token);
                }
            }
            catch (OperationCanceledException)
            {
                // Debounce cancelled by a subsequent page turn
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to auto-save reading progress: {ex.Message}");
            }
        }, token);
    }

    /// <summary>
    /// Immediately flushes any pending debounced progress updates to disk.
    /// </summary>
    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        lock (_syncLock)
        {
            try
            {
                _debounceCts?.Cancel();
                _debounceCts?.Dispose();
                _debounceCts = null;
            }
            catch { }
        }

        await SaveToDiskAsync(cancellationToken);
    }

    private async Task EnsureLoadedAsync(CancellationToken cancellationToken)
    {
        if (_isLoaded) return;

        await _diskLock.WaitAsync(cancellationToken);
        try
        {
            if (_isLoaded) return;

            if (File.Exists(_storageFilePath))
            {
                var json = await File.ReadAllTextAsync(_storageFilePath, cancellationToken);
                var entries = JsonSerializer.Deserialize<Dictionary<string, ReadingProgressEntry>>(json);
                if (entries != null)
                {
                    lock (_syncLock)
                    {
                        foreach (var kvp in entries)
                        {
                            _progressMap[kvp.Key] = kvp.Value;
                        }
                    }
                }
            }

            _isLoaded = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Could not load reading progress from disk: {ex.Message}");
            _isLoaded = true;
        }
        finally
        {
            _diskLock.Release();
        }
    }

    private async Task SaveToDiskAsync(CancellationToken cancellationToken = default)
    {
        string payload;
        lock (_syncLock)
        {
            payload = JsonSerializer.Serialize(_progressMap, new JsonSerializerOptions
            {
                WriteIndented = true
            });
        }

        await _diskLock.WaitAsync(cancellationToken);
        try
        {
            var directory = Path.GetDirectoryName(_storageFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Write atomically via temporary file swap
            var tempFilePath = _storageFilePath + ".tmp";
            await File.WriteAllTextAsync(tempFilePath, payload, cancellationToken);
            File.Move(tempFilePath, _storageFilePath, overwrite: true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error writing reading progress file: {ex.Message}");
        }
        finally
        {
            _diskLock.Release();
        }
    }

    private static string ResolveStorageFilePath()
    {
        try
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var dir = Path.Combine(appData, "ComicReader");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "reading_progress.json");
        }
        catch
        {
            var localDir = Path.Combine(AppContext.BaseDirectory, ".cache");
            Directory.CreateDirectory(localDir);
            return Path.Combine(localDir, "reading_progress.json");
        }
    }

    public void Dispose()
    {
        try
        {
            _debounceCts?.Cancel();
            _debounceCts?.Dispose();
            _diskLock.Dispose();
        }
        catch { }
    }
}
