using ComicReader.Config;
using ComicReader.Services.Interfaces;
using ComicReader.Utilities;
using Docnet.Core;
using Docnet.Core.Models;
using SharpCompress.Archives;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ComicReader.Services;

/// <summary>
/// Service that extracts the first page of comic archives (.cbr, .cbz) or PDFs to create local cover thumbnails.
/// </summary>
public sealed class CoverExtractorService : ICoverExtractorService
{
    private static readonly HashSet<string> s_imageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".bmp", ".gif"
    };

    private readonly ICacheService _cacheService;
    private readonly IConfigurationService _configurationService;

    public CoverExtractorService(ICacheService cacheService, IConfigurationService configurationService)
    {
        _cacheService = cacheService;
        _configurationService = configurationService;
    }

    public async Task<string?> GetOrCreateCoverAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        var fileInfo = new FileInfo(filePath);
        var cacheKey = _cacheService.ComputeCacheKey(filePath, fileInfo.LastWriteTimeUtc, fileInfo.Length);

        if (_cacheService.HasCachedCover(cacheKey))
        {
            return _cacheService.GetCachedCoverPath(cacheKey);
        }

        var extension = fileInfo.Extension.ToLowerInvariant();
        byte[]? rawImageBytes = null;

        if (extension == ".pdf")
        {
            rawImageBytes = await ExtractPdfCoverAsync(filePath, cancellationToken);
        }
        else
        {
            rawImageBytes = await ExtractArchiveCoverAsync(filePath, cancellationToken);
        }

        if (rawImageBytes == null || rawImageBytes.Length == 0)
        {
            return null;
        }

        var optimizedBytes = OptimizeThumbnail(rawImageBytes);
        var cachedPath = await _cacheService.SaveCoverToCacheAsync(cacheKey, optimizedBytes, cancellationToken);
        return cachedPath;
    }

    private Task<byte[]?> ExtractArchiveCoverAsync(string filePath, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            try
            {
                var ext = Path.GetExtension(filePath).ToLowerInvariant();
                if (ext is ".cbz" or ".zip")
                {
                    using var zip = System.IO.Compression.ZipFile.OpenRead(filePath);
                    var coverEntry = zip.Entries
                        .Where(e => !string.IsNullOrEmpty(e.Name) &&
                                    !e.FullName.EndsWith('/') &&
                                    !e.FullName.EndsWith('\\') &&
                                    !e.FullName.Contains("__MACOSX", StringComparison.OrdinalIgnoreCase) &&
                                    !e.Name.StartsWith('.') &&
                                    s_imageExtensions.Contains(Path.GetExtension(e.Name)))
                        .OrderBy(e => e.FullName, NaturalStringComparer.Instance)
                        .FirstOrDefault();

                    if (coverEntry == null)
                    {
                        return null;
                    }

                    using var entryStream = coverEntry.Open();
                    using var memoryStream = new MemoryStream();
                    entryStream.CopyTo(memoryStream);
                    return memoryStream.ToArray();
                }
                else
                {
                    using var archive = ArchiveFactory.Open(filePath);
                    var coverEntry = archive.Entries
                        .Where(entry => !entry.IsDirectory &&
                                        !string.IsNullOrEmpty(entry.Key) &&
                                        !entry.Key.EndsWith('/') &&
                                        !entry.Key.EndsWith('\\') &&
                                        !entry.Key.Contains("__MACOSX", StringComparison.OrdinalIgnoreCase) &&
                                        !Path.GetFileName(entry.Key).StartsWith('.') &&
                                        s_imageExtensions.Contains(Path.GetExtension(entry.Key)))
                        .OrderBy(entry => entry.Key ?? string.Empty, NaturalStringComparer.Instance)
                        .FirstOrDefault();

                    if (coverEntry == null)
                    {
                        return null;
                    }

                    using var entryStream = coverEntry.OpenEntryStream();
                    using var memoryStream = new MemoryStream();
                    entryStream.CopyTo(memoryStream);
                    return memoryStream.ToArray();
                }
            }
            catch
            {
                return null;
            }
        }, cancellationToken);
    }

    private Task<byte[]?> ExtractPdfCoverAsync(string filePath, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            try
            {
                var targetWidth = _configurationService.Settings.Library.ThumbnailMaxWidth;
                var targetHeight = _configurationService.Settings.Library.ThumbnailMaxHeight;

                using var docReader = DocLib.Instance.GetDocReader(filePath, new PageDimensions(targetWidth, targetHeight));
                if (docReader.GetPageCount() <= 0)
                {
                    return null;
                }

                using var pageReader = docReader.GetPageReader(0);
                var width = pageReader.GetPageWidth();
                var height = pageReader.GetPageHeight();
                var bgraBytes = pageReader.GetImage();

                if (bgraBytes == null || bgraBytes.Length == 0 || width <= 0 || height <= 0)
                {
                    return null;
                }

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

                var encoder = new JpegBitmapEncoder
                {
                    QualityLevel = _configurationService.Settings.Library.ThumbnailQuality
                };
                encoder.Frames.Add(BitmapFrame.Create(bitmap));

                using var ms = new MemoryStream();
                encoder.Save(ms);
                return ms.ToArray();
            }
            catch
            {
                return null;
            }
        }, cancellationToken);
    }

    private byte[] OptimizeThumbnail(byte[] rawBytes)
    {
        try
        {
            using var inputStream = new MemoryStream(rawBytes);
            var decoder = BitmapDecoder.Create(
                inputStream,
                BitmapCreateOptions.None,
                BitmapCacheOption.OnLoad);

            if (decoder.Frames.Count == 0)
            {
                return rawBytes;
            }

            var frame = decoder.Frames[0];
            var maxWidth = _configurationService.Settings.Library.ThumbnailMaxWidth;
            var maxHeight = _configurationService.Settings.Library.ThumbnailMaxHeight;

            if (frame.PixelWidth <= maxWidth && frame.PixelHeight <= maxHeight)
            {
                return rawBytes;
            }

            double scaleX = (double)maxWidth / frame.PixelWidth;
            double scaleY = (double)maxHeight / frame.PixelHeight;
            double scale = Math.Min(scaleX, scaleY);

            var scaled = new TransformedBitmap(frame, new System.Windows.Media.ScaleTransform(scale, scale));
            scaled.Freeze();

            var encoder = new JpegBitmapEncoder
            {
                QualityLevel = _configurationService.Settings.Library.ThumbnailQuality
            };
            encoder.Frames.Add(BitmapFrame.Create(scaled));

            using var outputStream = new MemoryStream();
            encoder.Save(outputStream);
            return outputStream.ToArray();
        }
        catch
        {
            return rawBytes;
        }
    }
}

