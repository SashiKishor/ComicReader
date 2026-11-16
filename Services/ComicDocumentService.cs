using ComicReader.Models;
using ComicReader.Services.Interfaces;
using ComicReader.Utilities;
using Docnet.Core;
using Docnet.Core.Models;
using Docnet.Core.Readers;
using SharpCompress.Archives;
using System.IO;
using System.IO.Compression;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ComicReader.Services;

/// <summary>
/// Service responsible for loading, decoding, and caching pages of comic archives (.cbz, .cbr) and PDFs on demand.
/// </summary>
public sealed class ComicDocumentService : IComicDocumentService
{
    private static readonly HashSet<string> s_imageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".bmp", ".gif"
    };

    private string? _currentFilePath;
    private bool _isPdf;
    private bool _isCbz;
    private IArchive? _archive;
    private System.IO.Compression.ZipArchive? _zipArchive;
    private IDocReader? _pdfReader;
    private readonly List<string> _archiveImageEntryKeys = [];
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task<int> OpenDocumentAsync(string filePath, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            CloseDocumentInternal();

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("Comic file not found", filePath);
            }

            _currentFilePath = filePath;
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            _isPdf = ext == ".pdf";
            _isCbz = ext == ".cbz" || ext == ".zip";

            if (_isPdf)
            {
                // PDFium requires non-zero page scaling. 1.5x scaling provides crisp rendering for reading.
                _pdfReader = DocLib.Instance.GetDocReader(filePath, new PageDimensions(1.5));
                return _pdfReader.GetPageCount();
            }
            else if (_isCbz)
            {
                _zipArchive = System.IO.Compression.ZipFile.OpenRead(filePath);
                _archiveImageEntryKeys.Clear();

                // Strictly filter valid image entries; exclude directories, ComicInfo.xml, thumbs.db, and macOS metadata
                var entries = _zipArchive.Entries
                    .Where(e => !string.IsNullOrEmpty(e.Name) &&
                                !e.FullName.EndsWith('/') &&
                                !e.FullName.EndsWith('\\') &&
                                !e.FullName.Contains("__MACOSX", StringComparison.OrdinalIgnoreCase) &&
                                !e.Name.StartsWith('.') &&
                                s_imageExtensions.Contains(Path.GetExtension(e.Name)))
                    .OrderBy(e => e.FullName, NaturalStringComparer.Instance);

                foreach (var entry in entries)
                {
                    _archiveImageEntryKeys.Add(entry.FullName);
                }

                return _archiveImageEntryKeys.Count;
            }
            else
            {
                // SharpCompress for RAR / CBR archives
                _archive = ArchiveFactory.Open(filePath);
                _archiveImageEntryKeys.Clear();

                var entries = _archive.Entries
                    .Where(e => !e.IsDirectory &&
                                !string.IsNullOrEmpty(e.Key) &&
                                !e.Key.EndsWith('/') &&
                                !e.Key.EndsWith('\\') &&
                                !e.Key.Contains("__MACOSX", StringComparison.OrdinalIgnoreCase) &&
                                !Path.GetFileName(e.Key).StartsWith('.') &&
                                s_imageExtensions.Contains(Path.GetExtension(e.Key)))
                    .OrderBy(e => e.Key ?? string.Empty, NaturalStringComparer.Instance);

                foreach (var entry in entries)
                {
                    if (entry.Key != null)
                    {
                        _archiveImageEntryKeys.Add(entry.Key);
                    }
                }

                return _archiveImageEntryKeys.Count;
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<ComicPage?> LoadPageAsync(int pageIndex, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_isPdf)
            {
                if (_pdfReader == null || pageIndex < 0 || pageIndex >= _pdfReader.GetPageCount())
                {
                    return null;
                }

                return await Task.Run(() =>
                {
                    try
                    {
                        using var pageReader = _pdfReader.GetPageReader(pageIndex);
                        var width = pageReader.GetPageWidth();
                        var height = pageReader.GetPageHeight();

                        if (width <= 0 || height <= 0)
                        {
                            return null;
                        }

                        var bgraBytes = pageReader.GetImage();
                        if (bgraBytes == null || bgraBytes.Length == 0)
                        {
                            return null;
                        }

                        // Create frozen, thread-safe BitmapSource directly from raw BGRA buffer
                        var bitmap = BitmapSource.Create(
                            width,
                            height,
                            96,
                            96,
                            PixelFormats.Bgra32,
                            null,
                            bgraBytes,
                            width * 4);

                        bitmap.Freeze();

                        return new ComicPage
                        {
                            PageIndex = pageIndex,
                            PageImage = bitmap,
                            PixelWidth = width,
                            PixelHeight = height
                        };
                    }
                    catch
                    {
                        return null;
                    }
                }, cancellationToken);
            }
            else if (_isCbz)
            {
                if (_zipArchive == null || pageIndex < 0 || pageIndex >= _archiveImageEntryKeys.Count)
                {
                    return null;
                }

                var key = _archiveImageEntryKeys[pageIndex];

                return await Task.Run(() =>
                {
                    try
                    {
                        var entry = _zipArchive.GetEntry(key);
                        if (entry == null)
                        {
                            return null;
                        }

                        // 1. Open entry stream and isolate into a dedicated seekable MemoryStream
                        using var entryStream = entry.Open();
                        using var memoryStream = new MemoryStream();
                        entryStream.CopyTo(memoryStream);

                        // 2. Stream Position Reset: Crucial rewind to position 0 before decoding
                        memoryStream.Position = 0;

                        // 3. Aggressive Caching & Freezing
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad; // Decode immediately into memory
                        bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                        bitmap.StreamSource = memoryStream;
                        bitmap.EndInit();
                        bitmap.Freeze(); // Safe for cross-thread UI consumption

                        return new ComicPage
                        {
                            PageIndex = pageIndex,
                            EntryName = key,
                            PageImage = bitmap,
                            PixelWidth = bitmap.PixelWidth,
                            PixelHeight = bitmap.PixelHeight
                        };
                    }
                    catch
                    {
                        return null;
                    }
                }, cancellationToken);
            }
            else
            {
                if (_archive == null || pageIndex < 0 || pageIndex >= _archiveImageEntryKeys.Count)
                {
                    return null;
                }

                var key = _archiveImageEntryKeys[pageIndex];

                return await Task.Run(() =>
                {
                    try
                    {
                        var entry = _archive.Entries.FirstOrDefault(e => e.Key == key);
                        if (entry == null)
                        {
                            return null;
                        }

                        // 1. Open entry stream and isolate into a dedicated seekable MemoryStream
                        using var entryStream = entry.OpenEntryStream();
                        using var memoryStream = new MemoryStream();
                        entryStream.CopyTo(memoryStream);

                        // 2. Stream Position Reset
                        memoryStream.Position = 0;

                        // 3. Aggressive Caching & Freezing
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                        bitmap.StreamSource = memoryStream;
                        bitmap.EndInit();
                        bitmap.Freeze();

                        return new ComicPage
                        {
                            PageIndex = pageIndex,
                            EntryName = key,
                            PageImage = bitmap,
                            PixelWidth = bitmap.PixelWidth,
                            PixelHeight = bitmap.PixelHeight
                        };
                    }
                    catch
                    {
                        return null;
                    }
                }, cancellationToken);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public void CloseDocument()
    {
        _lock.Wait();
        try
        {
            CloseDocumentInternal();
        }
        finally
        {
            _lock.Release();
        }
    }

    private void CloseDocumentInternal()
    {
        _pdfReader?.Dispose();
        _pdfReader = null;

        _zipArchive?.Dispose();
        _zipArchive = null;

        _archive?.Dispose();
        _archive = null;

        _archiveImageEntryKeys.Clear();
        _currentFilePath = null;
        _isPdf = false;
        _isCbz = false;
    }

    public void Dispose()
    {
        CloseDocumentInternal();
        _lock.Dispose();
    }
}
