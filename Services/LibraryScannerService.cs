using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ComicReader.Config;
using ComicReader.Models;
using ComicReader.Services.Interfaces;
using ComicReader.Utilities;
using ComicReader.ViewModels;

namespace ComicReader.Services;

/// <summary>
/// Service that scans directory contents, peeks for folder covers, loads cached progress,
/// and builds unified library nodes.
/// </summary>
public sealed class LibraryScannerService : ILibraryScannerService
{
    private readonly IConfigurationService _configurationService;
    private readonly ICoverExtractorService _coverExtractorService;
    private readonly IProgressTrackingService _progressService;
    private readonly IComicDocumentService _documentService;

    public LibraryScannerService(
        IConfigurationService configurationService,
        ICoverExtractorService coverExtractorService,
        IProgressTrackingService progressService,
        IComicDocumentService documentService)
    {
        _configurationService = configurationService;
        _coverExtractorService = coverExtractorService;
        _progressService = progressService;
        _documentService = documentService;
    }

    /// <summary>
    /// Scans the immediate children of the given directory, discovering subfolders and comic files side-by-side.
    /// Peeks one level deep into subfolders to discover the first comic and extract its thumbnail.
    /// Also reads cached progress for instant Netflix-style reading progress visualization.
    /// </summary>
    public async Task<IReadOnlyList<ILibraryNode>> ScanDirectoryContentsAsync(
        string directoryPath,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(async () =>
        {
            var dirInfo = new DirectoryInfo(directoryPath);
            if (!dirInfo.Exists)
            {
                return (IReadOnlyList<ILibraryNode>)[];
            }

            var supportedExtensions = _configurationService.Settings.Library.SupportedExtensions
                .Select(ext => ext.StartsWith('.') ? ext : $".{ext}")
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            progress?.Report($"Scanning '{dirInfo.Name}'...");

            var nodes = new List<ILibraryNode>();
            var folderNodes = new List<FolderItemViewModel>();
            var comicNodes = new List<ComicItemViewModel>();

            // 1. Discover Immediate Subfolders
            try
            {
                var subDirectories = dirInfo.GetDirectories();
                foreach (var subDir in subDirectories)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Skip hidden, system, or metadata directories
                    if ((subDir.Attributes & FileAttributes.Hidden) != 0 ||
                        (subDir.Attributes & FileAttributes.System) != 0 ||
                        subDir.Name.StartsWith('.'))
                    {
                        continue;
                    }

                    FileInfo[] subDirFiles = [];
                    DirectoryInfo[] subDirSubDirs = [];
                    try
                    {
                        subDirFiles = subDir.GetFiles();
                        subDirSubDirs = subDir.GetDirectories();
                    }
                    catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
                    {
                        continue; // Skip inaccessible directories
                    }

                    // Look for immediate comic files inside this subfolder
                    var immediateComics = subDirFiles
                        .Where(f => supportedExtensions.Contains(f.Extension))
                        .OrderBy(f => f.Name, NaturalStringComparer.Instance)
                        .ToList();

                    string? firstComicFilePath = immediateComics.FirstOrDefault()?.FullName;

                    // If no immediate comic found, peek at most 1 level deeper into subfolders
                    if (firstComicFilePath == null)
                    {
                        foreach (var nestedDir in subDirSubDirs.OrderBy(d => d.Name, NaturalStringComparer.Instance))
                        {
                            if (nestedDir.Name.StartsWith('.') || (nestedDir.Attributes & FileAttributes.Hidden) != 0)
                                continue;

                            try
                            {
                                var nestedComic = nestedDir.EnumerateFiles("*.*", SearchOption.TopDirectoryOnly)
                                    .Where(f => supportedExtensions.Contains(f.Extension))
                                    .OrderBy(f => f.Name, NaturalStringComparer.Instance)
                                    .FirstOrDefault();

                                if (nestedComic != null)
                                {
                                    firstComicFilePath = nestedComic.FullName;
                                    break;
                                }
                            }
                            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
                            {
                                // Skip inaccessible sub-folder
                            }
                        }
                    }

                    // Count total immediate items (comics + accessible subfolders)
                    int validSubDirsCount = subDirSubDirs.Count(d => !d.Name.StartsWith('.') && (d.Attributes & FileAttributes.Hidden) == 0);
                    int totalItemCount = immediateComics.Count + validSubDirsCount;

                    // Dynamically extract folder cover from the discovered first comic file
                    string? folderCoverPath = null;
                    if (!string.IsNullOrEmpty(firstComicFilePath))
                    {
                        try
                        {
                            folderCoverPath = await _coverExtractorService.GetOrCreateCoverAsync(firstComicFilePath, cancellationToken);
                        }
                        catch
                        {
                            folderCoverPath = null;
                        }
                    }

                    var folderVm = new FolderItemViewModel(subDir, firstComicFilePath, totalItemCount, folderCoverPath);
                    folderNodes.Add(folderVm);
                }
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                // Gracefully continue with discovered items
            }

            // 2. Discover Immediate Comic Files
            try
            {
                var files = dirInfo.GetFiles()
                    .Where(f => supportedExtensions.Contains(f.Extension) &&
                                (f.Attributes & FileAttributes.Hidden) == 0);

                foreach (var file in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var title = Path.GetFileNameWithoutExtension(file.Name);
                    var comicBook = new ComicBook
                    {
                        Title = title,
                        FilePath = file.FullName,
                        FileName = file.Name,
                        DirectoryPath = file.DirectoryName ?? directoryPath,
                        CategoryName = dirInfo.Name,
                        RelativeSubfolder = string.Empty,
                        FileExtension = file.Extension.ToLowerInvariant(),
                        FileSizeBytes = file.Length,
                        LastModifiedUtc = file.LastWriteTimeUtc
                    };

                    // Check cached reading progress in-memory (0ms)
                    var progressEntry = _progressService.GetProgress(file.FullName);
                    int readPages = 0;
                    int totalPages = 0;

                    if (progressEntry != null && progressEntry.TotalPages > 0)
                    {
                        totalPages = progressEntry.TotalPages;
                        readPages = progressEntry.LastReadPageIndex > 0 || progressEntry.HasStartedReading
                            ? progressEntry.LastReadPageIndex + 1
                            : 0;
                    }

                    string? comicCoverPath = null;
                    try
                    {
                        comicCoverPath = await _coverExtractorService.GetOrCreateCoverAsync(file.FullName, cancellationToken);
                    }
                    catch
                    {
                        comicCoverPath = null;
                    }

                    comicBook.CoverCachePath = comicCoverPath;
                    var comicVm = new ComicItemViewModel(comicBook, readPages, totalPages);
                    if (comicCoverPath != null)
                    {
                        comicVm.CoverPath = comicCoverPath;
                        comicVm.IsLoadingCover = false;
                    }

                    // If total pages not yet cached, extract asynchronously on background thread
                    if (totalPages == 0)
                    {
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                int extractedTotal = await _progressService.GetOrExtractTotalPagesAsync(
                                    file.FullName,
                                    _documentService,
                                    cancellationToken);

                                if (extractedTotal > 0)
                                {
                                    comicVm.UpdateProgress(0, extractedTotal);
                                }
                            }
                            catch { }
                        }, cancellationToken);
                    }

                    comicNodes.Add(comicVm);
                }
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                // Gracefully continue
            }

            // Natural sort: Folders first, then Files
            folderNodes.Sort((a, b) => NaturalStringComparer.Instance.Compare(a.Title, b.Title));
            comicNodes.Sort((a, b) => NaturalStringComparer.Instance.Compare(a.Title, b.Title));

            nodes.AddRange(folderNodes);
            nodes.AddRange(comicNodes);

            progress?.Report($"Found {folderNodes.Count} folders and {comicNodes.Count} comics in '{dirInfo.Name}'.");
            return (IReadOnlyList<ILibraryNode>)nodes;
        }, cancellationToken);
    }

    /// <summary>
    /// Legacy full recursive scan grouping comics into categories.
    /// </summary>
    public async Task<IReadOnlyList<ComicCategory>> ScanDirectoryAsync(
        string rootDirectory,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            var supportedExtensions = _configurationService.Settings.Library.SupportedExtensions
                .Select(ext => ext.StartsWith('.') ? ext : $".{ext}")
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var rootDirInfo = new DirectoryInfo(rootDirectory);
            if (!rootDirInfo.Exists)
            {
                return (IReadOnlyList<ComicCategory>)[];
            }

            progress?.Report($"Scanning '{rootDirInfo.Name}' for comics...");

            var fileEntries = new List<FileInfo>();
            try
            {
                var files = rootDirInfo.EnumerateFiles("*.*", SearchOption.AllDirectories)
                    .Where(file => supportedExtensions.Contains(file.Extension));

                fileEntries.AddRange(files);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                // Gracefully continue with accessible files
            }

            var categoryMap = new Dictionary<string, (string Name, string RelativePath, List<ComicBook> Books)>(StringComparer.OrdinalIgnoreCase);

            foreach (var file in fileEntries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var relativeSubfolder = Path.GetRelativePath(rootDirectory, file.DirectoryName ?? rootDirectory);
                string categoryKey = relativeSubfolder;
                string categoryDisplayName;

                if (string.IsNullOrWhiteSpace(relativeSubfolder) || relativeSubfolder == ".")
                {
                    categoryKey = string.Empty;
                    categoryDisplayName = rootDirInfo.Name;
                }
                else
                {
                    categoryDisplayName = relativeSubfolder
                        .Replace(Path.DirectorySeparatorChar, '/')
                        .Replace(Path.AltDirectorySeparatorChar, '/');
                }

                if (!categoryMap.TryGetValue(categoryKey, out var categoryData))
                {
                    categoryData = (categoryDisplayName, categoryKey, new List<ComicBook>());
                    categoryMap[categoryKey] = categoryData;
                }

                var title = Path.GetFileNameWithoutExtension(file.Name);

                var comicBook = new ComicBook
                {
                    Title = title,
                    FilePath = file.FullName,
                    FileName = file.Name,
                    DirectoryPath = file.DirectoryName ?? rootDirectory,
                    CategoryName = categoryDisplayName,
                    RelativeSubfolder = relativeSubfolder,
                    FileExtension = file.Extension.ToLowerInvariant(),
                    FileSizeBytes = file.Length,
                    LastModifiedUtc = file.LastWriteTimeUtc
                };

                categoryData.Books.Add(comicBook);
            }

            var sortedCategoryPairs = categoryMap
                .OrderBy(kvp => string.IsNullOrEmpty(kvp.Key) ? 0 : 1)
                .ThenBy(kvp => kvp.Value.Name, NaturalStringComparer.Instance)
                .ToList();

            var resultCategories = new List<ComicCategory>();

            foreach (var pair in sortedCategoryPairs)
            {
                var books = pair.Value.Books
                    .OrderBy(b => b.Title, NaturalStringComparer.Instance)
                    .ToList();

                var category = new ComicCategory
                {
                    Name = pair.Value.Name,
                    RelativePath = pair.Value.RelativePath,
                    Books = new ObservableCollection<ComicBook>(books)
                };

                resultCategories.Add(category);
            }

            progress?.Report($"Discovered {fileEntries.Count} comics in {resultCategories.Count} categories.");
            return (IReadOnlyList<ComicCategory>)resultCategories;
        }, cancellationToken);
    }
}
