using CommunityToolkit.Mvvm.ComponentModel;
using ComicReader.Models;
using ComicReader.Services.Interfaces;

namespace ComicReader.ViewModels;

/// <summary>
/// ViewModel representing a single readable comic/document file item in the library grid,
/// complete with cover art and Netflix-style reading progress visualization.
/// </summary>
public partial class ComicItemViewModel : ObservableObject, ILibraryNode
{
    private readonly ComicBook _book;

    [ObservableProperty]
    private string? _coverPath;

    [ObservableProperty]
    private bool _isLoadingCover;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressPercentage))]
    [NotifyPropertyChangedFor(nameof(HasProgress))]
    [NotifyPropertyChangedFor(nameof(Subtitle))]
    private int _readPages;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressPercentage))]
    [NotifyPropertyChangedFor(nameof(HasProgress))]
    [NotifyPropertyChangedFor(nameof(Subtitle))]
    private int _totalPages;

    public ComicBook Book => _book;
    public string Title => _book.Title;
    public string FullPath => _book.FilePath;
    public bool IsFolder => false;
    public string BadgeText => _book.FileExtension.TrimStart('.').ToUpperInvariant();
    public string BadgeBackground => "#E50914"; // Netflix Red

    public double ProgressPercentage =>
        TotalPages > 0
            ? Math.Min(100.0, Math.Max(0.0, ((double)ReadPages / TotalPages) * 100.0))
            : 0.0;

    public bool HasProgress => ReadPages > 0 && TotalPages > 0 && ProgressPercentage > 0;

    public string Subtitle
    {
        get
        {
            const double mb = 1024 * 1024;
            var sizeStr = $"{_book.FileSizeBytes / mb:F1} MB";

            if (HasProgress)
            {
                return $"Page {ReadPages} of {TotalPages} • {sizeStr}";
            }

            return sizeStr;
        }
    }

    public ComicItemViewModel(ComicBook book, int readPages = 0, int totalPages = 0)
    {
        _book = book;
        _readPages = readPages;
        _totalPages = totalPages;
        _coverPath = book.CoverCachePath;
        _isLoadingCover = string.IsNullOrEmpty(_coverPath);
    }

    /// <summary>
    /// Dynamically updates the reading progress and notifies dependent UI bindings.
    /// </summary>
    public void UpdateProgress(int lastReadPageIndex, int totalPages)
    {
        TotalPages = totalPages;
        ReadPages = totalPages > 0 ? Math.Min(lastReadPageIndex + 1, totalPages) : 0;
    }

    public async Task LoadCoverAsync(ICoverExtractorService coverService, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrEmpty(CoverPath))
        {
            IsLoadingCover = false;
            return;
        }

        try
        {
            IsLoadingCover = true;
            var path = await coverService.GetOrCreateCoverAsync(_book.FilePath, cancellationToken);
            CoverPath = path;
            _book.CoverCachePath = path;
        }
        catch
        {
            CoverPath = null;
        }
        finally
        {
            IsLoadingCover = false;
        }
    }
}
